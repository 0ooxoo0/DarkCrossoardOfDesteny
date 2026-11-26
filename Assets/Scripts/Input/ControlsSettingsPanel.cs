using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Экран «Настройки Управления» внутри паузы.
/// Список действий и кнопки строятся в рантайме, поэтому панель
/// не зависит от ручной разметки сцены и работает с любым реестром действий.
/// </summary>
[DisallowMultipleComponent]
public sealed class ControlsSettingsPanel : MonoBehaviour
{
    private const float ActionRowHeight = 54f;
    private const float CategoryRowHeight = 30f;
    private const float HeaderRowHeight = 34f;

    private const float NameX = 14f;
    private const float NameWidth = 380f;
    private const float PrimaryX = 410f;
    private const float BindingWidth = 330f;
    private const float PrimaryClearX = 748f;
    private const float SecondaryX = 800f;
    private const float SecondaryClearX = 1138f;
    private const float ClearWidth = 36f;

    [Header("Оформление (можно оставить пустым)")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Color rowColor = new Color(0.11f, 0.09f, 0.07f, 0.92f);
    [SerializeField] private Color categoryColor = new Color(0.20f, 0.14f, 0.07f, 0.96f);
    [SerializeField] private Color buttonColor = new Color(0.25f, 0.19f, 0.11f, 1f);
    [SerializeField] private Color buttonEmptyColor = new Color(0.16f, 0.14f, 0.12f, 1f);
    [SerializeField] private Color waitingColor = new Color(0.64f, 0.40f, 0.06f, 1f);
    [SerializeField] private Color conflictColor = new Color(0.64f, 0.13f, 0.13f, 1f);
    [SerializeField] private Color textColor = new Color(0.95f, 0.91f, 0.82f, 1f);
    [SerializeField] private Color mutedTextColor = new Color(0.72f, 0.68f, 0.60f, 1f);

    private sealed class BindingRow
    {
        public string ActionId;
        public Image Background;
        public Button PrimaryButton;
        public Button SecondaryButton;
        public Button PrimaryClear;
        public Button SecondaryClear;
        public Image PrimaryImage;
        public Image SecondaryImage;
        public TMP_Text PrimaryLabel;
        public TMP_Text SecondaryLabel;
    }

    private readonly List<BindingRow> rows = new List<BindingRow>();

    private InputRemapManager manager;
    private RectTransform content;
    private TMP_Text statusText;
    private Image blocker;
    private bool built;
    private BindingRow conflictRow;
    private float conflictFlashUntil;

    // ------------------------------------------------------------------
    // Жизненный цикл
    // ------------------------------------------------------------------

    private void Awake()
    {
        manager = InputRemapManager.EnsureExists();
        ResolveFont();
    }

    private void OnEnable()
    {
        manager = InputRemapManager.EnsureExists();
        BuildIfNeeded();
        Subscribe();
        RefreshAll();

        if (blocker != null && (manager == null || !manager.IsRebindingActive))
            blocker.gameObject.SetActive(false);

        GameMenuController.Instance?.OpenPanel(gameObject);
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (manager != null)
        {
            manager.CancelRebind();
            manager.Save();
        }

        if (blocker != null) blocker.gameObject.SetActive(false);

        GameMenuController.Instance?.NotifyClosed(gameObject);

        // Если панель закрыли вместе с родителем (PauseMenu), не даём ей
        // снова появиться при следующем открытии паузы.
        if (gameObject.activeSelf && !gameObject.activeInHierarchy)
            gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!built) return;

        if (conflictRow != null && Time.unscaledTime >= conflictFlashUntil)
        {
            RefreshRow(conflictRow);
            conflictRow = null;
        }

        if (manager != null && manager.IsRebindingActive)
        {
            if (manager.HasPendingModifier)
            {
                string name = InputDisplayNames.ModifierName(manager.PendingModifier);
                SetStatus(name + " удерживается — нажмите клавишу или отпустите " + name + ".", false);
            }

            return;
        }

        // Если GameMenuController есть, Esc закрывает панель через его стек.
        // Fallback нужен для сцен/тестов без контроллера.
        if (GameMenuController.Instance != null) return;
        if (InputRemapManager.EscapeConsumedThisFrame) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            Close();
    }

    // ------------------------------------------------------------------
    // Построение UI
    // ------------------------------------------------------------------

    private void BuildIfNeeded()
    {
        if (built) return;

        ResolveFont();
        if (font == null)
        {
            Debug.LogError("[InputRemap] Не найден TMP-шрифт: экран управления не будет построен.");
            return;
        }

        Transform panel = transform.Find("Panel");
        if (panel == null)
        {
            var panelObject = new GameObject("Panel", typeof(RectTransform));
            panelObject.transform.SetParent(transform, false);
            panel = panelObject.transform;
        }

        Transform oldRoot = panel.Find("Remap Content");
        if (oldRoot != null) Destroy(oldRoot.gameObject);

        var rootObject = new GameObject("Remap Content", typeof(RectTransform));
        rootObject.transform.SetParent(panel, false);
        var root = (RectTransform)rootObject.transform;
        Stretch(root, 0f, 0f, 0f, 0f);

        BuildHint(root);
        BuildScroll(root);
        BuildBottomBar(root);
        BuildBlocker();

        built = true;
    }

    private void BuildHint(RectTransform root)
    {
        TMP_Text hint = CreateText(
            root,
            "Hint",
            "ЛКМ — назначить · ПКМ — очистить привязку · Esc — отмена",
            20f,
            TextAlignmentOptions.Left,
            mutedTextColor);

        RectTransform rect = hint.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -4f);
        rect.sizeDelta = new Vector2(-24f, 26f);
        hint.margin = new Vector4(12f, 0f, 0f, 0f);
    }

    private void BuildScroll(RectTransform root)
    {
        var scrollObject = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollObject.transform.SetParent(root, false);

        var scrollRect = (RectTransform)scrollObject.transform;
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(12f, 56f);
        scrollRect.offsetMax = new Vector2(-12f, -34f);

        var scrollImage = scrollObject.GetComponent<Image>();
        scrollImage.color = new Color(0f, 0f, 0f, 0.16f);
        scrollImage.raycastTarget = true;

        var scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportObject.transform.SetParent(scrollObject.transform, false);
        var viewport = (RectTransform)viewportObject.transform;
        Stretch(viewport, 0f, 0f, 0f, 0f);

        var viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = true;

        var contentObject = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));

        contentObject.transform.SetParent(viewportObject.transform, false);
        content = (RectTransform)contentObject.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;

        BuildHeader();
        BuildActionRows();
    }

    private void BuildHeader()
    {
        RectTransform header = CreateRowContainer(content, "Header", HeaderRowHeight, null);
        AddCellLabel(header, "Действие", NameX, NameWidth, textColor, true);
        AddCellLabel(header, "Основная", PrimaryX, BindingWidth, textColor, true);
        AddCellLabel(header, "Дополнительная", SecondaryX, BindingWidth, textColor, true);
    }

    private void BuildActionRows()
    {
        string lastCategory = null;

        for (int i = 0; i < manager.Actions.Count; i++)
        {
            InputActionDefinition definition = manager.Actions[i];
            if (definition == null || !definition.rebindable) continue;

            if (!string.Equals(lastCategory, definition.category, StringComparison.Ordinal))
            {
                lastCategory = definition.category;
                BuildCategoryRow(lastCategory);
            }

            BuildActionRow(definition);
        }
    }

    private void BuildCategoryRow(string category)
    {
        RectTransform row = CreateRowContainer(content, "Category " + category, CategoryRowHeight, categoryColor);
        TMP_Text label = AddCellLabel(row, category.ToUpperInvariant(), 10f, 600f, textColor, true);
        label.fontSize = 18f;
        label.margin = new Vector4(10f, 0f, 0f, 0f);
    }

    private void BuildActionRow(InputActionDefinition definition)
    {
        RectTransform row = CreateRowContainer(content, definition.id, ActionRowHeight, rowColor);

        AddCellLabel(row, definition.displayName, NameX, NameWidth, textColor, false);

        Button primaryButton = CreateBindingButton(
            row, definition.id, InputBindingSlot.Primary, PrimaryX, out TMP_Text primaryLabel, out Image primaryImage);

        Button primaryClear = CreateClearButton(
            row, PrimaryClearX, () => RequestUnbind(definition.id, InputBindingSlot.Primary));

        Button secondaryButton = CreateBindingButton(
            row, definition.id, InputBindingSlot.Secondary, SecondaryX, out TMP_Text secondaryLabel, out Image secondaryImage);

        Button secondaryClear = CreateClearButton(
            row, SecondaryClearX, () => RequestUnbind(definition.id, InputBindingSlot.Secondary));

        rows.Add(new BindingRow
        {
            ActionId = definition.id,
            Background = row.GetComponent<Image>(),
            PrimaryButton = primaryButton,
            SecondaryButton = secondaryButton,
            PrimaryClear = primaryClear,
            SecondaryClear = secondaryClear,
            PrimaryImage = primaryImage,
            SecondaryImage = secondaryImage,
            PrimaryLabel = primaryLabel,
            SecondaryLabel = secondaryLabel
        });
    }

    private void BuildBottomBar(RectTransform root)
    {
        statusText = CreateText(root, "Status", string.Empty, 20f, TextAlignmentOptions.Left, textColor);
        RectTransform statusRect = statusText.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(0.72f, 0f);
        statusRect.pivot = new Vector2(0f, 0f);
        statusRect.anchoredPosition = new Vector2(16f, 10f);
        statusRect.sizeDelta = new Vector2(0f, 40f);
        statusText.margin = new Vector4(4f, 0f, 0f, 0f);

        Button resetButton = CreateTextButton(root, "Reset All", "Сбросить всё", 250f, 44f);
        RectTransform resetRect = (RectTransform)resetButton.transform;
        resetRect.anchorMin = new Vector2(1f, 0f);
        resetRect.anchorMax = new Vector2(1f, 0f);
        resetRect.pivot = new Vector2(1f, 0f);
        resetRect.anchoredPosition = new Vector2(-16f, 8f);
        resetRect.sizeDelta = new Vector2(250f, 44f);
        resetButton.onClick.AddListener(OnResetAll);
    }

    private void BuildBlocker()
    {
        Transform oldBlocker = transform.Find("Rebind Blocker");
        if (oldBlocker != null) Destroy(oldBlocker.gameObject);

        var blockerObject = new GameObject("Rebind Blocker", typeof(RectTransform), typeof(Image));
        blockerObject.transform.SetParent(transform, false);

        var blockerRect = (RectTransform)blockerObject.transform;
        Stretch(blockerRect, 0f, 0f, 0f, 0f);

        blocker = blockerObject.GetComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.001f);
        blocker.raycastTarget = true;

        blockerObject.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Обновление
    // ------------------------------------------------------------------

    private void RefreshAll()
    {
        if (!built) return;

        for (int i = 0; i < rows.Count; i++)
            RefreshRow(rows[i]);
    }

    private void RefreshRow(BindingRow row)
    {
        if (row == null || manager == null) return;

        InputActionDefinition definition = manager.GetDefinition(row.ActionId);
        if (definition == null) return;

        bool rebinding = manager.IsRebindingActive && manager.RebindActionId == row.ActionId;
        bool primaryWaiting = rebinding && manager.RebindSlot == InputBindingSlot.Primary;
        bool secondaryWaiting = rebinding && manager.RebindSlot == InputBindingSlot.Secondary;

        InputChord primary = manager.GetBinding(row.ActionId, InputBindingSlot.Primary);
        InputChord secondary = manager.GetBinding(row.ActionId, InputBindingSlot.Secondary);

        row.PrimaryLabel.text = primaryWaiting ? "Нажмите клавишу…" : InputDisplayNames.Get(primary);
        row.SecondaryLabel.text = !definition.allowSecondary
            ? "—"
            : (secondaryWaiting ? "Нажмите клавишу…" : InputDisplayNames.Get(secondary));

        row.PrimaryImage.color = primaryWaiting
            ? waitingColor
            : (primary.IsEmpty ? buttonEmptyColor : buttonColor);

        row.SecondaryImage.color = secondaryWaiting
            ? waitingColor
            : (secondary.IsEmpty ? buttonEmptyColor : buttonColor);

        row.SecondaryButton.interactable = definition.allowSecondary;
        row.PrimaryClear.interactable = definition.allowUnbind && !primary.IsEmpty;
        row.SecondaryClear.interactable = definition.allowSecondary && definition.allowUnbind && !secondary.IsEmpty;

        if (row.Background != null) row.Background.color = rowColor;
    }

    private void SetStatus(string message, bool isError)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = isError ? conflictColor : textColor;
    }

    private BindingRow FindRow(string actionId)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].ActionId == actionId) return rows[i];
        }

        return null;
    }

    // ------------------------------------------------------------------
    // Действия UI
    // ------------------------------------------------------------------

    public void RequestRebind(string actionId, InputBindingSlot slot)
    {
        if (manager == null) return;
        manager.StartRebind(actionId, slot);
    }

    public void RequestUnbind(string actionId, InputBindingSlot slot)
    {
        if (manager == null) return;

        if (manager.IsRebindingActive) manager.CancelRebind();
        if (manager.Unbind(actionId, slot))
            SetStatus("Привязка очищена.", false);
    }

    private void OnResetAll()
    {
        if (manager == null) return;
        manager.ResetToDefaults();
    }

    /// <summary>Закрывает панель. Вызывается кнопкой «Назад» и GameMenuController.</summary>
    public void Close()
    {
        if (manager != null)
        {
            manager.CancelRebind();
            manager.Save();
        }

        gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------
    // События менеджера
    // ------------------------------------------------------------------

    private void Subscribe()
    {
        if (manager == null) return;

        manager.BindingChanged += OnBindingChanged;
        manager.BindingsReset += OnBindingsReset;
        manager.RebindStarted += OnRebindStarted;
        manager.RebindCompleted += OnRebindCompleted;
        manager.RebindCanceled += OnRebindCanceled;
        manager.RebindConflict += OnRebindConflict;
        manager.RebindFailed += OnRebindFailed;
    }

    private void Unsubscribe()
    {
        if (manager == null) return;

        manager.BindingChanged -= OnBindingChanged;
        manager.BindingsReset -= OnBindingsReset;
        manager.RebindStarted -= OnRebindStarted;
        manager.RebindCompleted -= OnRebindCompleted;
        manager.RebindCanceled -= OnRebindCanceled;
        manager.RebindConflict -= OnRebindConflict;
        manager.RebindFailed -= OnRebindFailed;
    }

    private void OnBindingChanged(string actionId, InputBindingSlot slot)
    {
        BindingRow row = FindRow(actionId);
        if (row != null) RefreshRow(row);
    }

    private void OnBindingsReset()
    {
        RefreshAll();
        SetStatus("Настройки управления сброшены.", false);
    }

    private void OnRebindStarted(string actionId, InputBindingSlot slot)
    {
        if (blocker != null)
        {
            blocker.transform.SetAsLastSibling();
            blocker.gameObject.SetActive(true);
        }

        BindingRow row = FindRow(actionId);
        if (row != null) RefreshRow(row);

        InputActionDefinition definition = manager != null ? manager.GetDefinition(actionId) : null;
        string name = definition != null ? definition.displayName : actionId;
        SetStatus("Нажмите клавишу для «" + name + "». Esc — отмена.", false);
    }

    private void OnRebindCompleted(string actionId, InputBindingSlot slot, string note)
    {
        if (blocker != null) blocker.gameObject.SetActive(false);

        BindingRow row = FindRow(actionId);
        if (row != null) RefreshRow(row);

        SetStatus(string.IsNullOrEmpty(note) ? "Готово." : note, false);
    }

    private void OnRebindCanceled()
    {
        if (blocker != null) blocker.gameObject.SetActive(false);
        RefreshAll();
        SetStatus("Переназначение отменено.", false);
    }

    private void OnRebindConflict(string actionId, InputBindingSlot slot, string message)
    {
        BindingRow row = FindRow(actionId);
        if (row != null)
        {
            conflictRow = row;
            conflictFlashUntil = Time.unscaledTime + 0.7f;
            if (row.Background != null) row.Background.color = conflictColor;
        }

        SetStatus(message, true);
    }

    private void OnRebindFailed(string message)
    {
        if (blocker != null) blocker.gameObject.SetActive(false);
        SetStatus(message, true);
    }

    // ------------------------------------------------------------------
    // Фабрика UI
    // ------------------------------------------------------------------

    private void ResolveFont()
    {
        if (font != null) return;

        TMP_Text existing = GetComponentInChildren<TMP_Text>(true);
        if (existing != null && existing.font != null)
        {
            font = existing.font;
            return;
        }

        font = TMP_Settings.defaultFontAsset;
    }

    private RectTransform CreateRowContainer(Transform parent, string name, float height, Color? background)
    {
        var rowObject = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        rowObject.transform.SetParent(parent, false);

        var layoutElement = rowObject.GetComponent<LayoutElement>();
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;
        layoutElement.flexibleHeight = 0f;

        if (background.HasValue)
        {
            var image = rowObject.AddComponent<Image>();
            image.color = background.Value;
            image.raycastTarget = false;
        }

        return (RectTransform)rowObject.transform;
    }

    private TMP_Text AddCellLabel(RectTransform row, string value, float x, float width, Color color, bool bold)
    {
        TMP_Text label = CreateText(row, "Label", value, 20f, TextAlignmentOptions.Left, color);

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, -8f);
        label.margin = new Vector4(8f, 0f, 0f, 0f);

        if (bold) label.fontStyle = FontStyles.Bold;

        return label;
    }

    private Button CreateBindingButton(
        RectTransform row,
        string actionId,
        InputBindingSlot slot,
        float x,
        out TMP_Text label,
        out Image image)
    {
        var buttonObject = new GameObject(
            "Bind " + slot,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));

        buttonObject.transform.SetParent(row, false);

        var rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(BindingWidth, -10f);

        image = buttonObject.GetComponent<Image>();
        image.color = buttonColor;

        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.92f, 0.76f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        label = CreateText(buttonObject.transform, "Label", string.Empty, 20f, TextAlignmentOptions.Center, textColor);
        Stretch(label.rectTransform, 6f, 2f, 6f, 2f);

        var slotButton = buttonObject.AddComponent<BindingSlotButton>();
        slotButton.Initialize(this, actionId, slot);

        string capturedAction = actionId;
        InputBindingSlot capturedSlot = slot;
        button.onClick.AddListener(() => RequestRebind(capturedAction, capturedSlot));

        return button;
    }

    private Button CreateClearButton(RectTransform row, float x, UnityEngine.Events.UnityAction onClick)
    {
        var buttonObject = new GameObject("Clear", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(row, false);

        var rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(ClearWidth, -16f);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.34f, 0.11f, 0.11f, 0.95f);

        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text label = CreateText(buttonObject.transform, "Label", "×", 24f, TextAlignmentOptions.Center, textColor);
        Stretch(label.rectTransform, 0f, 0f, 0f, 0f);

        button.onClick.AddListener(onClick);
        return button;
    }

    private Button CreateTextButton(Transform parent, string name, string text, float width, float height)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.32f, 0.24f, 0.13f, 1f);

        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text label = CreateText(buttonObject.transform, "Label", text, 20f, TextAlignmentOptions.Center, textColor);
        Stretch(label.rectTransform, 4f, 2f, 4f, 2f);

        var rect = (RectTransform)buttonObject.transform;
        rect.sizeDelta = new Vector2(width, height);

        return button;
    }

    private TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        TextAlignmentOptions alignment,
        Color color)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.text = value;

        return text;
    }

    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
