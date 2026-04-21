using UnityEngine;

public class InventoryDragDrop : MonoBehaviour
{
    private InventorySlotUI draggedSlot;

    private void OnEnable()
    {
        InventorySlotUI.OnBeginDragEvent += BeginDrag;
        InventorySlotUI.OnEndDragEvent += EndDrag;
        InventorySlotUI.OnDropEvent += Drop;
    }

    private void OnDisable()
    {
        InventorySlotUI.OnBeginDragEvent -= BeginDrag;
        InventorySlotUI.OnEndDragEvent -= EndDrag;
        InventorySlotUI.OnDropEvent -= Drop;
    }

    private void BeginDrag(InventorySlotUI slotUI)
    {
        draggedSlot = slotUI;
    }

    private void EndDrag(InventorySlotUI slotUI)
    {
        draggedSlot = null;
    }

    private void Drop(InventorySlotUI targetSlot)
    {
        // Эта логика теперь обрабатывается непосредственно в InventorySlotUI.OnEndDrag
        // Оставляем для совместимости с другими системами
    }
}