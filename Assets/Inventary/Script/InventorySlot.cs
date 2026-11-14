using UnityEngine;

/// <summary>
/// Один слот инвентаря: предмет и количество.
/// Класс сериализуемый, но в сцене не хранится — слоты создаёт <see cref="Inventory"/>.
/// </summary>
[System.Serializable]
public class InventorySlot
{
    public ItemData itemData;
    public int quantity;

    /// <summary>Создаёт слот с предметом и количеством. Для пустого слота передайте null и 0.</summary>
    public InventorySlot(ItemData item, int count)
    {
        itemData = item;
        quantity = count;
    }

    /// <summary>Слот пуст, если нет предмета или закончилось количество.</summary>
    public bool IsEmpty => itemData == null || quantity <= 0;

    /// <summary>Максимум, который влезает в этот слот для указанного предмета.</summary>
    public int MaxStack => itemData != null ? itemData.MaxStack : 1;

    /// <summary>Сколько ещё единиц предмета поместится в слот.</summary>
    public int SpaceLeft => IsEmpty ? 0 : Mathf.Max(0, MaxStack - quantity);

    /// <summary>Можно ли доложить предмет в этот слот: тот же предмет и есть место в стаке.</summary>
    public bool CanStackWith(ItemData item)
    {
        return item != null
               && !IsEmpty
               && itemData == item
               && item.stackable
               && quantity < item.MaxStack;
    }

    /// <summary>Можно ли положить предмет в слот целиком (без учёта соседних слотов).</summary>
    public bool CanAddItem(ItemData item)
    {
        return item != null && (IsEmpty || CanStackWith(item));
    }

    /// <summary>Добавляет количество без проверки переполнения — проверку делает Inventory.</summary>
    public void AddItem(int count = 1)
    {
        if (count <= 0) return;
        quantity += count;
    }

    /// <summary>Убирает количество; когда оно кончилось, слот очищается.</summary>
    public void RemoveItem(int count = 1)
    {
        if (count <= 0) return;
        quantity -= count;
        if (quantity <= 0) ClearSlot();
    }

    /// <summary>Полностью очищает слот.</summary>
    public void ClearSlot()
    {
        itemData = null;
        quantity = 0;
    }

    /// <summary>Копия слота. Нужна, чтобы обмен не делил один объект между двумя ячейками.</summary>
    public InventorySlot Clone()
    {
        return new InventorySlot(itemData, quantity);
    }
}
