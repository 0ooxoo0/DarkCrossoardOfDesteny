using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно алхимии: список рецептов, ингредиенты «есть / нужно», шанс, качество
/// и кнопка «Варить». Работает и у печи, и в походном режиме.
/// </summary>
[DisallowMultipleComponent]
public class AlchemyPanelUI : MonoBehaviour
{
    [Header("Размеры")]
    [SerializeField] private Vector2 windowSize = new Vector2(960f, 580f);
    [SerializeField, Min(24f)] private float listWidth = 300f;
    [SerializeField, Min(24f)] private float rowHeight = 44f;

    private AlchemyService service;
    private AlchemySkill skill;
    private Inventory inventory;
    private CultivationSystem cultivation;
    private DaoSystem dao;
    private IReadOnlyList<AlchemyRecipe> allRecipes;

    private readonly List<RecipeEntry> entries = new List<RecipeEntry>();

    private RectTransform window;
    private RectTransform listRoot;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI detailsLabel;
    private TextMeshProUGUI chanceLabel;
    private TextMeshProUGUI reasonLabel;
    private TextMeshProUGUI logLabel;
    private TextMeshProUGUI hintLabel;
    private Button craftButton;

    private AlchemyStation station;
    private AlchemyRecipe selected;
    private bool atStation;
    private bool craftingInProgress;
    private bool pausedByUs;

    public bool IsOpen { get; private set; }

    private sealed class RecipeEntry
    {
        public AlchemyRecipe Recipe;
        public Button Button;
        public Image Background;
    }

    /// <summary>Собирает окно под указанным родителем (изначально скрыто).</summary>
    public void Build(AlchemyService alchemyService, AlchemySkill alchemySkill, Inventory playerInventory,
        CultivationSystem cultivationSystem, DaoSystem daoSystem,
        IReadOnlyList<AlchemyRecipe> recipes, Transform parent)
    {
        service = alchemyService;
        skill = alchemySkill;
        inventory = playerInventory;
        cultivation = cultivationSystem;
        dao = daoSystem;
        allRecipes = recipes;

        var root = (RectTransform)transform;
        root.SetParent(parent, false);
        CultivationUIFactory.Stretch(root);

        var dim = CultivationUIFactory.CreateImage("Dim", root, new Color(0f, 0f, 0f, 0.55f));
        CultivationUIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var windowRect = CultivationUIFactory.CreateRect("Window", root);
        CultivationUIFactory.Place(windowRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, windowSize);
        window = windowRect;

        var background = CultivationUIFactory.CreatePanel("Background", windowRect,
            new Color(0.07f, 0.08f, 0.1f, 0.97f));
        CultivationUIFactory.Stretch(background.rectTransform);

        var title = CultivationUIFactory.CreateText("Title", windowRect, "Алхимия", 26f,
            CultivationUIFactory.Gold);
        CultivationUIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(24f, -18f), new Vector2(windowSize.x - 96f, 32f));

        var close = CultivationUIFactory.CreateButton("Close", windowRect, "×",
            new Vector2(36f, 36f), Close, CultivationUIFactory.Danger, 22f);
        CultivationUIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-16f, -14f), new Vector2(36f, 36f));

        var listPanel = CultivationUIFactory.CreatePanel("Recipe List", windowRect,
            new Color(0.05f, 0.06f, 0.08f, 0.9f));
        CultivationUIFactory.Place(listPanel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(24f, -64f), new Vector2(listWidth, windowSize.y - 88f));
        listRoot = listPanel.rectTransform;

        titleLabel = CultivationUIFactory.CreateText("Recipe Title", windowRect, string.Empty, 22f,
            CultivationUIFactory.TextMain);
        CultivationUIFactory.Place(titleLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(listWidth + 56f, -70f), new Vector2(windowSize.x - listWidth - 96f, 30f));

        detailsLabel = CultivationUIFactory.CreateText("Details", windowRect, string.Empty, 16f,
            CultivationUIFactory.TextMain, TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(detailsLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(listWidth + 56f, -112f), new Vector2(windowSize.x - listWidth - 96f, 190f));

        chanceLabel = CultivationUIFactory.CreateText("Chance", windowRect, string.Empty, 15f,
            CultivationUIFactory.TextMuted, TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(chanceLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(listWidth + 56f, -310f), new Vector2(windowSize.x - listWidth - 96f, 72f));

        reasonLabel = CultivationUIFactory.CreateText("Reason", windowRect, string.Empty, 15f,
            CultivationUIFactory.Danger, TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(reasonLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(listWidth + 56f, -386f), new Vector2(windowSize.x - listWidth - 96f, 44f));

        craftButton = CultivationUIFactory.CreateButton("Craft", windowRect, "Варить",
            new Vector2(220f, 52f), CraftSelected, CultivationUIFactory.Gold, 22f);
        CultivationUIFactory.Place((RectTransform)craftButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(listWidth + 56f, 24f), new Vector2(220f, 52f));

        logLabel = CultivationUIFactory.CreateText("Log", windowRect, string.Empty, 15f,
            CultivationUIFactory.Success, TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(logLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(listWidth + 300f, 24f), new Vector2(windowSize.x - listWidth - 340f, 60f));

        hintLabel = CultivationUIFactory.CreateText("Hint", parent, string.Empty, 20f,
            CultivationUIFactory.Gold, TextAlignmentOptions.Center);
        CultivationUIFactory.Place(hintLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 48f), new Vector2(620f, 36f));

        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    /// <summary>Открывает окно у печи: все рецепты и бонус станции.</summary>
    public void OpenForStation(AlchemyStation alchemyStation)
    {
        if (alchemyStation == null) return;

        station = alchemyStation;
        atStation = true;
        ResetLog();
        Open(ResolveRecipes(station.Recipes));
    }

    /// <summary>Открывает походный режим: только рецепты с PortableAllowed.</summary>
    public void OpenPortable()
    {
        station = null;
        atStation = false;
        ResetLog();
        Open(ResolveRecipes(null));
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        selected = null;
        GameMenuController.Instance?.NotifyClosed(gameObject);
        gameObject.SetActive(false);

        if (pausedByUs)
        {
            pausedByUs = false;
            UiManager manager = FindFirstObjectByType<UiManager>();
            if (manager == null || !manager.IsOpen) Time.timeScale = 1f;
        }
    }

    /// <summary>Подсказка у печи. Пустая строка скрывает её.</summary>
    public void SetHint(string text)
    {
        if (hintLabel == null) return;

        bool visible = !string.IsNullOrEmpty(text);
        hintLabel.text = visible ? text : string.Empty;
    }

    private void Open(IReadOnlyList<AlchemyRecipe> recipes)
    {
        gameObject.SetActive(true);
        IsOpen = true;
        GameMenuController.Instance?.OpenPanel(gameObject);
        SetHint(string.Empty);

        // Походный крафт у печи открывается без меню: мир нужно остановить.
        pausedByUs = Time.timeScale > 0f;
        if (pausedByUs) Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RebuildList(recipes);

        if (entries.Count > 0)
            Select(entries[0].Recipe);
        else
        {
            selected = null;
            RefreshDetails();
        }
    }

    private IReadOnlyList<AlchemyRecipe> ResolveRecipes(IReadOnlyList<AlchemyRecipe> stationRecipes)
    {
        if (stationRecipes != null && stationRecipes.Count > 0)
            return stationRecipes;

        return allRecipes;
    }

    private void RebuildList(IReadOnlyList<AlchemyRecipe> recipes)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Button != null)
                Destroy(entries[i].Button.gameObject);
        }

        entries.Clear();
        if (listRoot == null || recipes == null) return;

        float y = -10f;
        for (int i = 0; i < recipes.Count; i++)
        {
            AlchemyRecipe recipe = recipes[i];
            if (recipe == null) continue;
            if (!atStation && !recipe.PortableAllowed) continue;

            var button = CultivationUIFactory.CreateButton("Recipe " + recipe.RecipeId, listRoot,
                recipe.DisplayName, new Vector2(listWidth - 20f, rowHeight - 4f),
                null, CultivationUIFactory.Gold, 15f);

            var rect = (RectTransform)button.transform;
            CultivationUIFactory.Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(10f, y), new Vector2(listWidth - 20f, rowHeight - 4f));

            AlchemyRecipe captured = recipe;
            button.onClick.AddListener(() => Select(captured));

            entries.Add(new RecipeEntry
            {
                Recipe = recipe,
                Button = button,
                Background = button.targetGraphic as Image
            });

            y -= rowHeight;
        }
    }

    private void Select(AlchemyRecipe recipe)
    {
        selected = recipe;
        RefreshSelection();
        RefreshDetails();
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Background == null) continue;

            bool active = entries[i].Recipe == selected;
            entries[i].Background.color = active
                ? new Color(0.28f, 0.23f, 0.14f, 1f)
                : CultivationUIFactory.PanelLight;
        }
    }

    private void RefreshDetails()
    {
        if (titleLabel == null) return;

        if (selected == null)
        {
            titleLabel.text = "Рецепт не выбран";
            detailsLabel.text = string.Empty;
            chanceLabel.text = string.Empty;
            reasonLabel.text = string.Empty;
            if (craftButton != null) craftButton.interactable = false;
            return;
        }

        titleLabel.text = selected.DisplayName;
        detailsLabel.text = BuildDetails(selected);
        chanceLabel.text = BuildChanceText(selected);

        string reason = string.Empty;
        bool canCraft = service != null && service.CanCraft(selected, inventory, atStation, out reason);
        reasonLabel.text = canCraft ? string.Empty : reason;
        if (craftButton != null) craftButton.interactable = canCraft && !craftingInProgress;
    }

    private string BuildDetails(AlchemyRecipe recipe)
    {
        var builder = new System.Text.StringBuilder(256);
        if (!string.IsNullOrEmpty(recipe.Description))
            builder.AppendLine(recipe.Description).AppendLine();

        builder.AppendLine("Ингредиенты:");
        IReadOnlyList<AlchemyIngredient> ingredients = recipe.Ingredients;
        if (ingredients != null)
        {
            for (int i = 0; i < ingredients.Count; i++)
            {
                AlchemyIngredient ingredient = ingredients[i];
                if (ingredient == null || ingredient.Item == null) continue;

                int have = inventory != null ? inventory.GetItemCount(ingredient.Item) : 0;
                string mark = have >= ingredient.Count ? "✓" : "×";
                builder.AppendLine("  " + mark + " " + ingredient.Item.DisplayName +
                                   "  " + have + "/" + ingredient.Count);
            }
        }

        builder.AppendLine();
        builder.AppendLine("Требуется грейд: " + recipe.RequiredGrade.DisplayName);
        builder.AppendLine("Требуется Дао: " + recipe.RequiredDaoLevel +
                           "   Реалм: " + (recipe.RequiredRealmIndex + 1));
        return builder.ToString();
    }

    private string BuildChanceText(AlchemyRecipe recipe)
    {
        if (service == null) return string.Empty;

        float chance = service.ComputeChance(recipe, atStation);
        AlchemyConfig config = service.Config;
        if (config == null)
            return "Шанс успеха: " + Mathf.RoundToInt(chance * 100f) + "%";

        float rankBonus = (ResolveRank() - recipe.RequiredGrade.RankIndex) * config.ChancePerRank;
        float venueBonus = rankBonus + (atStation ? config.StationBonus : -config.PortableQualityPenalty);

        float veinsThreshold = Mathf.Max(0f, config.VeinsMargin - config.VeinsQualityBonus);
        float cloudThreshold = Mathf.Max(veinsThreshold, config.CloudMargin - config.CloudQualityBonus);

        float veinsOrBetter = Mathf.Clamp01(venueBonus - veinsThreshold);
        float cloud = Mathf.Clamp01(venueBonus - cloudThreshold);
        float veins = Mathf.Max(0f, veinsOrBetter - cloud);

        return "Шанс успеха: " + Mathf.RoundToInt(chance * 100f) + "%\n" +
               "Жилки: " + Mathf.RoundToInt(veins * 100f) + "%   " +
               "Облако: " + Mathf.RoundToInt(cloud * 100f) + "%";
    }

    private int ResolveRank()
    {
        return skill != null ? skill.Grade.RankIndex : AlchemyGrade.CommonLow.RankIndex;
    }

    private void CraftSelected()
    {
        if (craftingInProgress || selected == null || service == null) return;
        craftingInProgress = true;

        try
        {
            AlchemyResult result = service.Craft(selected, inventory, atStation);
            logLabel.text = result.Message;
            logLabel.color = result.Success ? CultivationUIFactory.Success : CultivationUIFactory.Danger;
        }
        finally
        {
            craftingInProgress = false;
        }

        RefreshDetails();
    }

    private void ResetLog()
    {
        if (logLabel == null) return;

        logLabel.text = string.Empty;
        logLabel.color = CultivationUIFactory.Success;
    }
}
