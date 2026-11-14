using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Один слот сетки инвентаря: иконка, количество, подсветка выбора,
/// тултип по наведению и участие в drag&amp;drop.
/// </summary>
public class InventorySlotUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Визуал")]
    [SerializeField] private Image icon;
    [SerializeField] private Image background;
    [SerializeField] private Image border;
    [SerializeField] private Image highlight;
    [SerializeField] private Image placeholder;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private TextMeshProUGUI placeholderText;

    [Header("Связи")]
    [SerializeField] private InventoryDragController dragController;
    [SerializeField] private InventoryTooltipUI tooltip;
    [SerializeField] private InventoryPanelUI panel;
    [SerializeField] private InventoryTheme theme;

    private Inventory inventory;
    private InventorySlot currentSlot;
    private int slotIndex;
    private bool selected;
    private CanvasGroup canvasGroup;

    /// <summary>Индекс слота в сетке.</summary>
    public int SlotIndex => slotIndex;

    /// <summary>Текущие данные слота.</summary>
    public InventorySlot Slot => currentSlot;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (dragController == null) dragController = FindFirstObjectByType<InventoryDragController>();
        if (tooltip == null) tooltip = FindFirstObjectByType<InventoryTooltipUI>(FindObjectsInactive.Include);
        if (panel == null) panel = GetComponentInParent<InventoryPanelUI>();
    }

    /// <summary>Привязывает общие UI-сервисы панели.</summary>
    public void Setup(InventoryDragController drag, InventoryTooltipUI tooltipUi, InventoryPanelUI owner,
        InventoryTheme inventoryTheme)
    {
        dragController = drag;
        tooltip = tooltipUi;
        panel = owner;
        theme = inventoryTheme;
        ApplyTheme();
    }

    /// <summary>Инициализирует слот индексом и инвентарём.</summary>
    public void Initialize(int index, Inventory inv)
    {
        slotIndex = index;
        inventory = inv;
        UpdateSlot(inventory != null ? inventory.GetSlot(index) : null);
        ApplyTheme();
    }

    /// <summary>Перерисовывает слот по данным инвентаря.</summary>
    public void UpdateSlot(InventorySlot slot)
    {
        currentSlot = slot;

        ItemData item = slot != null && !slot.IsEmpty ? slot.itemData : null;
        bool hasItem = item != null;
        bool hasIcon = hasItem && item.icon != null;

        if (icon != null)
        {
            icon.enabled = hasIcon;
            if (hasIcon)
            {
                icon.sprite = item.icon;
                icon.color = Color.white;
            }
        }

        if (placeholder != null)
        {
            placeholder.enabled = hasItem && !hasIcon;
            placeholder.color = theme != null ? theme.placeholderColor : new Color(0.40f, 0.24f, 0.24f, 1f);
        }

        if (placeholderText != null)
        {
            bool showPlaceholder = hasItem && !hasIcon;
            placeholderText.enabled = showPlaceholder;
            placeholderText.text = showPlaceholder ? "[PLACEHOLDER]" : string.Empty;
        }

        if (quantityText != null)
        {
            bool showQuantity = hasItem && slot.quantity > 1;
            quantityText.enabled = showQuantity;
            quantityText.text = showQuantity ? slot.quantity.ToString() : string.Empty;
        }

        ApplySelectionVisual();
    }

    /// <summary>Включает или выключает подсветку выбранного слота.</summary>
    public void SetSelected(bool value)
    {
        selected = value;
        ApplySelectionVisual();
    }

    /// <summary>Есть ли в слоте предмет.</summary>
    public bool HasItem()
    {
        return currentSlot != null && !currentSlot.IsEmpty;
    }

    /// <summary>Слот по текущему состоянию (совместимость с WeaponSlotUI).</summary>
    public InventorySlot GetSlot() => currentSlot;

    /// <summary>Инвентарь, к которому относится слот.</summary>
    public Inventory GetInventory() => inventory;

    /// <summary>Индекс слота (совместимость с WeaponSlotUI).</summary>
    public int GetSlotIndex() => slotIndex;

    /// <summary>Имя предмета или пустая строка.</summary>
    public string GetItemName() => currentSlot?.itemData != null ? currentSlot.itemData.DisplayName : string.Empty;

    /// <summary>Описание предмета или пустая строка.</summary>
    public string GetItemDescription() => currentSlot?.itemData != null ? currentSlot.itemData.description : string.Empty;

    /// <summary>Начинает перетаскивание, если слот не пуст.</summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!HasItem() || dragController == null) return;

        if (canvasGroup != null) canvasGroup.alpha = 0.45f;
        tooltip?.Hide();
        dragController.BeginDragFromSlot(this);
    }

    /// <summary>Двигает иконку перетаскивания за курсором.</summary>
    public void OnDrag(PointerEventData eventData)
    {
        dragController?.UpdateDrag(eventData.position);
    }

    /// <summary>Завершает перетаскивание. Если дроп не случился, предмет остаётся в слоте.</summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        dragController?.EndDrag();
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    /// <summary>Принимает перетаскиваемый предмет.</summary>
    public void OnDrop(PointerEventData eventData)
    {
        dragController?.DropOnSlot(this);
    }

    /// <summary>Показывает тултип с названием, типом, описанием и ценой.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!HasItem()) return;
        tooltip?.Show(currentSlot.itemData, eventData.position);
    }

    /// <summary>Скрывает тултип.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        tooltip?.Hide();
    }

    /// <summary>Левый клик выделяет слот, двойной — применяет предмет.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (eventData.dragging) return;

        panel?.SelectSlot(this);

        if (eventData.clickCount >= 2 && inventory != null)
        {
            inventory.RequestUse(slotIndex);
            tooltip?.Hide();
        }
    }

    private void ApplySelectionVisual()
    {
        if (highlight != null)
        {
            highlight.enabled = selected;
            highlight.color = theme != null ? theme.highlightColor : new Color(0.79f, 0.65f, 0.36f, 1f);
        }

        if (border != null)
        {
            border.color = selected
                ? (theme != null ? theme.highlightColor : Color.yellow)
                : (theme != null ? theme.borderColor : Color.gray);
        }
    }

    private void ApplyTheme()
    {
        if (theme == null) return;

        if (background != null) background.color = theme.slotBackground;
        if (quantityText != null) quantityText.color = theme.textColor;
        if (placeholderText != null) placeholderText.color = theme.textColor;

        TMP_FontAsset font = theme.ResolveFont();
        if (font == null) return;

        if (quantityText != null) quantityText.font = font;
        if (placeholderText != null) placeholderText.font = font;

        ApplySelectionVisual();
    }
}
