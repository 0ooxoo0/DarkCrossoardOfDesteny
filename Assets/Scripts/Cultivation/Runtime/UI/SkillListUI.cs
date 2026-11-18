using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Экран скиллов: вкладки «Техники» и «Пассивные», статусы изучения,
/// описание эффекта и назначение техники на панель быстрого доступа.
/// </summary>
[DisallowMultipleComponent]
public class SkillListUI : MonoBehaviour
{
    [Header("Размеры")]
    [SerializeField] private Vector2 windowSize = new Vector2(1040f, 640f);
    [SerializeField, Min(80f)] private float rowHeight = 96f;

    private DaoSystem dao;
    private CultivationSystem cultivation;
    private PassiveSkillService passives;
    private CultivationAbilityBridge bridge;

    private RectTransform window;
    private RectTransform content;
    private TextMeshProUGUI countLabel;
    private Button[] tabButtons;
    private Image[] tabBackgrounds;

    private int tabIndex;

    public bool IsOpen { get; private set; }

    /// <summary>Собирает окно под указанным родителем (изначально скрыто).</summary>
    public void Build(DaoSystem daoSystem, CultivationSystem cultivationSystem,
        PassiveSkillService passiveService, CultivationAbilityBridge abilityBridge, Transform parent)
    {
        dao = daoSystem;
        cultivation = cultivationSystem;
        passives = passiveService;
        bridge = abilityBridge;

        var root = (RectTransform)transform;
        root.SetParent(parent, false);
        CultivationUIFactory.Stretch(root);

        var dim = CultivationUIFactory.CreateImage("Dim", root, new Color(0f, 0f, 0f, 0.55f));
        CultivationUIFactory.Stretch(dim.rectTransform);

        var windowRect = CultivationUIFactory.CreateRect("Window", root);
        CultivationUIFactory.Place(windowRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, windowSize);
        window = windowRect;

        var background = CultivationUIFactory.CreatePanel("Background", windowRect,
            new Color(0.07f, 0.08f, 0.1f, 0.97f));
        CultivationUIFactory.Stretch(background.rectTransform);

        var title = CultivationUIFactory.CreateText("Title", windowRect, "Скиллы", 26f,
            CultivationUIFactory.Gold);
        CultivationUIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(24f, -18f), new Vector2(300f, 32f));

        countLabel = CultivationUIFactory.CreateText("Count", windowRect, string.Empty, 14f,
            CultivationUIFactory.TextMuted);
        CultivationUIFactory.Place(countLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(180f, -22f), new Vector2(560f, 24f));

        var close = CultivationUIFactory.CreateButton("Close", windowRect, "×",
            new Vector2(36f, 36f), Close, CultivationUIFactory.Danger, 22f);
        CultivationUIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-16f, -14f), new Vector2(36f, 36f));

        BuildTabs(windowRect);
        BuildScroll(windowRect);

        gameObject.SetActive(false);
        Subscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void BuildTabs(RectTransform parent)
    {
        tabButtons = new Button[2];
        tabBackgrounds = new Image[2];

        string[] labels = { "Техники", "Пассивные" };
        for (int i = 0; i < labels.Length; i++)
        {
            int captured = i;
            var button = CultivationUIFactory.CreateButton("Tab " + labels[i], parent, labels[i],
                new Vector2(180f, 40f), () => SelectTab(captured), CultivationUIFactory.Gold, 17f);
            CultivationUIFactory.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f + i * 190f, -58f), new Vector2(180f, 40f));

            tabButtons[i] = button;
            tabBackgrounds[i] = button.GetComponent<Image>();
        }
    }

    private void BuildScroll(RectTransform parent)
    {
        var scrollRect = CultivationUIFactory.CreateRect("Scroll", parent);
        CultivationUIFactory.Place(scrollRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(24f, -108f), new Vector2(windowSize.x - 48f, windowSize.y - 136f));

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

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

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
        Rebuild();
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

    public void SelectTab(int index)
    {
        tabIndex = Mathf.Clamp(index, 0, 1);
        RefreshTabColors();
        if (IsOpen) Rebuild();
    }

    private void Rebuild()
    {
        if (content == null || dao == null) return;

        CultivationUIFactory.DestroyChildren(content);
        RefreshTabColors();

        List<TechniqueData> all = dao.GetAllTechniques();
        int shown = 0;

        for (int i = 0; i < all.Count; i++)
        {
            TechniqueData technique = all[i];
            if (technique == null) continue;

            bool passive = technique.Kind == TechniqueKind.Passive;
            if (passive != (tabIndex == 1)) continue;

            BuildRow(technique);
            shown++;
        }

        if (countLabel != null)
            countLabel.text = tabIndex == 0 ? "Активные техники: " + shown : "Пассивные скиллы: " + shown;
    }

    private void BuildRow(TechniqueData technique)
    {
        var rowRect = CultivationUIFactory.CreateRect("Skill " + technique.TechniqueId, content);
        rowRect.sizeDelta = new Vector2(0f, rowHeight);
        var layoutElement = rowRect.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = rowHeight;

        var background = CultivationUIFactory.CreateImage("Background", rowRect,
            new Color(0.1f, 0.11f, 0.14f, 0.95f));
        CultivationUIFactory.Stretch(background.rectTransform);

        bool learned = dao.HasLearnedTechnique(technique);
        bool available = dao.IsTechniqueAvailable(technique);
        Color accent = learned ? CultivationUIFactory.Gold : CultivationUIFactory.GoldDim;
        CultivationUIFactory.AddGoldBorder(background, learned ? 0.75f : 0.3f, 1.2f);

        var icon = CultivationUIFactory.CreateImage("Icon", rowRect,
            technique.Icon != null ? Color.white : DaoTypeInfo.GetColor(technique.DaoType),
            technique.Icon, false);
        CultivationUIFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(12f, 0f), new Vector2(64f, 64f));

        var name = CultivationUIFactory.CreateText("Name", rowRect, technique.DisplayName, 19f,
            CultivationUIFactory.TextMain);
        CultivationUIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(88f, -10f), new Vector2(420f, 26f));

        string school = DaoTypeInfo.GetDisplayName(technique.DaoType) + " · " + technique.GetKindName();
        var sub = CultivationUIFactory.CreateText("School", rowRect, school, 13f,
            CultivationUIFactory.TextMuted);
        CultivationUIFactory.Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(88f, -34f), new Vector2(420f, 20f));

        var description = CultivationUIFactory.CreateText("Description", rowRect,
            string.IsNullOrEmpty(technique.Description) ? string.Empty : technique.Description,
            13f, CultivationUIFactory.TextMuted, TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(description.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(88f, -54f), new Vector2(560f, 36f));

        var status = CultivationUIFactory.CreateText("Status", rowRect, BuildStatus(technique, learned, available),
            14f, learned ? CultivationUIFactory.Success : CultivationUIFactory.Danger,
            TextAlignmentOptions.MidlineRight);
        CultivationUIFactory.Place(status.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-24f, -14f), new Vector2(280f, 24f));

        var info = CultivationUIFactory.CreateText("Info", rowRect, BuildEffect(technique), 14f,
            accent, TextAlignmentOptions.MidlineRight);
        CultivationUIFactory.Place(info.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-24f, -8f), new Vector2(300f, 44f));

        if (technique.Kind == TechniqueKind.Passive) return;

        var assign = CultivationUIFactory.CreateButton("Assign", rowRect, "Назначить",
            new Vector2(140f, 34f), () => Assign(technique), CultivationUIFactory.Gold, 14f);
        CultivationUIFactory.Place((RectTransform)assign.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-24f, 10f), new Vector2(140f, 34f));

        assign.interactable = learned && bridge != null;
    }

    private string BuildStatus(TechniqueData technique, bool learned, bool available)
    {
        if (learned) return "Открыто";
        if (available) return "Можно изучить";

        int daoLevel = dao.GetLevel(technique.DaoType);
        if (daoLevel < technique.RequiredDaoLevel)
            return "Нужен уровень Дао " + technique.RequiredDaoLevel;

        int realmIndex = cultivation != null ? cultivation.CurrentRealmIndex : 0;
        if (realmIndex < technique.RequiredRealmIndex)
            return "Нужен реалм " + (technique.RequiredRealmIndex + 1);

        return "Недоступно";
    }

    private string BuildEffect(TechniqueData technique)
    {
        if (technique.Kind == TechniqueKind.Passive)
            return DescribePassive(technique.PassiveEffect, technique.PassiveValue);

        return "Ци " + technique.QiCost + "   ·   КД " + technique.Cooldown.ToString("0.#") + " с";
    }

    private static string DescribePassive(PassiveEffectKind kind, float value)
    {
        int percent = Mathf.RoundToInt(Mathf.Abs(value) * 100f);
        string sign = value >= 0f ? "+" : "−";

        switch (kind)
        {
            case PassiveEffectKind.MaxHealthPercent: return sign + percent + "% к максимуму HP";
            case PassiveEffectKind.QiRegenPercent: return sign + percent + "% к регену Ци";
            case PassiveEffectKind.MaxQiPercent: return sign + percent + "% к максимуму Ци";
            case PassiveEffectKind.SpellPowerPercent: return sign + percent + "% к силе техник";
            case PassiveEffectKind.MeleeDamagePercent: return sign + percent + "% к урону оружием";
            case PassiveEffectKind.DamageReductionPercent: return "−" + percent + "% получаемого урона";
            case PassiveEffectKind.ComprehensionGainPercent: return sign + percent + "% к Прозрению";
            case PassiveEffectKind.AlchemySuccessPercent: return sign + percent + "% к успеху алхимии";
            default: return string.Empty;
        }
    }

    private void Assign(TechniqueData technique)
    {
        if (bridge == null || technique == null) return;
        bridge.AssignTechnique(technique);
    }

    private void RefreshTabColors()
    {
        if (tabBackgrounds == null) return;

        for (int i = 0; i < tabBackgrounds.Length; i++)
        {
            if (tabBackgrounds[i] == null) continue;
            tabBackgrounds[i].color = i == tabIndex
                ? new Color(0.28f, 0.23f, 0.14f, 1f)
                : CultivationUIFactory.PanelLight;
        }
    }

    private void Subscribe()
    {
        if (dao != null)
        {
            dao.OnTechniqueLearned -= HandleTechniqueChanged;
            dao.OnTechniqueLearned += HandleTechniqueChanged;
            dao.OnDaoLevelChanged -= HandleDaoLevelChanged;
            dao.OnDaoLevelChanged += HandleDaoLevelChanged;
        }

        if (passives != null)
        {
            passives.OnModifiersChanged -= HandleModifiersChanged;
            passives.OnModifiersChanged += HandleModifiersChanged;
        }
    }

    private void Unsubscribe()
    {
        if (dao != null)
        {
            dao.OnTechniqueLearned -= HandleTechniqueChanged;
            dao.OnDaoLevelChanged -= HandleDaoLevelChanged;
        }

        if (passives != null)
            passives.OnModifiersChanged -= HandleModifiersChanged;
    }

    private void HandleTechniqueChanged(TechniqueData technique)
    {
        if (IsOpen) Rebuild();
    }

    private void HandleDaoLevelChanged(DaoType type, int level)
    {
        if (IsOpen) Rebuild();
    }

    private void HandleModifiersChanged()
    {
        if (IsOpen) Rebuild();
    }
}
