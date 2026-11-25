using System;

/// <summary>
/// Одна стопка предметов внутри контейнера лута.
/// Структура, а не класс: контейнер хранит записи в списке и меняет их целиком.
/// </summary>
[Serializable]
public struct LootEntry
{
    /// <summary>Предмет в контейнере.</summary>
    public ItemData item;

    /// <summary>Сколько единиц лежит.</summary>
    public int quantity;

    /// <summary>Создаёт запись лута.</summary>
    public LootEntry(ItemData item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
    }

    /// <summary>Запись осмысленна, только если есть предмет и количество больше нуля.</summary>
    public bool IsValid => item != null && quantity > 0;
}
