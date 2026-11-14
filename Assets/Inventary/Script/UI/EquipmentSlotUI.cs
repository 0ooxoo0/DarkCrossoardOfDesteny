using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Слот экипировки. Сейчас реально работает Weapon; остальные типы
/// поддерживаются той же логикой и включатся, когда появятся предметы брони.
/// </summary>
public class EquipmentSlotUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Тип слота")]
    [SerializeField] private EquipmentSlotType slotType = EquipmentSlotType.Weapon;

    [Header("Визуал")]
    [SerializeField] private Image icon;
    [SerializeField] private Image background;
    [SerializeField] private Image border;
    [SerializeField] private Image placeholder;
    [SerializeField] private TextMeshProUGUI typeLabel;

    [Header("Связи")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private InventoryDragController dragController;
    [SerializeField] private InventoryTooltipUI tooltip;
    [SerializeField] private InventoryTheme theme;

    [Header("Интеграция с оружием (опционально)")]
    [Tooltip("Если назначен, надетое оружие сразу попадает в руки через систему боя.")]
    [SerializeField] private WeaponManager weaponManager;

    private CanvasGroup canvasGroup;

    /// <summary>Тип этого слота.</summary>
    public EquipmentSlotType SlotType => slotType;

    /// <summary>Надетый предмет или null.</summary>
    public InventorySlot EquippedSlot => inventory != null ? inventory.GetEquipped(slotType) : null;

    /// <summary>Инвентарь, к которому привязан слот.</summary>
    public Inventory GetInventory() => inventory;

    private void Awake()
    {
        if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        Subscribe();
        if (typeLabel != null) typeLabel.text = EquipmentSlotInfo.GetDisplayName(slotType);
        ApplyTheme();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    /// <summary>Привязывает слот к инвентарю и общим UI-сервисам.</summary>
    public void Setup(Inventory targetInventory, InventoryDragController drag, InventoryTooltipUI tooltipUi,
        InventoryTheme inventoryTheme, WeaponManager weapon)
    {
        Unsubscribe();

        inventory = targetInventory;
        dragController = drag;
        tooltip = tooltipUi;
        theme = inventoryTheme;
        weaponManager = weapon;

        Subscribe();
        ApplyTheme();
        Refresh();
    }

    /// <summary>Перерисовывает иконку и подпись по текущему состоянию инвентаря.</summary>
    public void Refresh()
    {
        InventorySlot slot = EquippedSlot;
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

        if (placeholder != null) placeholder.enabled = hasItem && !hasIcon;

        if (border != null && theme != null)
        {
            border.color = hasItem ? theme.highlightColor : theme.borderColor;
        }

        if (background != null && theme != null)
        {
            background.color = theme.slotBackground;
        }

        if (typeLabel != null)
        {
            typeLabel.color = theme != null ? theme.mutedTextColor : Color.white;
        }
    }

    /// <summary>Начинает drag, если в слоте есть предмет.</summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        InventorySlot slot = EquippedSlot;
        if (slot == null || slot.IsEmpty || dragController == null) return;

        dragController.BeginDragFromEquipment(this);
    }

    /// <summary>Двигает иконку перетаскивания.</summary>
    public void OnDrag(PointerEventData eventData)
    {
        dragController?.UpdateDrag(eventData.position);
    }

    /// <summary>Завершает drag и возвращает слоту обычную прозрачность.</summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        dragController?.EndDrag();
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    /// <summary>Принимает предмет из сетки.</summary>
    public void OnDrop(PointerEventData eventData)
    {
        dragController?.DropOnEquipment(this);
    }

    /// <summary>Показывает тултип надетого предмета.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        InventorySlot slot = EquippedSlot;
        if (slot == null || slot.IsEmpty) return;
        tooltip?.Show(slot.itemData, eventData.position);
    }

    /// <summary>Скрывает тултип.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        tooltip?.Hide();
    }

    /// <summary>Двойной клик снимает предмет в первый свободный слот.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (eventData.dragging || eventData.clickCount < 2) return;

        InventorySlot slot = EquippedSlot;
        if (slot == null || slot.IsEmpty || inventory == null) return;

        inventory.Unequip(slotType);
        tooltip?.Hide();
    }

    private void Subscribe()
    {
        if (inventory == null) return;

        inventory.OnInventoryChanged -= HandleInventoryChanged;
        inventory.OnInventoryChanged += HandleInventoryChanged;

        inventory.OnItemEquipped -= HandleEquipped;
        inventory.OnItemEquipped += HandleEquipped;

        inventory.OnItemUnequipped -= HandleUnequipped;
        inventory.OnItemUnequipped += HandleUnequipped;
    }

    private void Unsubscribe()
    {
        if (inventory == null) return;

        inventory.OnInventoryChanged -= HandleInventoryChanged;
        inventory.OnItemEquipped -= HandleEquipped;
        inventory.OnItemUnequipped -= HandleUnequipped;
    }

    private void HandleInventoryChanged(Inventory changed)
    {
        Refresh();
    }

    private void HandleEquipped(ItemData item, EquipmentSlotType equippedType)
    {
        if (equippedType != slotType) return;

        Refresh();

        // UI инвентаря — единственное место, которое знает про систему боя.
        // Сам Inventory об оружии ничего не знает.
        if (slotType == EquipmentSlotType.Weapon && weaponManager != null && item != null && item.weaponData != null)
        {
            weaponManager.EquipWeapon(item.weaponData);
        }
    }

    private void HandleUnequipped(ItemData item, EquipmentSlotType unequippedType)
    {
        if (unequippedType == slotType) Refresh();
    }

    private void ApplyTheme()
    {
        if (theme == null) return;

        if (icon != null) icon.raycastTarget = false;
        if (placeholder != null)
        {
            placeholder.color = theme.placeholderColor;
            placeholder.raycastTarget = false;
        }
    }
}
