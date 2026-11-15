using UnityEngine;

/// <summary>Русские подписи и цвета для ItemType — только для интерфейса.</summary>
public static class ItemTypeInfo
{
    private static readonly Color WeaponColor = Hex("#C9A55C");
    private static readonly Color ArmorColor = Hex("#7E96B5");
    private static readonly Color ConsumableColor = Hex("#8FBF7A");
    private static readonly Color QuestColor = Hex("#C98F5C");
    private static readonly Color MiscColor = Hex("#9C9384");

    /// <summary>Название категории для тултипа.</summary>
    public static string GetDisplayName(ItemType type)
    {
        switch (type)
        {
            case ItemType.Weapon: return "Оружие";
            case ItemType.Armor: return "Броня";
            case ItemType.Consumable: return "Расходник";
            case ItemType.Quest: return "Квестовый предмет";
            case ItemType.Misc: return "Материал";
            default: return type.ToString();
        }
    }

    /// <summary>Цвет подписи категории.</summary>
    public static Color GetColor(ItemType type)
    {
        switch (type)
        {
            case ItemType.Weapon: return WeaponColor;
            case ItemType.Armor: return ArmorColor;
            case ItemType.Consumable: return ConsumableColor;
            case ItemType.Quest: return QuestColor;
            case ItemType.Misc: return MiscColor;
            default: return Color.white;
        }
    }

    /// <summary>RGB из HTML-строки с безопасным фолбэком.</summary>
    public static Color Hex(string html)
    {
        return ColorUtility.TryParseHtmlString(html, out Color color) ? color : Color.white;
    }
}

/// <summary>Русские подписи для ItemUseKind — только для интерфейса.</summary>
public static class ItemUseKindInfo
{
    /// <summary>Уточнение к категории, например «Расходник · Пилюля».</summary>
    public static string GetDisplayName(ItemUseKind kind)
    {
        switch (kind)
        {
            case ItemUseKind.Potion: return "Зелье";
            case ItemUseKind.Pill: return "Пилюля";
            case ItemUseKind.Scroll: return "Свиток техники";
            case ItemUseKind.DaoSeed: return "Семя Дао";
            case ItemUseKind.BreakthroughMaterial: return "Материал прорыва";
            default: return string.Empty;
        }
    }
}

/// <summary>Русские подписи для слотов экипировки.</summary>
public static class EquipmentSlotInfo
{
    /// <summary>Подпись слота под иконкой.</summary>
    public static string GetDisplayName(EquipmentSlotType slot)
    {
        switch (slot)
        {
            case EquipmentSlotType.Weapon: return "Оружие";
            case EquipmentSlotType.Armor: return "Броня";
            case EquipmentSlotType.Helmet: return "Шлем";
            case EquipmentSlotType.Boots: return "Сапоги";
            case EquipmentSlotType.Accessory: return "Аксессуар";
            default: return slot.ToString();
        }
    }
}
