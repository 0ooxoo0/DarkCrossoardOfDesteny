using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Крафт пилюль: проверки, шанс успеха, списание ингредиентов и качество.
/// Не знает об UI и сцене — вызывает его панель или печь.
/// </summary>
[DisallowMultipleComponent]
public class AlchemyService : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private AlchemyConfig config;
    [SerializeField] private AlchemySkill skill;
    [SerializeField] private CultivationSystem cultivation;
    [SerializeField] private DaoSystem dao;

    private static readonly HashSet<string> MissingVariantWarnings = new HashSet<string>();
    private static ItemData[] itemCache;
    private static AlchemyConfig fallbackConfig;

    /// <summary>Источник случайного числа 0..1. Подменяется тестами и ручной проверкой.</summary>
    public static Func<float> Random01 = () => UnityEngine.Random.value;

    public AlchemyConfig Config => config;
    public AlchemySkill Skill => skill;

    /// <summary>Передаёт ссылки из bootstrap/сетапа.</summary>
    public void Initialize(AlchemyConfig alchemyConfig, AlchemySkill alchemySkill,
        CultivationSystem cultivationSystem, DaoSystem daoSystem)
    {
        config = alchemyConfig;
        skill = alchemySkill;
        cultivation = cultivationSystem;
        dao = daoSystem;
    }

    /// <summary>Шанс успеха с учётом грейда, Прозрения и места крафта.</summary>
    public float ComputeChance(AlchemyRecipe recipe, bool atStation)
    {
        if (recipe == null) return 0f;

        AlchemyConfig cfg = ResolveConfig();
        int playerRank = PlayerRank;
        int requiredRank = recipe.RequiredGrade.RankIndex;
        int comprehension = cultivation != null ? cultivation.Comprehension : 0;

        float chance = recipe.BaseSuccessChance
                       + (playerRank - requiredRank) * cfg.ChancePerRank
                       + comprehension * cfg.ChancePerComprehension
                       + (atStation ? cfg.StationBonus : -cfg.PortablePenalty);

        return Mathf.Clamp(chance, cfg.MinChance, cfg.MaxChance);
    }

    /// <summary>Проверяет все условия. Причина — короткая русская строка для UI.</summary>
    public bool CanCraft(AlchemyRecipe recipe, Inventory inventory, bool atStation, out string reason)
    {
        reason = string.Empty;

        if (recipe == null)
        {
            reason = "Рецепт не выбран";
            return false;
        }

        if (inventory == null)
        {
            reason = "Инвентарь не найден";
            return false;
        }

        if (!atStation && !recipe.PortableAllowed)
        {
            reason = "Этот рецепт нельзя варить в походе";
            return false;
        }

        if (PlayerRank < recipe.RequiredGrade.RankIndex)
        {
            reason = "Нужен грейд " + recipe.RequiredGrade.DisplayName;
            return false;
        }

        int daoLevel = CurrentDaoLevel;
        if (daoLevel < recipe.RequiredDaoLevel)
        {
            reason = "Нужен уровень Дао " + recipe.RequiredDaoLevel;
            return false;
        }

        int realmIndex = cultivation != null ? cultivation.CurrentRealmIndex : 0;
        if (realmIndex < recipe.RequiredRealmIndex)
        {
            reason = "Нужен реалм " + (recipe.RequiredRealmIndex + 1);
            return false;
        }

        if (recipe.Output == null)
        {
            reason = "У рецепта нет результата";
            return false;
        }

        IReadOnlyList<AlchemyIngredient> ingredients = recipe.Ingredients;
        if (ingredients == null || ingredients.Count == 0)
        {
            reason = "У рецепта нет ингредиентов";
            return false;
        }

        for (int i = 0; i < ingredients.Count; i++)
        {
            AlchemyIngredient ingredient = ingredients[i];
            if (ingredient == null || ingredient.Item == null)
            {
                reason = "В рецепте пустой ингредиент";
                return false;
            }

            int have = inventory.GetItemCount(ingredient.Item);
            if (have < ingredient.Count)
            {
                reason = "Не хватает: " + ingredient.Item.DisplayName + " " + (ingredient.Count - have);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Варит пилюлю. При нехватке места ничего не списывает; при провале
    /// тратит часть ингредиентов и даёт немного мастерства.
    /// </summary>
    public AlchemyResult Craft(AlchemyRecipe recipe, Inventory inventory, bool atStation)
    {
        if (!CanCraft(recipe, inventory, atStation, out string reason))
            return AlchemyResult.Fail(reason);

        float chance = ComputeChance(recipe, atStation);
        ItemData normalOutput = recipe.Output;
        int count = recipe.OutputCount;

        if (inventory.GetFreeSpace(normalOutput) < count)
            return AlchemyResult.Fail("В инвентаре нет места", chance);

        float roll = Mathf.Clamp01(Random01 != null ? Random01() : UnityEngine.Random.value);

        if (roll <= chance)
        {
            PillQuality quality = ResolveQuality(recipe, atStation, roll);
            ItemData output = OutputForQuality(recipe, quality);

            if (!inventory.AddItem(output, count))
            {
                // Место проверяли до броска; сюда попадаем только при рассинхроне.
                return AlchemyResult.Fail("Не удалось положить пилюлю в инвентарь", chance);
            }

            ConsumeIngredients(recipe, inventory, 1f);
            if (skill != null) skill.AddMastery(recipe.MasteryReward);

            string message = "Сварено: " + output.DisplayName + " ×" + count + QualitySuffix(quality);
            Debug.Log("[Alchemy] " + message + " (шанс " + Mathf.RoundToInt(chance * 100f) + "%)");
            return new AlchemyResult(true, quality, count, message, chance);
        }

        ConsumeIngredients(recipe, inventory, ResolveConfig().IngredientLossOnFail);
        if (skill != null) skill.AddMastery(ResolveConfig().MasteryOnFail);

        string failMessage = "Крафт не удался: часть ингредиентов потеряна";
        Debug.Log("[Alchemy] " + failMessage + " (шанс " + Mathf.RoundToInt(chance * 100f) + "%)");
        return new AlchemyResult(false, PillQuality.Normal, 0, failMessage, chance);
    }

    /// <summary>
    /// Качество по запасу броска над порогом. Бонусы грейда и печи увеличивают запас,
    /// походный штраф — уменьшает.
    /// </summary>
    public PillQuality ResolveQuality(AlchemyRecipe recipe, bool atStation, float roll)
    {
        if (recipe == null) return PillQuality.Normal;

        AlchemyConfig cfg = ResolveConfig();
        float chance = ComputeChance(recipe, atStation);
        int rankBonus = PlayerRank - recipe.RequiredGrade.RankIndex;

        float surplus = Mathf.Clamp01(roll) - chance
                        + rankBonus * cfg.ChancePerRank
                        + (atStation ? cfg.StationBonus : -cfg.PortableQualityPenalty);

        float veinsThreshold = Mathf.Max(0f, cfg.VeinsMargin - cfg.VeinsQualityBonus);
        float cloudThreshold = Mathf.Max(veinsThreshold, cfg.CloudMargin - cfg.CloudQualityBonus);

        if (surplus >= cloudThreshold) return PillQuality.PillCloud;
        if (surplus >= veinsThreshold) return PillQuality.PillVeins;
        return PillQuality.Normal;
    }

    /// <summary>Вариант результата для обычного качества (контракт рецепта).</summary>
    public ItemData OutputForQuality(AlchemyRecipe recipe)
    {
        return OutputForQuality(recipe, PillQuality.Normal);
    }

    /// <summary>
    /// Вариант предмета по качеству: сначала явные ссылки рецепта, затем
    /// поиск по суффиксу имени ассета. Если варианта нет — обычный результат.
    /// </summary>
    public ItemData OutputForQuality(AlchemyRecipe recipe, PillQuality quality)
    {
        if (recipe == null || recipe.Output == null) return null;
        if (quality == PillQuality.Normal) return recipe.Output;

        ItemData explicitVariant = quality == PillQuality.PillCloud ? recipe.CloudOutput : recipe.VeinsOutput;
        if (explicitVariant != null) return explicitVariant;

        string suffix = quality == PillQuality.PillCloud ? "_Cloud" : "_Veins";
        string baseName = recipe.Output.name;
        if (!baseName.Contains("_Normal")) return recipe.Output;

        string wanted = baseName.Replace("_Normal", suffix);
        ItemData found = FindItemByName(wanted);
        if (found != null) return found;

        if (MissingVariantWarnings.Add(wanted))
            Debug.LogWarning("[Alchemy] Не найден вариант пилюли " + wanted + " — выдан обычный предмет.");

        return recipe.Output;
    }

    private void ConsumeIngredients(AlchemyRecipe recipe, Inventory inventory, float portion)
    {
        IReadOnlyList<AlchemyIngredient> ingredients = recipe.Ingredients;
        if (ingredients == null) return;

        float share = Mathf.Clamp01(portion);
        for (int i = 0; i < ingredients.Count; i++)
        {
            AlchemyIngredient ingredient = ingredients[i];
            if (ingredient == null || ingredient.Item == null) continue;

            int requested = share >= 1f
                ? ingredient.Count
                : Mathf.Max(1, Mathf.CeilToInt(ingredient.Count * share));

            int available = inventory.GetItemCount(ingredient.Item);
            int take = Mathf.Min(requested, available);
            if (take > 0) inventory.RemoveItem(ingredient.Item, take);
        }
    }

    private int PlayerRank => skill != null ? skill.Grade.RankIndex : AlchemyGrade.CommonLow.RankIndex;

    private int CurrentDaoLevel
    {
        get
        {
            if (dao == null || !dao.ActiveDao.HasValue) return 0;
            return dao.GetLevel(dao.ActiveDao.Value);
        }
    }

    private AlchemyConfig ResolveConfig()
    {
        if (config != null) return config;
        if (fallbackConfig == null) fallbackConfig = ScriptableObject.CreateInstance<AlchemyConfig>();
        return fallbackConfig;
    }

    private static string QualitySuffix(PillQuality quality)
    {
        switch (quality)
        {
            case PillQuality.PillVeins: return " (с жилками)";
            case PillQuality.PillCloud: return " (облачная)";
            default: return string.Empty;
        }
    }

    private static ItemData FindItemByName(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return null;

        if (itemCache == null)
            itemCache = Resources.FindObjectsOfTypeAll<ItemData>();

        for (int i = 0; i < itemCache.Length; i++)
        {
            if (itemCache[i] != null && itemCache[i].name == assetName)
                return itemCache[i];
        }

        return null;
    }
}
