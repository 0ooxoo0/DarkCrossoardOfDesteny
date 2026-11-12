using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{
    Weapon,
    Potion,
    Quest,
    Pill,
    Scroll,
    BreakthroughMaterial,
    DaoSeed
}

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    [Header("Basic Info")]
    public string itemId; // Уникальный идентификатор для сохранения
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

    [Header("Weapon")]
    public WeaponData weaponData;

    [Header("Breakthrough")]
    [Tooltip("Теги материалов, например herb.mortal или ore.jade.")]
    public List<string> breakthroughTags = new List<string>();

    [Header("Pill")]
    [Min(0)] public int qiRestore;
    [Min(0)] public int experienceQiGain;
    [Min(0)] public int comprehensionGain;

    [Header("Scroll")]
    public TechniqueData technique;

    [Header("Dao Seed")]
    public DaoType unlocksDao;

    public bool HasBreakthroughTag(string tag)
    {
        return !string.IsNullOrWhiteSpace(tag) && breakthroughTags != null && breakthroughTags.Contains(tag);
    }
}