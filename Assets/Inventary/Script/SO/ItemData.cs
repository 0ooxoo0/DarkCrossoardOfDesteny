using UnityEngine;

public enum ItemType
{
    Weapon,
    Potion,
    Quest
}

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    [Header("Basic Info")]
    public string itemId; // ”никальный идентификатор дл€ сохранени€
    public string itemName;
    public string description;

    [Header("Visual")]
    public Sprite icon;

    [Header("Gameplay")]
    public ItemType type;
    public bool stackable = true;
    public int maxStackSize = 1;

    [Header("Usage")]
    public bool consumable = false;
}