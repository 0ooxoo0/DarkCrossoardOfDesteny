using System;
using UnityEngine;

/// <summary>
/// Одна строка таблицы лута: что выпадает, в каком количестве и с каким шансом.
/// Гарантированные строки выпадают всегда, остальные проверяются броском.
/// </summary>
[Serializable]
public class LootRoll
{
    public ItemData item;
    [Min(1)] public int min = 1;
    [Min(1)] public int max = 1;
    [Range(0f, 1f)] public float chance = 1f;
    public bool guaranteed;
}
