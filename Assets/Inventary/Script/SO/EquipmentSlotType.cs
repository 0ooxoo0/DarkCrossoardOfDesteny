/// <summary>
/// Слоты экипировки. Реально работает Weapon; остальные заведены заранее,
/// чтобы систему можно было расширять без переделки сохранений и UI.
/// </summary>
public enum EquipmentSlotType
{
    /// <summary>Оружие в правой руке.</summary>
    Weapon = 0,

    /// <summary>Нательная броня.</summary>
    Armor = 1,

    /// <summary>Шлем.</summary>
    Helmet = 2,

    /// <summary>Сапоги.</summary>
    Boots = 3,

    /// <summary>Кольцо, амулет или другой аксессуар.</summary>
    Accessory = 4
}
