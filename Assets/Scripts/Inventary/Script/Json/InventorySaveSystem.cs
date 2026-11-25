using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Данные сохранения: слоты сетки, экипировка и слот оружия.</summary>
[System.Serializable]
public class InventorySaveData
{
    /// <summary>Версия схемы. 0 — старое сохранение без сетки инвентаря.</summary>
    public int version;

    public List<InventorySlotSaveData> slots = new List<InventorySlotSaveData>();
    public string equippedWeaponId;
    public string weaponSlotItemId;
    public List<EquippedItemSaveData> equippedItems = new List<EquippedItemSaveData>();
}

/// <summary>Один сохранённый слот сетки.</summary>
[System.Serializable]
public class InventorySlotSaveData
{
    public string itemId;
    public int quantity;
}

/// <summary>Один сохранённый предмет экипировки.</summary>
[System.Serializable]
public class EquippedItemSaveData
{
    public string itemId;
    public int slotType;
}

/// <summary>
/// Сохранение инвентаря в JSON. Для восстановления предметов нужен список
/// allItems: по itemId находится сам ассет. Сетка и экипировка сохраняются вместе.
/// </summary>
public class InventorySaveSystem : MonoBehaviour
{
    [SerializeField] private Inventory inventory;

    [Header("Weapon")]
    [SerializeField] private WeaponSlotUI weaponSlot;
    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private List<WeaponData> allWeapons = new List<WeaponData>();
    [SerializeField] private bool loadOnStart;

    [Header("Item Database")]
    [Tooltip("Все ItemData, которые могут встретиться в сохранении.")]
    [SerializeField] private List<ItemData> allItems = new List<ItemData>();

    private string savePath;

    /// <summary>Актуальная версия схемы сохранения.</summary>
    private const int CurrentVersion = 1;

    private void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "inventory_save.json");
        Debug.Log($"Save path: {savePath}");
    }

    private void Start()
    {
        if (loadOnStart) LoadInventory();
    }

    /// <summary>Собирает слоты, экипировку и оружие в JSON и пишет файл.</summary>
    public void SaveInventory()
    {
        if (inventory == null && weaponManager == null && weaponSlot == null) return;

        InventorySaveData saveData = ReadExistingSave() ?? new InventorySaveData();
        saveData.version = CurrentVersion;
        if (saveData.slots == null) saveData.slots = new List<InventorySlotSaveData>();
        if (saveData.equippedItems == null) saveData.equippedItems = new List<EquippedItemSaveData>();

        if (inventory != null)
        {
            saveData.slots.Clear();

            for (int i = 0; i < inventory.GetSlotCount(); i++)
            {
                InventorySlot slot = inventory.GetSlot(i);
                InventorySlotSaveData slotSaveData = new InventorySlotSaveData();

                if (slot != null && !slot.IsEmpty)
                {
                    slotSaveData.itemId = slot.itemData.itemId;
                    slotSaveData.quantity = slot.quantity;
                }
                else
                {
                    slotSaveData.itemId = string.Empty;
                    slotSaveData.quantity = 0;
                }

                saveData.slots.Add(slotSaveData);
            }

            saveData.equippedItems.Clear();

            foreach (KeyValuePair<EquipmentSlotType, InventorySlot> pair in inventory.Equipped)
            {
                if (pair.Value == null || pair.Value.IsEmpty) continue;

                saveData.equippedItems.Add(new EquippedItemSaveData
                {
                    itemId = pair.Value.itemData.itemId,
                    slotType = (int)pair.Key
                });
            }
        }

        if (weaponManager != null && weaponManager.CurrentWeapon != null)
            saveData.equippedWeaponId = weaponManager.CurrentWeapon.weaponId;
        else if (weaponSlot != null && weaponSlot.OwnedWeapon != null)
            saveData.equippedWeaponId = weaponSlot.OwnedWeapon.weaponId;

        if (weaponSlot != null && !weaponSlot.IsDisplayOnly && weaponSlot.OwnedItem != null)
            saveData.weaponSlotItemId = weaponSlot.OwnedItem.itemId;

        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(savePath, json);
        Debug.Log("Inventory saved!");
    }

    /// <summary>Загружает сохранение: сетка, экипировка, оружие.</summary>
    public void LoadInventory()
    {
        if (!File.Exists(savePath))
        {
            Debug.Log("No save file found");
            return;
        }

        try
        {
            string json = File.ReadAllText(savePath);
            InventorySaveData saveData = JsonUtility.FromJson<InventorySaveData>(json);

            if (saveData == null)
            {
                Debug.LogWarning("Save file is empty or invalid");
                return;
            }

            if (inventory != null)
            {
                // Старые сохранения (version 0) писались без сетки инвентаря.
                // Их не применяем, иначе стартовые предметы будут стёрты пустым списком.
                if (saveData.version >= CurrentVersion)
                {
                    inventory.ClearAllSlots();

                    if (saveData.slots != null)
                    {
                        for (int i = 0; i < saveData.slots.Count && i < inventory.GetSlotCount(); i++)
                        {
                            InventorySlotSaveData slotData = saveData.slots[i];
                            if (string.IsNullOrEmpty(slotData.itemId)) continue;

                            ItemData itemData = FindItemById(slotData.itemId);
                            if (itemData != null)
                            {
                                inventory.AddItem(itemData, slotData.quantity);
                            }
                            else
                            {
                                Debug.LogWarning($"Item with ID {slotData.itemId} not found!");
                            }
                        }
                    }
                }
            }

            if (saveData.version >= CurrentVersion) RestoreEquippedItems(saveData);
            RestoreWeapon(saveData);

            Debug.Log("Inventory loaded successfully!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading inventory: {e.Message}");
        }
    }

    /// <summary>Возвращает экипировку из сохранения и сразу надевает оружие в руки.</summary>
    private void RestoreEquippedItems(InventorySaveData saveData)
    {
        if (inventory == null || saveData == null || saveData.equippedItems == null) return;

        for (int i = 0; i < saveData.equippedItems.Count; i++)
        {
            EquippedItemSaveData entry = saveData.equippedItems[i];
            ItemData item = FindItemById(entry.itemId);
            if (item == null) continue;

            EquipmentSlotType slotType = (EquipmentSlotType)entry.slotType;
            inventory.RestoreEquipped(slotType, item);

            if (slotType == EquipmentSlotType.Weapon && weaponManager != null && item.weaponData != null)
                weaponManager.EquipWeapon(item.weaponData);
        }
    }

    /// <summary>Восстанавливает слот оружия и надетое оружие по старым полям сохранения.</summary>
    private void RestoreWeapon(InventorySaveData saveData)
    {
        if (weaponSlot != null && !weaponSlot.IsDisplayOnly)
        {
            ItemData slotItem = FindItemById(saveData.weaponSlotItemId);
            weaponSlot.RestoreOwnedItem(slotItem);
        }

        WeaponData weapon = FindWeaponById(saveData.equippedWeaponId);
        if (weapon == null && weaponSlot != null)
            weapon = weaponSlot.OwnedWeapon;

        if (weapon != null && weaponManager != null)
            weaponManager.EquipWeapon(weapon);
    }

    private WeaponData FindWeaponById(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId) || allWeapons == null) return null;

        for (int i = 0; i < allWeapons.Count; i++)
        {
            WeaponData weapon = allWeapons[i];
            if (weapon != null && weapon.weaponId == weaponId) return weapon;
        }
        return null;
    }

    private ItemData FindItemById(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || allItems == null) return null;

        for (int i = 0; i < allItems.Count; i++)
        {
            ItemData item = allItems[i];
            if (item != null && item.itemId == itemId) return item;
        }
        return null;
    }

    private InventorySaveData ReadExistingSave()
    {
        if (!File.Exists(savePath)) return null;

        try
        {
            return JsonUtility.FromJson<InventorySaveData>(File.ReadAllText(savePath));
        }
        catch
        {
            return null;
        }
    }

    // Автосохранение при выходе и сворачивании.
    private void OnApplicationQuit() => SaveInventory();
    private void OnApplicationPause(bool pauseStatus) { if (pauseStatus) SaveInventory(); }

    /// <summary>Удаляет файл сохранения.</summary>
    public void DeleteSave()
    {
        if (!File.Exists(savePath)) return;

        File.Delete(savePath);
        Debug.Log("Save file deleted");
    }

    /// <summary>Есть ли файл сохранения.</summary>
    public bool SaveExists() => File.Exists(savePath);
}
