using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Категория предмета. Используется сеткой, сортировкой и слотами экипировки.
/// Специальные типы культивации сюда не входят — их описывает <see cref="ItemUseKind"/>.
/// </summary>
public enum ItemType
{
    /// <summary>Оружие. Надевается в слот Weapon.</summary>
    Weapon = 0,

    /// <summary>Броня и её части. Сейчас используется только слот Armor.</summary>
    Armor = 1,

    /// <summary>Расходник. Конкретное действие описывает <see cref="ItemUseKind"/>.</summary>
    Consumable = 2,

    /// <summary>Квестовый предмет: не выбрасывается и не применяется вручную.</summary>
    Quest = 3,

    /// <summary>Материалы, хлам и всё, что не попало в другие категории.</summary>
    Misc = 4
}

/// <summary>
/// Способ применения предмета. Инвентарь это поле не толкует: он лишь передаёт
/// предмет событиям <see cref="Inventory.OnItemUseRequested"/>, а решение принимает
/// внешняя система (культивация, навыки и т. д.).
/// </summary>
public enum ItemUseKind
{
    /// <summary>Предмет не применяется вручную.</summary>
    None = 0,

    /// <summary>Обычное зелье. Эффекта пока нет — заготовка под систему навыков.</summary>
    Potion = 1,

    /// <summary>Пилюля культивации.</summary>
    Pill = 2,

    /// <summary>Свиток техники.</summary>
    Scroll = 3,

    /// <summary>Семя Дао.</summary>
    DaoSeed = 4,

    /// <summary>Материал прорыва: применяется системой культивации, не вручную.</summary>
    BreakthroughMaterial = 5
}

/// <summary>
/// Описание предмета. Один ассет — один предмет.
/// Поле <see cref="itemId"/> должно быть уникальным: по нему работают сохранения.
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    /// <summary>Версия раскладки данных. Нужна миграции после смены ItemType.</summary>
    [SerializeField, HideInInspector] private int dataVersion;

    [Header("Basic Info")]
    [Tooltip("Уникальный идентификатор для сохранений. Не меняйте после релиза.")]
    public string itemId;

    [Tooltip("Имя, которое видит игрок. Поле называется itemName ради совместимости с уже созданными ассетами.")]
    public string itemName;

    [TextArea(2, 5)]
    public string description;

    [Header("Visual")]
    public Sprite icon;

    [Header("Gameplay")]
    public ItemType type;

    [Tooltip("Только для расходников и материалов. Оружие и броня не стакаются.")]
    public bool stackable = true;

    [Tooltip("Максимум в одном слоте. Для оружия и брони принудительно 1.")]
    [Min(1)]
    public int maxStackSize = 1;

    [Header("Economy")]
    [Tooltip("Цена продажи в гоблинах-медяках. 0 — предмет нельзя продать.")]
    [Min(0)]
    public int sellPrice;

    [Header("Usage")]
    [Tooltip("Как применяется предмет. Читается внешними системами, инвентарь его не разбирает.")]
    public ItemUseKind useKind = ItemUseKind.None;

    [Tooltip("Устаревший флаг. Оставлен, чтобы не ломать ассеты; используйте useKind.")]
    public bool consumable;

    [Header("Weapon")]
    [Tooltip("Необязательно: данные оружия для системы боя. Инвентарь их не читает.")]
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
    [Tooltip("Открываемый путь Дао. Имеет смысл только при useKind = DaoSeed.")]
    public DaoType unlocksDao;

    /// <summary>Имя для интерфейса с запасным вариантом на имя ассета.</summary>
    public string DisplayName => string.IsNullOrEmpty(itemName) ? name : itemName;

    /// <summary>Безопасный максимум стака: никогда не меньше единицы.</summary>
    public int MaxStack => Mathf.Max(1, maxStackSize);

    /// <summary>Можно ли надеть предмет в слот экипировки.</summary>
    public bool IsEquipment => type == ItemType.Weapon || type == ItemType.Armor;

    /// <summary>Слот, в который предмет надевается. Шлем/сапоги/аксессуар появятся позже.</summary>
    public EquipmentSlotType EquipmentSlot =>
        type == ItemType.Weapon ? EquipmentSlotType.Weapon : EquipmentSlotType.Armor;

    /// <summary>Версия данных, через которую работает миграция старых ассетов.</summary>
    public int DataVersion => dataVersion;

    /// <summary>Проверка тега материала прорыва.</summary>
    public bool HasBreakthroughTag(string tag)
    {
        return !string.IsNullOrWhiteSpace(tag) && breakthroughTags != null && breakthroughTags.Contains(tag);
    }

    /// <summary>Помечает ассет как перенесённый на актуальную раскладку ItemType.</summary>
    public void MarkMigrated(int version)
    {
        dataVersion = version;
    }

    private void OnValidate()
    {
        maxStackSize = Mathf.Max(1, maxStackSize);

        // Оружие и броня никогда не стакаются: так проще и безопаснее для экипировки.
        if (IsEquipment)
        {
            stackable = false;
            maxStackSize = 1;
            if (useKind != ItemUseKind.None) useKind = ItemUseKind.None;
        }
    }
}
