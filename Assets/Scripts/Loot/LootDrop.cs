/// <summary>
/// Результат ролла таблицы лута: конкретный предмет и сколько его выпало.
/// </summary>
public struct LootDrop
{
    public ItemData Item;
    public int Quantity;

    public LootDrop(ItemData item, int quantity)
    {
        Item = item;
        Quantity = quantity;
    }
}
