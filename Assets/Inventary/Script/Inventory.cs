using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Единая точка правды по предметам игрока: сетка слотов, стаки, экипировка.
/// Инвентарь не знает про оружие, скиллы и культивацию: наружу уходят только
/// события <see cref="OnItemEquipped"/>, <see cref="OnItemUnequipped"/> и
/// <see cref="OnItemUseRequested"/>, а решение принимают другие системы.
/// </summary>
[DisallowMultipleComponent]
public class Inventory : MonoBehaviour
{
    /// <summary>Стартовый предмет: используется тестовым набором и настройкой сцены.</summary>
    [Serializable]
    public struct StartingItem
    {
        public ItemData item;

        [Min(1)]
        public int quantity;
    }

    [Header("Размер сетки")]
    [SerializeField, Min(1)] private int width = 5;
    [SerializeField, Min(1)] private int height = 4;

    [Header("Стартовые предметы")]
    [Tooltip("Выдаются один раз в Awake, до загрузки сохранения. Нужны для теста и отладки.")]
    [SerializeField] private List<StartingItem> startingItems = new List<StartingItem>();

    /// <summary>Инвентарь изменился любым способом. Аргумент — сам инвентарь.</summary>
    public event Action<Inventory> OnInventoryChanged;

    /// <summary>Добавлен предмет: (предмет, добавленное количество).</summary>
    public event Action<ItemData, int> OnItemAdded;

    /// <summary>Удалён предмет: (предмет, удалённое количество).</summary>
    public event Action<ItemData, int> OnItemRemoved;

    /// <summary>Предмет надет: (предмет, слот экипировки).</summary>
    public event Action<ItemData, EquipmentSlotType> OnItemEquipped;

    /// <summary>Предмет снят: (предмет, слот экипировки).</summary>
    public event Action<ItemData, EquipmentSlotType> OnItemUnequipped;

    /// <summary>
    /// Запрос на применение предмета. Инвентарь ничего не применяет сам:
    /// подписчики (культивация, навыки) решают, что делать, и сами вызывают
    /// <see cref="RemoveItem(int,int)"/> при успехе.
    /// </summary>
    public event Action<ItemData, Inventory, int> OnItemUseRequested;

    private readonly List<InventorySlot> slots = new List<InventorySlot>();
    private readonly Dictionary<EquipmentSlotType, InventorySlot> equipped =
        new Dictionary<EquipmentSlotType, InventorySlot>();

    /// <summary>Ширина сетки (для UI).</summary>
    public int Width => width;

    /// <summary>Высота сетки (для UI).</summary>
    public int Height => height;

    /// <summary>Только чтение списка слотов: UI и сохранения читают, менять — только через методы.</summary>
    public IReadOnlyList<InventorySlot> Slots => slots;

    /// <summary>Только чтение экипировки.</summary>
    public IReadOnlyDictionary<EquipmentSlotType, InventorySlot> Equipped => equipped;

    private void Awake()
    {
        InitializeSlots();
        AddStartingItems();
    }

    private void InitializeSlots()
    {
        slots.Clear();
        equipped.Clear();

        int count = Mathf.Max(1, width) * Mathf.Max(1, height);
        for (int i = 0; i < count; i++)
        {
            slots.Add(new InventorySlot(null, 0));
        }
    }

    private void AddStartingItems()
    {
        if (startingItems == null) return;

        for (int i = 0; i < startingItems.Count; i++)
        {
            StartingItem entry = startingItems[i];
            if (entry.item != null && entry.quantity > 0)
            {
                // События на этом этапе ещё никто не слушает — это нормально:
                // UI читает готовое состояние в Start.
                AddItem(entry.item, entry.quantity);
            }
        }
    }

    private void EnsureInitialized()
    {
        if (slots.Count == 0) InitializeSlots();
    }

    // ---------------------------------------------------------------- Добавление

    /// <summary>
    /// Добавляет предмет с автоматическим стакингом. Операция атомарная:
    /// если места не хватает на всё количество, не добавляется ничего.
    /// </summary>
    public bool AddItem(ItemData item, int count = 1)
    {
        if (item == null || count <= 0) return false;
        EnsureInitialized();

        if (GetFreeSpace(item) < count)
        {
            Debug.LogWarning($"[Inventory] Не хватает места: {item.DisplayName} × {count}.");
            return false;
        }

        int remaining = count;

        if (item.stackable)
        {
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                InventorySlot slot = slots[i];
                if (!slot.CanStackWith(item)) continue;

                int add = Mathf.Min(remaining, slot.SpaceLeft);
                slot.AddItem(add);
                remaining -= add;
            }
        }

        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (!slots[i].IsEmpty) continue;

            int add = Mathf.Min(remaining, item.MaxStack);
            slots[i] = new InventorySlot(item, add);
            remaining -= add;
        }

        OnItemAdded?.Invoke(item, count);
        OnInventoryChanged?.Invoke(this);
        return true;
    }

    /// <summary>Совместимость со старыми кнопками сцены: добавляет один предмет.</summary>
    public void AddTestItem(ItemData item)
    {
        AddItem(item);
    }

    /// <summary>Сколько единиц предмета ещё влезет с учётом стаков и пустых слотов.</summary>
    public int GetFreeSpace(ItemData item)
    {
        if (item == null) return 0;

        int space = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];

            if (slot.IsEmpty)
            {
                space += item.MaxStack;
            }
            else if (item.stackable && slot.itemData == item)
            {
                space += Mathf.Max(0, item.MaxStack - slot.quantity);
            }
        }

        return space;
    }

    /// <summary>Есть ли предмет в инвентаре в нужном количестве.</summary>
    public bool HasItem(ItemData item, int count = 1)
    {
        return GetItemCount(item) >= count;
    }

    /// <summary>Суммарное количество предмета во всех слотах сетки.</summary>
    public int GetItemCount(ItemData item)
    {
        if (item == null) return 0;

        int total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].itemData == item) total += slots[i].quantity;
        }
        return total;
    }

    // ----------------------------------------------------------------- Удаление

    /// <summary>Убирает количество из конкретного слота.</summary>
    public void RemoveItem(int slotIndex, int count = 1)
    {
        InventorySlot slot = GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty || count <= 0) return;

        ItemData item = slot.itemData;
        int remove = Mathf.Min(count, slot.quantity);
        slot.RemoveItem(remove);

        OnItemRemoved?.Invoke(item, remove);
        OnInventoryChanged?.Invoke(this);
    }

    /// <summary>
    /// Убирает указанное количество предмета из любых слотов.
    /// Возвращает, сколько реально удалось убрать.
    /// </summary>
    public int RemoveItem(ItemData item, int count = 1)
    {
        if (item == null || count <= 0) return 0;

        int remaining = count;
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            InventorySlot slot = slots[i];
            if (slot.itemData != item) continue;

            int remove = Mathf.Min(remaining, slot.quantity);
            slot.RemoveItem(remove);
            remaining -= remove;
        }

        int removed = count - remaining;
        if (removed > 0)
        {
            OnItemRemoved?.Invoke(item, removed);
            OnInventoryChanged?.Invoke(this);
        }
        return removed;
    }

    /// <summary>Очищает сетку и экипировку. Нужно загрузке сохранения и тестам.</summary>
    public void ClearAllSlots()
    {
        List<EquipmentSlotType> equippedKeys = new List<EquipmentSlotType>(equipped.Keys);
        for (int i = 0; i < equippedKeys.Count; i++)
        {
            InventorySlot slot = equipped[equippedKeys[i]];
            if (slot != null && !slot.IsEmpty)
                OnItemUnequipped?.Invoke(slot.itemData, equippedKeys[i]);
        }

        equipped.Clear();

        for (int i = 0; i < slots.Count; i++)
        {
            slots[i] = new InventorySlot(null, 0);
        }

        OnInventoryChanged?.Invoke(this);
    }

    // ------------------------------------------------------------ Перемещение

    /// <summary>Меняет два слота местами. Одинаковые стаки сливаются, остаток остаётся в источнике.</summary>
    public void SwapSlots(int fromIndex, int toIndex)
    {
        MoveItem(fromIndex, toIndex);
    }

    /// <summary>
    /// Переносит содержимое слота в другой слот. Если предметы одинаковые и стакаются —
    /// сливает их; иначе меняет местами.
    /// </summary>
    public void MoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || fromIndex >= slots.Count || toIndex < 0 || toIndex >= slots.Count) return;

        InventorySlot fromSlot = slots[fromIndex];
        InventorySlot toSlot = slots[toIndex];

        if (fromSlot.itemData != null && toSlot.itemData != null &&
            fromSlot.itemData == toSlot.itemData && fromSlot.itemData.stackable)
        {
            int total = fromSlot.quantity + toSlot.quantity;
            int maxStack = fromSlot.itemData.MaxStack;

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
            slots[fromIndex] = toSlot.Clone();
            slots[toIndex] = fromSlot.Clone();
        }

        OnInventoryChanged?.Invoke(this);
    }

    // -------------------------------------------------------------- Экипировка

    /// <summary>Надетый предмет в указанном слоте или null.</summary>
    public InventorySlot GetEquipped(EquipmentSlotType slotType)
    {
        return equipped.TryGetValue(slotType, out InventorySlot slot) ? slot : null;
    }

    /// <summary>
    /// Надевает предмет из слота сетки. Если в слоте экипировки уже что-то было,
    /// оно возвращается в освобождённый слот сетки (обмен).
    /// </summary>
    public bool TryEquip(InventorySlot slot, EquipmentSlotType slotType)
    {
        if (slot == null) return false;

        int index = slots.IndexOf(slot);
        return TryEquip(index, slotType);
    }

    /// <summary>Надевает предмет из указанного слота сетки в слот экипировки.</summary>
    public bool TryEquip(int slotIndex, EquipmentSlotType slotType)
    {
        InventorySlot source = GetSlot(slotIndex);
        if (source == null || source.IsEmpty) return false;

        if (!IsCompatible(source.itemData, slotType))
        {
            Debug.LogWarning($"[Inventory] «{source.itemData.DisplayName}» нельзя надеть в слот {slotType}.");
            return false;
        }

        // Экипируем всегда по одному предмету: оружие и броня не стакаются.
        if (source.quantity > 1)
        {
            Debug.LogWarning($"[Inventory] «{source.itemData.DisplayName}» лежит стаком — наденьте по одному.");
            return false;
        }

        ItemData incoming = source.itemData;
        InventorySlot previous = GetEquipped(slotType);

        slots[slotIndex] = new InventorySlot(null, 0);

        if (previous != null && !previous.IsEmpty)
        {
            // Снятое возвращаем ровно в освободившийся слот — это предсказуемо для игрока.
            slots[slotIndex] = new InventorySlot(previous.itemData, previous.quantity);
            equipped.Remove(slotType);
            OnItemUnequipped?.Invoke(previous.itemData, slotType);
        }

        equipped[slotType] = new InventorySlot(incoming, 1);
        OnItemEquipped?.Invoke(incoming, slotType);
        OnInventoryChanged?.Invoke(this);
        return true;
    }

    /// <summary>
    /// Восстанавливает экипировку из сохранения. Проверки сетки не выполняются:
    /// сохранение — доверенный источник, а событие нужно подписчикам (система оружия).
    /// </summary>
    public bool RestoreEquipped(EquipmentSlotType slotType, ItemData item, int quantity = 1)
    {
        if (item == null) return false;

        equipped[slotType] = new InventorySlot(item, Mathf.Max(1, quantity));
        OnItemEquipped?.Invoke(item, slotType);
        OnInventoryChanged?.Invoke(this);
        return true;
    }

    /// <summary>Снимает предмет из слота экипировки в первый свободный слот сетки.</summary>
    public bool Unequip(EquipmentSlotType slotType)
    {
        InventorySlot current = GetEquipped(slotType);
        if (current == null || current.IsEmpty) return false;

        int freeIndex = FindEmptySlotIndex();
        if (freeIndex < 0)
        {
            Debug.LogWarning($"[Inventory] Нельзя снять «{current.itemData.DisplayName}»: в сетке нет свободного слота.");
            return false;
        }

        ItemData removed = current.itemData;
        slots[freeIndex] = new InventorySlot(removed, current.quantity);
        equipped.Remove(slotType);

        OnItemUnequipped?.Invoke(removed, slotType);
        OnInventoryChanged?.Invoke(this);
        return true;
    }

    /// <summary>
    /// Перетаскивает надетый предмет в конкретный слот сетки. Если слот занят —
    /// обмен возможен только когда лежащий там предмет подходит к тому же слоту.
    /// </summary>
    public bool TryMoveEquippedToSlot(EquipmentSlotType slotType, int slotIndex)
    {
        InventorySlot current = GetEquipped(slotType);
        if (current == null || current.IsEmpty) return false;

        InventorySlot target = GetSlot(slotIndex);
        if (target == null) return false;

        if (target.IsEmpty)
        {
            slots[slotIndex] = new InventorySlot(current.itemData, current.quantity);
            equipped.Remove(slotType);
            OnItemUnequipped?.Invoke(current.itemData, slotType);
            OnInventoryChanged?.Invoke(this);
            return true;
        }

        if (!IsCompatible(target.itemData, slotType) || target.quantity > 1)
        {
            return false;
        }

        ItemData swappedOut = target.itemData;
        slots[slotIndex] = new InventorySlot(current.itemData, current.quantity);
        equipped[slotType] = new InventorySlot(swappedOut, 1);

        OnItemUnequipped?.Invoke(current.itemData, slotType);
        OnItemEquipped?.Invoke(swappedOut, slotType);
        OnInventoryChanged?.Invoke(this);
        return true;
    }

    private static bool IsCompatible(ItemData item, EquipmentSlotType slotType)
    {
        if (item == null) return false;
        if (slotType == EquipmentSlotType.Weapon) return item.type == ItemType.Weapon;
        return item.type == ItemType.Armor;
    }

    // ------------------------------------------------------------ Применение

    /// <summary>
    /// Просит применить предмет из слота. Оружие и броня надеваются сразу,
    /// остальное уходит подписчикам <see cref="OnItemUseRequested"/>.
    /// </summary>
    public bool RequestUse(int slotIndex)
    {
        EnsureInitialized();

        InventorySlot slot = GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty) return false;

        ItemData item = slot.itemData;

        if (item.IsEquipment)
        {
            return TryEquip(slotIndex, item.EquipmentSlot);
        }

        if (OnItemUseRequested == null)
        {
            Debug.Log($"[Inventory] Применение «{item.DisplayName}» пока не реализовано: нет подписчиков.");
            return false;
        }

        OnItemUseRequested.Invoke(item, this, slotIndex);
        return true;
    }

    // -------------------------------------------------------------- Сортировка

    /// <summary>Сортирует сетку: по типу, затем по имени. Пустые слоты уходят в конец.</summary>
    public void SortByTypeAndName()
    {
        List<InventorySlot> filled = new List<InventorySlot>();
        List<InventorySlot> empty = new List<InventorySlot>();

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty) empty.Add(new InventorySlot(null, 0));
            else filled.Add(slots[i]);
        }

        filled.Sort((a, b) =>
        {
            int byType = a.itemData.type.CompareTo(b.itemData.type);
            if (byType != 0) return byType;

            return string.Compare(a.itemData.DisplayName, b.itemData.DisplayName,
                StringComparison.OrdinalIgnoreCase);
        });

        slots.Clear();
        slots.AddRange(filled);
        slots.AddRange(empty);

        OnInventoryChanged?.Invoke(this);
    }

    /// <summary>Убирает последний предмет в сетке. Отладочная кнопка старой сцены.</summary>
    public void RemoveLastItem()
    {
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (!slots[i].IsEmpty)
            {
                RemoveItem(i, 1);
                return;
            }
        }
    }

    // ------------------------------------------------------- Прямой доступ к слотам

    /// <summary>Индекс первого пустого слота или -1.</summary>
    public int FindEmptySlotIndex()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty) return i;
        }
        return -1;
    }

    /// <summary>Забирает содержимое слота: слот становится пустым, предмет возвращается наружу.</summary>
    public InventorySlot TakeSlot(int slotIndex)
    {
        InventorySlot slot = GetSlot(slotIndex);
        if (slot == null) return null;

        InventorySlot taken = slot.Clone();
        slots[slotIndex] = new InventorySlot(null, 0);
        OnInventoryChanged?.Invoke(this);
        return taken;
    }

    /// <summary>Кладёт предмет в слот напрямую. Используется загрузкой и обменом оружия.</summary>
    public bool SetSlot(int slotIndex, ItemData item, int count)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count) return false;

        if (item == null || count <= 0)
        {
            slots[slotIndex] = new InventorySlot(null, 0);
        }
        else
        {
            slots[slotIndex] = new InventorySlot(item, Mathf.Clamp(count, 1, item.MaxStack));
        }

        OnInventoryChanged?.Invoke(this);
        return true;
    }

    /// <summary>Слот по индексу или null, если индекс вне сетки.</summary>
    public InventorySlot GetSlot(int index)
    {
        if (index < 0 || index >= slots.Count) return null;
        return slots[index];
    }

    /// <summary>Количество слотов в сетке.</summary>
    public int GetSlotCount()
    {
        return slots.Count;
    }

    // ------------------------------------------- Совместимость с культивацией

    /// <summary>Суммарное количество предметов с указанным тегом прорыва.</summary>
    public int CountItemsWithTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return 0;

        int total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot?.itemData == null) continue;

            List<string> tags = slot.itemData.breakthroughTags;
            if (tags == null) continue;
            if (tags.Contains(tag)) total += slot.quantity;
        }
        return total;
    }

    /// <summary>Есть ли предметы со всеми (requireAll) или хотя бы с одним из тегов.</summary>
    public bool HasItemsWithTags(IReadOnlyList<string> tags, bool requireAll = false)
    {
        if (tags == null || tags.Count == 0) return true;

        if (requireAll)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (CountItemsWithTag(tags[i]) <= 0) return false;
            }
            return true;
        }

        for (int i = 0; i < tags.Count; i++)
        {
            if (CountItemsWithTag(tags[i]) > 0) return true;
        }
        return false;
    }

    /// <summary>Есть ли хотя бы один предмет с любым из тегов.</summary>
    public bool HasAnyItemWithTags(IReadOnlyList<string> tags)
    {
        return HasItemsWithTags(tags, false);
    }

    /// <summary>Есть ли предметы со всеми тегами одновременно.</summary>
    public bool HasAllItemsWithTags(IReadOnlyList<string> tags)
    {
        return HasItemsWithTags(tags, true);
    }

    /// <summary>Убирает по countPerTag единиц для каждого тега. Возвращает число удалённых.</summary>
    public int RemoveItemsByTags(IReadOnlyList<string> tags, int countPerTag = 1)
    {
        if (tags == null || tags.Count == 0 || countPerTag <= 0) return 0;

        int removed = 0;
        HashSet<string> processed = new HashSet<string>();

        for (int t = 0; t < tags.Count; t++)
        {
            string tag = tags[t];
            if (string.IsNullOrWhiteSpace(tag) || !processed.Add(tag)) continue;

            int remaining = countPerTag;
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                InventorySlot slot = slots[i];
                if (slot?.itemData == null) continue;

                List<string> itemTags = slot.itemData.breakthroughTags;
                if (itemTags == null || !itemTags.Contains(tag)) continue;

                int take = Mathf.Min(remaining, slot.quantity);
                slot.RemoveItem(take);
                remaining -= take;
                removed += take;
            }
        }

        if (removed > 0) OnInventoryChanged?.Invoke(this);
        return removed;
    }
}
