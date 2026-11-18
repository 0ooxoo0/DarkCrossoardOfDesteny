using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Контент мира культивации: ресурсы и пилюли в Assets/Inventary/Data/Content.
/// Меню: Tools → Cultivation → 4. Setup World Content (Game).
/// Повторный запуск обновляет существующие ассеты, а не создаёт дубли.
/// </summary>
public static class CultivationWorldSetup
{
    private const string ContentFolder = "Assets/Inventary/Data/Content";
    private const string PillsFolder = ContentFolder + "/Pills";
    private const string IconsFolder =
        "Assets/Ideals/Menu/MenuSaves/Brackeys/2D Mega Pack/Items & Icons/";

    private const int DataVersion = 2;

    // [PLACEHOLDER] Иконки взяты из готового набора ассетов; часть названий не совпадает
    // с лором (цветы заменены сердцем, трава — древесиной). Заменить на финальные арты.
    private const string WoodIconPath = IconsFolder + "Pixel Art/Wood.png";
    private const string SpiritIconPath = IconsFolder + "Pixel Art/MagicEssence.png";
    private const string HeartIconPath = IconsFolder + "Pixel Art/Heart.png";
    private const string IronIconPath = IconsFolder + "Pixel Art/Iron.png";
    private const string DiamondIconPath = IconsFolder + "Pixel Art/Diamond.png";
    private const string BeastCoreIconPath = IconsFolder + "Pixel Art/Magic Egg.png";
    private const string GoldenEssenceIconPath = IconsFolder + "Gothic/SoulFragment.png";
    private const string PeltIconPath = IconsFolder + "Pixel Art/Wool.png";
    private const string MeatIconPath = IconsFolder + "Pixel Art/Meat.png";
    private const string PillIconPath = IconsFolder + "Pixel Art/Potion.png";

    private static readonly string[] QualitySuffixes = { "Normal", "Veins", "Cloud" };
    private static readonly string[] QualityNames = { "обычная", "с жилками", "облачная" };

    /// <summary>Создаёт ресурсы и пилюли. Идемпотентно.</summary>
    [MenuItem("Tools/Cultivation/4. Setup World Content (Game)", priority = 4)]
    public static void SetupWorldContent()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Cultivation", "Выйдите из Play mode перед запуском.", "OK");
            return;
        }

        EnsureFolder(PillsFolder);
        EnsureItems();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CultivationSetup] Контент мира обновлён: ресурсы и пилюли.");
    }

    private static void EnsureItems()
    {
        Sprite wood = LoadSprite(WoodIconPath);
        Sprite spirit = LoadSprite(SpiritIconPath);
        Sprite heart = LoadSprite(HeartIconPath);
        Sprite iron = LoadSprite(IronIconPath);
        Sprite diamond = LoadSprite(DiamondIconPath);
        Sprite beastCore = LoadSprite(BeastCoreIconPath);
        Sprite goldenEssence = LoadSprite(GoldenEssenceIconPath);
        Sprite pelt = LoadSprite(PeltIconPath);
        Sprite meat = LoadSprite(MeatIconPath);
        Sprite pill = LoadSprite(PillIconPath);

        CreateResource("Herb_Mortal", "res.herb.mortal", "Трава смертного",
            "Обычная горная трава. Основа большинства пилюль.",
            wood, 99, 2, "herb.mortal");
        CreateResource("Herb_Spirit", "res.herb.spirit", "Духовная трава",
            "Трава, впитавшая духовную энергию. Ценится алхимиками.",
            spirit, 99, 6, "herb.spirit");
        CreateResource("Herb_Blood", "res.herb.blood", "Кровавый цветок",
            "Алый цветок с терпким запахом. Ускоряет кровоток.",
            heart, 99, 8, "herb.blood");
        CreateResource("Ore_Iron", "res.ore.iron", "Железная руда",
            "Тяжёлый кусок руды. Годится для ковки.",
            iron, 99, 3, "ore.iron");
        CreateResource("Ore_Jade", "res.ore.jade", "Нефритовая руда",
            "Зеленоватый камень с мягким свечением.",
            diamond, 99, 15, "ore.jade");
        CreateResource("Core_Beast", "res.core.beast", "Ядро зверя",
            "Сгусток силы убитого зверя. Пульсирует теплом.",
            beastCore, 99, 20, "core.beast");
        CreateResource("Essence_Golden", "res.essence.golden", "Золотая эссенция",
            "Редкая субстанция, сияющая золотом. Нужна для великих прорывов.",
            goldenEssence, 99, 60, "essence.golden");
        CreateResource("Wood_Spirit", "res.wood.spirit", "Духовная древесина",
            "Древесина древнего дерева. Не гниёт и не горит.",
            wood, 99, 5);
        CreateResource("Pelt", "res.pelt", "Шкура",
            "Выделанная шкура животного. Пахнет дымом.",
            pelt, 99, 4);
        CreateResource("Meat_Raw", "res.meat.raw", "Сырое мясо",
            "Свежее мясо. Можно съесть, а можно пустить в пилюли.",
            meat, 99, 2);

        CreatePillSet("Pill_QiReturn", "pill.qi.return", "Пилюля возврата Ци",
            "Восстанавливает запас Ци.",
            pill, new[] { 40, 70, 110 }, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }, new[] { 0, 0, 0 });
        CreatePillSet("Pill_BoneMarrow", "pill.bone.marrow", "Пилюля очищения костного мозга",
            "Прибавляет опыт Ци текущей ступени.",
            pill, new[] { 0, 0, 0 }, new[] { 60, 110, 180 }, new[] { 0, 0, 0 }, new[] { 0, 0, 0 });
        CreatePillSet("Pill_Enlightenment", "pill.enlightenment", "Пилюля озарения",
            "Дарует Прозрение.",
            pill, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }, new[] { 1, 2, 3 }, new[] { 0, 0, 0 });
        CreatePillSet("Pill_Healing", "pill.healing", "Пилюля лечения",
            "Закрывает раны и возвращает здоровье.",
            pill, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }, new[] { 40, 70, 110 });

        CreateBreakthroughPill(pill);
    }

    private static void CreateResource(string fileName, string itemId, string displayName, string description,
        Sprite icon, int maxStack, int sellPrice, params string[] tags)
    {
        ItemData item = CreateOrUpdate(ContentFolder + "/" + fileName + ".asset", itemId, displayName, description);
        item.type = ItemType.Misc;
        item.useKind = ItemUseKind.None;
        item.stackable = maxStack > 1;
        item.maxStackSize = Mathf.Max(1, maxStack);
        item.sellPrice = Mathf.Max(0, sellPrice);
        item.icon = icon;
        item.breakthroughTags = new List<string>(tags ?? new string[0]);
        MarkDirty(item);
    }

    /// <summary>Три варианта качества: обычная, с жилками, облачная.</summary>
    private static void CreatePillSet(string fileName, string itemId, string displayName, string description,
        Sprite icon, int[] qiValues, int[] experienceValues, int[] comprehensionValues, int[] healValues)
    {
        for (int quality = 0; quality < QualitySuffixes.Length; quality++)
        {
            string suffix = QualitySuffixes[quality];
            ItemData item = CreateOrUpdate(
                PillsFolder + "/" + fileName + "_" + suffix + ".asset",
                itemId + "." + suffix.ToLowerInvariant(),
                displayName + " (" + QualityNames[quality] + ")",
                description);

            item.type = ItemType.Consumable;
            item.useKind = ItemUseKind.Pill;
            item.stackable = true;
            item.maxStackSize = 20;
            item.sellPrice = 5 + quality * 15;
            item.icon = icon;
            item.breakthroughTags = new List<string>();
            item.qiRestore = ValueAt(qiValues, quality);
            item.experienceQiGain = ValueAt(experienceValues, quality);
            item.comprehensionGain = ValueAt(comprehensionValues, quality);
            item.healAmount = ValueAt(healValues, quality);
            MarkDirty(item);
        }
    }

    private static void CreateBreakthroughPill(Sprite icon)
    {
        for (int quality = 0; quality < QualitySuffixes.Length; quality++)
        {
            string suffix = QualitySuffixes[quality];
            ItemData item = CreateOrUpdate(
                PillsFolder + "/Pill_Breakthrough_" + suffix + ".asset",
                "pill.breakthrough." + suffix.ToLowerInvariant(),
                "Пилюля прорыва (" + QualityNames[quality] + ")",
                "Считается материалом прорыва: заменяет ядро зверя.");

            item.type = ItemType.Consumable;
            item.useKind = ItemUseKind.BreakthroughMaterial;
            item.stackable = true;
            item.maxStackSize = 10;
            item.sellPrice = 40 + quality * 30;
            item.icon = icon;
            item.breakthroughTags = new List<string> { "core.beast" };
            item.qiRestore = 0;
            item.experienceQiGain = 0;
            item.comprehensionGain = 0;
            item.healAmount = 0;
            MarkDirty(item);
        }
    }

    private static ItemData CreateOrUpdate(string assetPath, string itemId, string displayName, string description)
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, assetPath);
        }

        item.itemId = itemId;
        item.itemName = displayName;
        item.description = description;
        return item;
    }

    private static void MarkDirty(ItemData item)
    {
        item.MarkMigrated(DataVersion);
        EditorUtility.SetDirty(item);
    }

    private static int ValueAt(int[] values, int index)
    {
        if (values == null || index < 0 || index >= values.Length) return 0;
        return Mathf.Max(0, values[index]);
    }

    private static Sprite LoadSprite(string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
            Debug.LogWarning("[CultivationSetup] Не найден спрайт: " + assetPath + ". Останется [PLACEHOLDER].");
        return sprite;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(folder);

        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
