using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [Header("Inventory Settings")]
    [SerializeField] private int width = 5;
    [SerializeField] private int height = 4;
    [SerializeField] private TextMeshProUGUI DescriptText;

    private List<InventorySlot> slots = new List<InventorySlot>();
    public event Action<Inventory> OnInventoryChanged;

    private void Awake()
    {
        InitializeSlots();
    }

    private void InitializeSlots()
    {
        slots.Clear();
        for (int i = 0; i < width * height; i++)
        {
            slots.Add(new InventorySlot(null, 0));
        }
    }

    public bool AddItem(ItemData item, int count = 1)
    {
        if (item == null) return false;

        bool success = InternalAddItem(item, count);
        if (success)
        {
            OnInventoryChanged?.Invoke(this);
        }
        return success;
    }

    // Новый метод для тестирования
    public void AddTestItem(ItemData item)
    {
        AddItem(item);
    }

    private bool InternalAddItem(ItemData item, int count)
    {
        // Попробуем добавить в существующий стек
        if (item.stackable)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].itemData == item && slots[i].quantity < item.maxStackSize)
                {
                    int spaceLeft = item.maxStackSize - slots[i].quantity;
                    int addAmount = Mathf.Min(spaceLeft, count);
                    slots[i].AddItem(addAmount);
                    count -= addAmount;

                    if (count <= 0) return true;
                }
            }
        }

        // Добавляем в пустые слоты
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].itemData == null)
            {
                int addAmount = Mathf.Min(count, item.maxStackSize);
                slots[i] = new InventorySlot(item, addAmount);
                slots[i].itemData.description = item.description; 
                count -= addAmount;

                if (count <= 0) return true;
            }
        }
        DescriptText.text = "В инвентаре более нет места";
        Debug.LogWarning("Not enough space in inventory");
        return false;
    }

    public void RemoveItem(int slotIndex, int count = 1)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count) return;

        slots[slotIndex].RemoveItem(count);

        OnInventoryChanged?.Invoke(this);
    }

    public void ClearAllSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i] = new InventorySlot(null, 0);
        }
        OnInventoryChanged?.Invoke(this);
    }

    // Метод сортировки по типу и имени
    public void SortByTypeAndName()
    {
        // Собираем все непустые слоты
        var nonEmptySlots = slots.Where(slot => slot.itemData != null).ToList();

        // Собираем все пустые слоты
        var emptySlots = slots.Where(slot => slot.itemData == null).ToList();

        // Сортируем непустые слоты: сначала по типу, затем по имени
        nonEmptySlots.Sort((a, b) =>
        {
            // Сначала сравниваем по типу
            int typeComparison = a.itemData.type.CompareTo(b.itemData.type);
            if (typeComparison != 0)
                return typeComparison;

            // Если типы одинаковые, сравниваем по имени
            return string.Compare(a.itemData.itemName, b.itemData.itemName, StringComparison.Ordinal);
        });

        // Объединяем отсортированные непустые слоты с пустыми
        slots.Clear();
        slots.AddRange(nonEmptySlots);
        slots.AddRange(emptySlots);

        // Уведомляем об изменении инвентаря
        OnInventoryChanged?.Invoke(this);

        Debug.Log("Inventory sorted by type and name");
    }


    public void RemoveLastItem()
    {
            bool success = RemoveLastItemPrivate();
            if (success)
            {
                Debug.Log("Last item removed successfully");
            }
            else
            {
                Debug.Log("No items to remove");
            }
    }
    // Метод для удаления последнего элемента
    private bool RemoveLastItemPrivate()
    {
        // Ищем последний непустой слот с конца
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i].itemData != null && slots[i].quantity > 0)
            {
                Debug.Log($"Removing last item: {slots[i].itemData.itemName} from slot {i}");
                RemoveItem(i, 1);
                return true;
            }
        }

        Debug.Log("No items found to remove");
        return false;
    }

    public void MoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || fromIndex >= slots.Count || toIndex < 0 || toIndex >= slots.Count) return;

        InventorySlot fromSlot = slots[fromIndex];
        InventorySlot toSlot = slots[toIndex];

        // Если оба слота содержат одинаковые стакаемые предметы
        if (fromSlot.itemData != null && toSlot.itemData != null &&
            fromSlot.itemData == toSlot.itemData && fromSlot.itemData.stackable)
        {
            int total = fromSlot.quantity + toSlot.quantity;
            int maxStack = fromSlot.itemData.maxStackSize;

            if (total <= maxStack)
            {
                toSlot.quantity = total;
                fromSlot.ClearSlot();
            }
            else
            {
                toSlot.quantity = maxStack;
                fromSlot.quantity = total - maxStack;
            }
        }
        else
        {
            // Просто меняем слоты местами
            InventorySlot temp = new InventorySlot(fromSlot.itemData, fromSlot.quantity);
            slots[fromIndex] = new InventorySlot(toSlot.itemData, toSlot.quantity);
            slots[toIndex] = temp;
        }

        OnInventoryChanged?.Invoke(this);
    }

    public InventorySlot GetSlot(int index)
    {
        if (index < 0 || index >= slots.Count) return null;
        return slots[index];
    }

    public int GetSlotCount()
    {
        return slots.Count;
    }
}