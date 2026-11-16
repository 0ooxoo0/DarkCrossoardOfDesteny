using System;
using UnityEngine;

/// <summary>
/// Подписчик на события <see cref="Inventory"/>, который переводит надетые предметы
/// в визуальные объекты персонажа. Инвентарь о сцене не знает — знает только мост.
/// </summary>
public class PlayerEquipmentBridge : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private CharacterEquipmentVisuals visuals;

    private static readonly EquipmentSlotType[] Slots =
        (EquipmentSlotType[])Enum.GetValues(typeof(EquipmentSlotType));

    private void OnEnable()
    {
        if (inventory == null) return;

        inventory.OnItemEquipped += HandleEquipped;
        inventory.OnItemUnequipped += HandleUnequipped;
    }

    private void Start()
    {
        // Sync идёт в Start, а не в OnEnable: CharacterEquipmentVisuals в Awake
        // выключает всё, и порядок Awake между компонентами не гарантирован.
        Sync();
    }

    private void OnDisable()
    {
        if (inventory == null) return;

        inventory.OnItemEquipped -= HandleEquipped;
        inventory.OnItemUnequipped -= HandleUnequipped;
    }

    /// <summary>Приводит внешность в соответствие со всеми надетыми слотами.</summary>
    public void Sync()
    {
        if (inventory == null || visuals == null) return;

        for (int i = 0; i < Slots.Length; i++)
        {
            EquipmentSlotType slot = Slots[i];
            InventorySlot equipped = inventory.GetEquipped(slot);
            ItemData item = equipped != null && !equipped.IsEmpty ? equipped.itemData : null;

            visuals.Apply(slot, item != null ? item.equipVisualId : null);
        }
    }

    private void HandleEquipped(ItemData item, EquipmentSlotType slot)
    {
        if (visuals == null) return;
        visuals.Apply(slot, item != null ? item.equipVisualId : null);
    }

    private void HandleUnequipped(ItemData item, EquipmentSlotType slot)
    {
        if (visuals == null) return;
        visuals.Apply(slot, null);
    }
}
