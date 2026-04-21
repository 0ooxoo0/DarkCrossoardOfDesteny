[System.Serializable]
public class InventorySlot
{
    public ItemData itemData;
    public int quantity;

    public InventorySlot(ItemData item, int count)
    {
        itemData = item;
        quantity = count;
    }

    public bool CanAddItem(ItemData item)
    {
        return itemData == null || (itemData == item && quantity < item.maxStackSize);
    }

    public void AddItem(int count = 1)
    {
        quantity += count;
    }

    public void RemoveItem(int count = 1)
    {
        quantity -= count;
        if (quantity <= 0)
        {
            ClearSlot();
        }
    }

    public void ClearSlot()
    {
        itemData = null;
        quantity = 0;
    }
}