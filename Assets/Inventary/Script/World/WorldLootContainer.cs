using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Контейнер лута в мире: один визуал на земле и список записей внутри.
/// Не знает ни о игроке, ни об интерфейсе — только принимает и отдаёт предметы.
/// </summary>
public class WorldLootContainer : MonoBehaviour
{
    [Header("Содержимое")]
    [SerializeField] private List<LootEntry> entries = new List<LootEntry>();

    /// <summary>Содержимое изменилось: интерфейс должен перерисовать список.</summary>
    public event Action<WorldLootContainer> OnChanged;

    /// <summary>Контейнер опустел: подписчики отписываются, объект уничтожается.</summary>
    public event Action<WorldLootContainer> OnEmptied;

    /// <summary>Записи только для чтения.</summary>
    public IReadOnlyList<LootEntry> Entries => entries;

    /// <summary>В контейнере не осталось предметов.</summary>
    public bool IsEmpty => entries.Count == 0;

    /// <summary>
    /// Кладёт предметы в контейнер, объединяя с существующей записью того же ассета.
    /// </summary>
    public void Add(ItemData item, int quantity)
    {
        if (item == null || quantity <= 0) return;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].item != item) continue;

            LootEntry merged = entries[i];
            merged.quantity += quantity;
            entries[i] = merged;
            OnChanged?.Invoke(this);
            return;
        }

        entries.Add(new LootEntry(item, quantity));
        OnChanged?.Invoke(this);
    }

    /// <summary>
    /// Пытается забрать указанное количество предмета в инвентарь.
    /// Берёт столько, сколько влезает; остаток остаётся в контейнере.
    /// </summary>
    public bool TryTake(ItemData item, int quantity, Inventory inventory)
    {
        if (item == null || quantity <= 0 || inventory == null) return false;

        int index = IndexOf(item);
        if (index < 0) return false;

        int space = inventory.GetFreeSpace(item);
        int take = Mathf.Min(quantity, Mathf.Min(entries[index].quantity, space));
        if (take <= 0) return false;

        if (!inventory.AddItem(item, take)) return false;

        LootEntry entry = entries[index];
        entry.quantity -= take;

        // Нулевые записи удаляются сразу: пустой список = пустой контейнер.
        if (entry.quantity <= 0) entries.RemoveAt(index);
        else entries[index] = entry;

        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Забирает всё, что влезает в инвентарь. Возвращает true, если взято хоть что-то.
    /// </summary>
    public bool TryTakeAll(Inventory inventory)
    {
        if (inventory == null || entries.Count == 0) return false;

        bool takenAnything = false;

        // Идём по копии: исходный список меняется по ходу.
        List<LootEntry> snapshot = new List<LootEntry>(entries);

        for (int i = 0; i < snapshot.Count; i++)
        {
            ItemData item = snapshot[i].item;
            if (item == null) continue;

            int space = inventory.GetFreeSpace(item);
            int take = Mathf.Min(snapshot[i].quantity, space);
            if (take <= 0) continue;

            if (!inventory.AddItem(item, take)) continue;

            takenAnything = true;
            RemoveQuantity(item, take);
        }

        if (!takenAnything) return false;

        NotifyChanged();
        return true;
    }

    private int IndexOf(ItemData item)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].item == item && entries[i].quantity > 0) return i;
        }

        return -1;
    }

    private void RemoveQuantity(ItemData item, int quantity)
    {
        int index = IndexOf(item);
        if (index < 0) return;

        LootEntry entry = entries[index];
        entry.quantity -= quantity;

        if (entry.quantity <= 0) entries.RemoveAt(index);
        else entries[index] = entry;
    }

    private void NotifyChanged()
    {
        OnChanged?.Invoke(this);

        if (!IsEmpty) return;

        OnEmptied?.Invoke(this);
        Destroy(gameObject);
    }
}
