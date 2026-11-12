using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Сетка карточек техник с фильтрами «Все / по Дао».
/// Кнопка «В слот» не назначает технику сама — только поднимает событие для хотбара Промта B.
/// </summary>
[DisallowMultipleComponent]
public class TechniqueListUI : MonoBehaviour
{
    private DaoSystem dao;
    private CultivationSystem cultivation;
    private CultivationConfig config;

    private RectTransform content;
    private TextMeshProUGUI hintLabel;
    private TextMeshProUGUI countLabel;
    private Button[] filterButtons;
    private Image[] filterBackgrounds;
    private DaoType? filter;
    private float hintTimer;

    public bool IsOpen { get; private set; }
    public event Action<TechniqueData> OnTechniqueAssignRequested;

    /// <summary>Запасной обработчик, если хотбар Промта B ещё не подписан.</summary>
    public Action<TechniqueData> AssignFallback { get; set; }

    public void Build(DaoSystem daoSystem, CultivationSystem cultivationSystem, CultivationConfig cultivationConfig)
    {
        dao = daoSystem;
        cultivation = cultivationSystem;
        config = cultivationConfig;

        BuildHierarchy();
        RebuildCards();
        gameObject.SetActive(false);
    }

    private void BuildHierarchy()
    {
        var root = (RectTransform)transform;
        CultivationUIFactory.Stretch(root);

        var dim = CultivationUIFactory.CreateImage("Dim", root, new Color(0.02f, 0.02f, 0.03f, 0.8f));
        CultivationUIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var windowImage = CultivationUIFactory.CreatePanel("Window", root, new Color(0.05f, 0.055f, 0.07f, 0.98f));
        CultivationUIFactory.Place(windowImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(1240f, 800f));
        var window = windowImage.rectTransform;

        var title = CultivationUIFactory.CreateText("Title", window, "ТЕХНИКИ", 34f, CultivationUIFactory.TextMain,
            TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(48f, -30f), new Vector2(400f, 44f));
        title.characterSpacing = 12f;

        countLabel = CultivationUIFactory.CreateText("Count", window, string.Empty, 14f, CultivationUIFactory.TextMuted,
            TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(countLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(50f, -74f), new Vector2(600f, 22f));

        var closeButton = CultivationUIFactory.CreateButton("Close", window, "×", new Vector2(48f, 48f), Close,
            CultivationUIFactory.Gold, 28f);
        CultivationUIFactory.Place((RectTransform)closeButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-30f, -26f), new Vector2(48f, 48f));

        BuildFilters(window);
        BuildScroll(window);

        hintLabel = CultivationUIFactory.CreateText("Hint", window, string.Empty, 15f, CultivationUIFactory.Gold,
            TextAlignmentOptions.Center);
        CultivationUIFactory.Place(hintLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 18f), new Vector2(1100f, 24f));
    }

    private void BuildFilters(RectTransform window)
    {
        filterButtons = new Button[6];
        filterBackgrounds = new Image[6];

        string[] labels = new string[6];
        labels[0] = "Все";
        for (int i = 0; i < 5; i++) labels[i + 1] = DaoTypeInfo.GetDisplayName(DaoTypeInfo.All[i]);

        for (int i = 0; i < 6; i++)
        {
            int index = i;
            DaoType? filterValue = i == 0 ? (DaoType?)null : DaoTypeInfo.All[i - 1];
            Color accent = filterValue.HasValue ? ResolveColor(filterValue.Value) : CultivationUIFactory.Gold;

            var button = CultivationUIFactory.CreateButton("Filter_" + i, window, labels[i], new Vector2(180f, 44f),
                () => SetFilter(filterValue), accent, 17f);
            CultivationUIFactory.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(48f + i * 190f, -108f), new Vector2(180f, 44f));
            filterButtons[i] = button;
            filterBackgrounds[i] = button.GetComponent<Image>();
        }
    }

    private void BuildScroll(RectTransform window)
    {
        var scrollRect = CultivationUIFactory.CreateRect("Scroll", window);
        CultivationUIFactory.Place(scrollRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(48f, -166f), new Vector2(1144f, 570f));

        var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;

        var viewport = CultivationUIFactory.CreateRect("Viewport", scrollRect);
        CultivationUIFactory.Stretch(viewport);
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
        viewport.gameObject.AddComponent<RectMask2D>();

        content = CultivationUIFactory.CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(360f, 160f);
        grid.spacing = new Vector2(16f, 16f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.padding = new RectOffset(4, 4, 4, 4);
        grid.childAlignment = TextAnchor.UpperLeft;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
    }

    public void Open()
    {
        IsOpen = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        RebuildCards();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        gameObject.SetActive(false);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    private void SetFilter(DaoType? value)
    {
        filter = value;
        RebuildCards();
    }

    private void RebuildCards()
    {
        if (content == null || dao == null) return;

        CultivationUIFactory.DestroyChildren(content);
        RefreshFilterColors();

        List<TechniqueData> techniques = filter.HasValue ? dao.GetTechniquesForDao(filter.Value) : dao.GetAllTechniques();
        int available = 0;

        for (int i = 0; i < techniques.Count; i++)
        {
            var technique = techniques[i];
            if (technique == null) continue;
            if (dao.IsTechniqueAvailable(technique)) available++;
            BuildCard(technique);
        }

        if (countLabel != null)
            countLabel.text = "Найдено: " + techniques.Count + "   ·   доступно сейчас: " + available;
    }

    private void RefreshFilterColors()
    {
        if (filterButtons == null) return;

        for (int i = 0; i < filterButtons.Length; i++)
        {
            DaoType? value = i == 0 ? (DaoType?)null : DaoTypeInfo.All[i - 1];
            bool selected = filter == value;
            if (filterBackgrounds[i] != null)
                filterBackgrounds[i].color = selected
                    ? new Color(0.24f, 0.2f, 0.13f, 1f)
                    : new Color(0.1f, 0.11f, 0.14f, 1f);
        }
    }

    private void BuildCard(TechniqueData technique)
    {
        Color accent = ResolveColor(technique.DaoType);
        bool available = dao.IsTechniqueAvailable(technique);
        int daoLevel = dao.GetLevel(technique.DaoType);

        var card = CultivationUIFactory.CreateImage("Technique_" + technique.TechniqueId,
            content, new Color(0.075f, 0.08f, 0.1f, 0.95f));
        CultivationUIFactory.AddGoldBorder(card, available ? 0.35f : 0.12f, 1f);

        var iconFrame = CultivationUIFactory.CreateImage("Icon", card.rectTransform,
            new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 1f));
        CultivationUIFactory.Place(iconFrame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(12f, -12f), new Vector2(88f, 88f));

        if (technique.Icon != null)
        {
            var iconImage = CultivationUIFactory.CreateImage("Sprite", iconFrame.rectTransform, Color.white,
                technique.Icon, false);
            CultivationUIFactory.Stretch(iconImage.rectTransform);
            iconImage.raycastTarget = false;
        }
        else
        {
            var rune = CultivationUIFactory.CreateText("Rune", iconFrame.rectTransform,
                string.IsNullOrEmpty(technique.DisplayName) ? "?" : technique.DisplayName.Substring(0, 1).ToUpperInvariant(),
                36f, accent, TextAlignmentOptions.Center);
            CultivationUIFactory.Stretch(rune.rectTransform);
        }

        var name = CultivationUIFactory.CreateText("Name", card.rectTransform, technique.DisplayName, 20f,
            available ? CultivationUIFactory.TextMain : CultivationUIFactory.TextMuted, TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(112f, -12f), new Vector2(232f, 26f));
        name.overflowMode = TextOverflowModes.Ellipsis;

        var meta = CultivationUIFactory.CreateText("Meta", card.rectTransform,
            technique.GetKindName() + " · Ранг " + technique.Rank, 14f, accent, TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(meta.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(112f, -42f), new Vector2(232f, 22f));

        var cost = CultivationUIFactory.CreateText("Cost", card.rectTransform,
            "Ци " + technique.QiCost + "  ·  КД " + technique.Cooldown.ToString("0.#") + " с", 14f,
            CultivationUIFactory.TextMuted, TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(cost.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(112f, -66f), new Vector2(232f, 22f));

        string requirement = "Нужно: Дао " + technique.RequiredDaoLevel + " ур. (сейчас " + daoLevel + ")  ·  Реалм " +
                             (technique.RequiredRealmIndex + 1);
        var requirements = CultivationUIFactory.CreateText("Requirements", card.rectTransform, requirement, 12f,
            available ? CultivationUIFactory.TextMuted : CultivationUIFactory.Danger, TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(requirements.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(112f, -90f), new Vector2(232f, 42f));

        var assign = CultivationUIFactory.CreateButton("Assign", card.rectTransform, "В слот", new Vector2(110f, 34f),
            () => RequestAssign(technique), accent, 15f);
        CultivationUIFactory.Place((RectTransform)assign.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-12f, 12f), new Vector2(110f, 34f));
        assign.interactable = available;
    }

    private void RequestAssign(TechniqueData technique)
    {
        if (OnTechniqueAssignRequested != null)
        {
            OnTechniqueAssignRequested.Invoke(technique);
            ShowHint("Запрос на назначение: " + technique.DisplayName + " (хотбар Промта B)");
            return;
        }

        if (AssignFallback != null)
        {
            AssignFallback.Invoke(technique);
            ShowHint("Назначено в хотбар: " + technique.DisplayName);
            return;
        }

        ShowHint("Хотбар ещё не подключён");
    }

    private void ShowHint(string message)
    {
        if (hintLabel == null) return;
        hintLabel.text = message;
        hintTimer = 3f;
    }

    private void Update()
    {
        if (hintTimer <= 0f) return;

        hintTimer -= Time.unscaledDeltaTime;
        if (hintTimer <= 0f && hintLabel != null)
            hintLabel.text = string.Empty;
    }

    private Color ResolveColor(DaoType type)
    {
        var path = dao != null ? dao.GetDaoPath(type) : null;
        return path != null ? path.Color : DaoTypeInfo.GetColor(type);
    }
}
