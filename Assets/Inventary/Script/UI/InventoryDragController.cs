using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ведёт перетаскивание между слотами сетки и экипировкой.
/// Источник и приёмник сами сообщают о начале и конце drag, поэтому
/// никакого ручного RaycastAll по всему экрану не нужно.
/// </summary>
public class InventoryDragController : MonoBehaviour
{
    [Header("Визуал")]
    [SerializeField] private RectTransform dragLayer;
    [SerializeField] private Image dragIcon;

    [Header("Связи")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private InventoryTooltipUI tooltip;
    [SerializeField] private InventoryTheme theme;

    private InventorySlotUI slotSource;
    private EquipmentSlotUI equipmentSource;
    private Sprite defaultSprite;
    private Color defaultColor = Color.white;

    /// <summary>Идёт ли сейчас перетаскивание.</summary>
    public bool IsDragging => slotSource != null || equipmentSource != null;

    /// <summary>Слот сетки, из которого тянут предмет (или null).</summary>
    public InventorySlotUI SlotSource => slotSource;

    /// <summary>Слот экипировки, из которого тянут предмет (или null).</summary>
    public EquipmentSlotUI EquipmentSource => equipmentSource;

    private void Awake()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (dragIcon != null)
        {
            defaultSprite = dragIcon.sprite;
            defaultColor = dragIcon.color;
            dragIcon.raycastTarget = false;
        }
        HideIcon();
    }

    /// <summary>Начинает перетаскивание предмета из слота сетки.</summary>
    public void BeginDragFromSlot(InventorySlotUI source)
    {
        if (source == null) return;
        InventorySlot slot = source.GetSlot();
        if (slot == null || slot.IsEmpty) return;

        ClearSources();
        slotSource = source;
        ShowIcon(slot.itemData);
    }

    /// <summary>Начинает перетаскивание надетого предмета.</summary>
    public void BeginDragFromEquipment(EquipmentSlotUI source)
    {
        if (source == null) return;
        InventorySlot slot = source.EquippedSlot;
        if (slot == null || slot.IsEmpty) return;

        ClearSources();
        equipmentSource = source;
        ShowIcon(slot.itemData);
    }

    /// <summary>Двигает иконку за курсором.</summary>
    public void UpdateDrag(Vector2 screenPosition)
    {
        if (dragIcon == null) return;

        RectTransform parent = dragLayer != null ? dragLayer : dragIcon.rectTransform.parent as RectTransform;
        if (parent == null)
        {
            dragIcon.transform.position = screenPosition;
            return;
        }

        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, camera, out Vector2 local))
        {
            dragIcon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            dragIcon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            dragIcon.rectTransform.anchoredPosition = local;
        }
    }

    /// <summary>Завершает перетаскивание. Если дроп не случился, предмет остаётся на месте.</summary>
    public void EndDrag()
    {
        ClearSources();
        HideIcon();
    }

    /// <summary>Приёмник-слот сетки сообщает о дропе.</summary>
    public void DropOnSlot(InventorySlotUI target)
    {
        if (target == null) return;

        if (slotSource != null)
        {
            if (slotSource == target) return;

            Inventory inventory = slotSource.GetInventory();
            if (inventory == null) return;

            inventory.SwapSlots(slotSource.GetSlotIndex(), target.GetSlotIndex());
        }
        else if (equipmentSource != null)
        {
            Inventory inventory = equipmentSource.GetInventory();
            inventory?.TryMoveEquippedToSlot(equipmentSource.SlotType, target.GetSlotIndex());
        }
    }

    /// <summary>Слот экипировки сообщает о дропе.</summary>
    public void DropOnEquipment(EquipmentSlotUI target)
    {
        if (target == null) return;

        if (slotSource == null) return;

        Inventory inventory = slotSource.GetInventory();
        inventory?.TryEquip(slotSource.GetSlotIndex(), target.SlotType);
    }

    private void ShowIcon(ItemData item)
    {
        if (dragIcon == null) return;

        Sprite icon = item != null ? item.icon : null;
        if (icon != null)
        {
            dragIcon.sprite = icon;
            dragIcon.color = Color.white;
        }
        else
        {
            // [PLACEHOLDER] — у предмета нет иконки: показываем цветной квадрат.
            dragIcon.sprite = defaultSprite;
            dragIcon.color = theme != null ? theme.placeholderColor : defaultColor;
        }

        dragIcon.enabled = true;
        dragIcon.transform.SetAsLastSibling();
        if (tooltip != null) tooltip.Hide();
    }

    private void HideIcon()
    {
        if (dragIcon != null) dragIcon.enabled = false;
    }

    private void ClearSources()
    {
        slotSource = null;
        equipmentSource = null;
    }
}
