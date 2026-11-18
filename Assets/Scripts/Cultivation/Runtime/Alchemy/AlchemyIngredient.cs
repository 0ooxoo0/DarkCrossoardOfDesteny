using System;
using UnityEngine;

/// <summary>Один ингредиент рецепта: предмет и сколько его нужно.</summary>
[Serializable]
public class AlchemyIngredient
{
    public ItemData item;
    [Min(1)] public int count = 1;

    public ItemData Item => item;
    public int Count => Mathf.Max(1, count);
}
