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
    private DaoTreeUI tree;
    private Button treeButton;

    private RectTransform window;
    private RectTransform content;
    private TextMeshProUGUI countLabel;
    private TextMeshProUGUI hintLabel;
    private float hintTimer;
    private Button[] tabButtons;
    private Image[] tabBackgrounds;

    private int tabIndex;
    private bool pausedByUs;

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

        hintLabel = CultivationUIFactory.CreateText("Hint", windowRect, string.Empty, 14f,
            CultivationUIFactory.Gold, TextAlignmentOptions.Center);
        CultivationUIFactory.Place(hintLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 10f), new Vector2(windowSize.x - 60f, 26f));

        gameObject.SetActive(false);
        Subscribe();
    }

    private void Update()
    {
        if (hintTimer <= 0f) return;

        hintTimer -= Time.unscaledDeltaTime;
        if (hintTimer <= 0f && hintLabel != null) hintLabel.text = string.Empty;
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

        // Третья кнопка ведёт в древо Дао: список остаётся плоским, дерево — отдельный экран.
        var tree = CultivationUIFactory.CreateButton("Open Tree", parent, "Древо Дао",
            new Vector2(180f, 40f), OpenTree, CultivationUIFactory.Success, 17f);
        CultivationUIFactory.Place((RectTransform)tree.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(24f + labels.Length * 190f, -58f), new Vector2(180f, 40f));
        treeButton = tree;
    }

    /// <summary>Древо Дао: тот же экран скиллов, но другой режим просмотра.</summary>
    private void OpenTree()
    {
        if (tree == null)
        {
            ShowHint("Древо Дао недоступно: экран не собран.");
            return;
        }

        Close();
        tree.Open();
    }

    /// <summary>Ссылка на экран дерева (задаёт CultivationUIRoot).</summary>
    public void SetTree(DaoTreeUI treeUi)
    {
        tree = treeUi;
        if (treeButton != null) treeButton.interactable = tree != null;
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
        GameMenuController.Instance?.OpenPanel(gameObject);
        transform.SetAsLastSibling();
        PauseGame();
        Rebuild();
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        gameObject.SetActive(false);
        GameMenuController.Instance?.NotifyClosed(gameObject);
        ResumeGame();
    }

    /// <summary>Открытие из панели культивации: паузу уже держит она — не перезаписываем её.</summary>
    public void OpenFromPanel()
    {
        IsOpen = true;
        gameObject.SetActive(true);
        GameMenuController.Instance?.OpenPanel(gameObject);
        transform.SetAsLastSibling();
        pausedByUs = false;
        Rebuild();
    }

    /// <summary>Ставит паузу, как CultivationUI: иначе можно умереть в открытом меню.</summary>
    private void PauseGame()
    {
        if (Time.timeScale <= 0f) return;
        Time.timeScale = 0f;
        pausedByUs = true;
    }

    private void ResumeGame()
    {
        if (!pausedByUs) return;
        pausedByUs = false;
        Time.timeScale = 1f;
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
        bool passive = technique.Kind == TechniqueKind.Passive;
        bool passiveActive = passive && passives != null && passives.IsPassiveActive(technique);
        Color accent = learned || passiveActive ? CultivationUIFactory.Gold : CultivationUIFactory.GoldDim;
        CultivationUIFactory.AddGoldBorder(background, learned || passiveActive ? 0.75f : 0.3f, 1.2f);

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

        string foreignDao = BuildForeignDaoWarning(technique);
        string descriptionText = string.IsNullOrEmpty(technique.Description) ? string.Empty : technique.Description;
        if (!string.IsNullOrEmpty(foreignDao))
            descriptionText = foreignDao + "\n" + descriptionText;

        var description = CultivationUIFactory.CreateText("Description", rowRect, descriptionText,
            13f, string.IsNullOrEmpty(foreignDao) ? CultivationUIFactory.TextMuted : CultivationUIFactory.Danger,
            TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(description.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(88f, -54f), new Vector2(560f, 36f));

        var status = CultivationUIFactory.CreateText("Status", rowRect,
            BuildStatus(technique, learned, available, passiveActive),
            14f, passive ? (passiveActive ? CultivationUIFactory.Success : CultivationUIFactory.Danger)
                         : (learned ? CultivationUIFactory.Success : CultivationUIFactory.Danger),
            TextAlignmentOptions.MidlineRight);
        CultivationUIFactory.Place(status.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-24f, -14f), new Vector2(280f, 24f));

        var info = CultivationUIFactory.CreateText("Info", rowRect, BuildEffect(technique), 14f,
            accent, TextAlignmentOptions.MidlineRight);
        CultivationUIFactory.Place(info.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-24f, -8f), new Vector2(300f, 44f));

        if (technique.Kind == TechniqueKind.Passive) return;

        BuildAssignControls(technique, rowRect);
    }

    /// <summary>
    /// Слоты 4 и 5 явными кнопками с пометкой «занято: X» и снятие. Пассив и Block
    /// в слот не кладутся — вместо кнопок причина.
    /// </summary>
    private void BuildAssignControls(TechniqueData technique, RectTransform rowRect)
    {
        if (technique.Kind == TechniqueKind.Passive || technique.Kind == TechniqueKind.Block)
        {
            var note = CultivationUIFactory.CreateText("AssignNote", rowRect, "Механика в разработке",
                14f, CultivationUIFactory.Danger, TextAlignmentOptions.MidlineRight);
            CultivationUIFactory.Place(note.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-24f, 18f), new Vector2(300f, 28f));
            return;
        }

        for (int slot = 3; slot < AbilitySystem.SlotCount; slot++)
        {
            int captured = slot;
            TechniqueData occupant = bridge != null ? bridge.GetAssignedTechnique(slot) : null;
            bool mine = occupant == technique;
            string label = mine
                ? "В слоте " + (slot + 1)
                : "В слот " + (slot + 1) + (occupant != null ? " (занято: " + Shorten(occupant.DisplayName) + ")" : string.Empty);

            var button = CultivationUIFactory.CreateButton("AssignSlot" + (slot + 1), rowRect, label,
                new Vector2(240f, 30f), () => Assign(technique, captured), CultivationUIFactory.Gold, 13f);
            CultivationUIFactory.Place((RectTransform)button.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-24f, 12f + (AbilitySystem.SlotCount - 1 - slot) * 34f), new Vector2(240f, 30f));
            button.interactable = bridge != null && !mine;
        }

        var unassign = CultivationUIFactory.CreateButton("Unassign", rowRect, "Снять",
            new Vector2(90f, 30f), () => Unassign(technique), CultivationUIFactory.Danger, 13f);
        CultivationUIFactory.Place((RectTransform)unassign.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-274f, 12f), new Vector2(90f, 30f));
        unassign.interactable = bridge != null && IsAssigned(technique);
    }

    /// <summary>Техника чужого Дао назначается, но не сработает — предупреждаем прямо в строке.</summary>
    private string BuildForeignDaoWarning(TechniqueData technique)
    {
        if (technique.Kind != TechniqueKind.Active || dao == null || !dao.ActiveDao.HasValue) return string.Empty;
        if (dao.ActiveDao.Value == technique.DaoType) return string.Empty;

        return "Техника школы " + DaoTypeInfo.GetDisplayName(technique.DaoType) +
               "; активный Дао — " + DaoTypeInfo.GetDisplayName(dao.ActiveDao.Value) +
               ". Не сработает, пока не сменишь Дао у алтаря.";
    }

    private bool IsAssigned(TechniqueData technique)
    {
        if (bridge == null || technique == null) return false;

        for (int slot = 3; slot < AbilitySystem.SlotCount; slot++)
        {
            if (bridge.GetAssignedTechnique(slot) == technique) return true;
        }
        return false;
    }

    private static string Shorten(string value)
    {
        if (string.IsNullOrEmpty(value)) return "?";
        return value.Length <= 14 ? value : value.Substring(0, 13) + "…";
    }

    private string BuildStatus(TechniqueData technique, bool learned, bool available, bool passiveActive = false)
    {
        if (technique.Kind == TechniqueKind.Passive)
        {
            if (passiveActive) return "Активна";
            if (passives != null)
            {
                string reason = passives.GetInactiveReason(technique);
                if (!string.IsNullOrEmpty(reason)) return reason;
            }
            return "Неактивна";
        }

        // Block ещё не реализован; Aura назначается (MagicAura в AbilitySystem).
        if (technique.Kind == TechniqueKind.Block)
            return "Механика в разработке";

        if (learned) return "Открыто";
        if (available) return "Доступно";

        if (dao != null && !dao.IsDaoUnlocked(technique.DaoType))
            return "Дао " + DaoTypeInfo.GetDisplayName(technique.DaoType) + " не открыт";

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

        string effect = "Ци " + technique.QiCost + "   ·   КД " + technique.Cooldown.ToString("0.#") + " с";
        if (technique.Power > 0) effect += "   ·   сила " + technique.Power;

        string requirements = "Дао " + technique.RequiredDaoLevel + " ур.   ·   реалм " +
                              (technique.RequiredRealmIndex + 1);
        return effect + "\n" + requirements;
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

    private void Assign(TechniqueData technique, int slotIndex = -1)
    {
        if (bridge == null || technique == null) return;

        TechniqueData occupant = slotIndex >= 3 ? bridge.GetAssignedTechnique(slotIndex) : null;
        if (occupant != null && occupant != technique)
            ShowHint("Слот " + (slotIndex + 1) + " занят: " + occupant.DisplayName + ". Нажмите ещё раз, чтобы заменить.");

        string warning = BuildForeignDaoWarning(technique);
        bridge.AssignTechnique(technique, slotIndex);
        ShowHint(string.IsNullOrEmpty(warning) ? bridge.LastAssignMessage : warning);
        if (IsOpen) Rebuild();
    }

    private void Unassign(TechniqueData technique)
    {
        if (bridge == null || technique == null) return;

        for (int slot = 3; slot < AbilitySystem.SlotCount; slot++)
        {
            if (bridge.GetAssignedTechnique(slot) != technique) continue;
            bridge.UnassignTechnique(slot);
            ShowHint(bridge.LastAssignMessage);
            if (IsOpen) Rebuild();
            return;
        }
    }

    private void ShowHint(string message)
    {
        if (hintLabel == null) return;
        hintLabel.text = message;
        hintTimer = 4f;
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
