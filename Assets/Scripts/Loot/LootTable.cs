using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Таблица лута: список строк с шансами. Одна таблица на тип добычи
/// (дерево, камень, враг, животное), чтобы контент жил в ассетах, а не в коде.
/// </summary>
[CreateAssetMenu(fileName = "LootTable", menuName = "Loot/Loot Table")]
public class LootTable : ScriptableObject
{
    [SerializeField] private List<LootRoll> rolls = new List<LootRoll>();

    public IReadOnlyList<LootRoll> Rolls => rolls;

    /// <summary>
    /// Добавляет результаты ролла в <paramref name="output"/>.
    /// Пустые строки пропускаются: таблица может быть недозаполнена без падений.
    /// </summary>
    public void Roll(List<LootDrop> output)
    {
        if (output == null || rolls == null) return;

        for (int i = 0; i < rolls.Count; i++)
        {
            LootRoll roll = rolls[i];
            if (roll == null || roll.item == null) continue;

            if (!roll.guaranteed && Random.value > Mathf.Clamp01(roll.chance)) continue;

            int min = Mathf.Max(1, roll.min);
            int max = Mathf.Max(min, roll.max);
            int quantity = Random.Range(min, max + 1);

            if (quantity > 0)
                output.Add(new LootDrop(roll.item, quantity));
        }
    }
}
