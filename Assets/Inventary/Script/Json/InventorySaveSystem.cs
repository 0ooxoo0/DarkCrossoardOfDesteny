using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class InventorySaveData
{
    public List<InventorySlotSaveData> slots = new List<InventorySlotSaveData>();
    public string equippedWeaponId;
    public string weaponSlotItemId;
}

[System.Serializable]
public class InventorySlotSaveData
{
    public string itemId;
    public int quantity;
}

public class InventorySaveSystem : MonoBehaviour
{
    [SerializeField] private Inventory inventory;

    [Header("Weapon")]
    [SerializeField] private WeaponSlotUI weaponSlot;
    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private List<WeaponData> allWeapons = new List<WeaponData>();
    [SerializeField] private bool loadOnStart;

    [Header("Item Database")]
    [SerializeField] private List<ItemData> allItems = new List<ItemData>(); // Перетащите сюда ВСЕ предметы из проекта

    private string savePath;

    private void Start()
    {
        if (loadOnStart) LoadInventory();
    }

    private void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "inventory_save.json");
        Debug.Log($"Save path: {savePath}");
    }

    // Сохранение инвентаря
    public void SaveInventory()
    {
        if (inventory == null && weaponManager == null && weaponSlot == null) return;

        InventorySaveData saveData = null;
        if (File.Exists(savePath))
        {
            try
            {
                saveData = JsonUtility.FromJson<InventorySaveData>(File.ReadAllText(savePath));
            }
            catch
            {
                saveData = null;
            }
        }

        if (saveData == null) saveData = new InventorySaveData();
        if (saveData.slots == null) saveData.slots = new List<InventorySlotSaveData>();

        if (inventory != null)
        {
            saveData.slots.Clear();

            for (int i = 0; i < inventory.GetSlotCount(); i++)
            {
                InventorySlot slot = inventory.GetSlot(i);
                InventorySlotSaveData slotSaveData = new InventorySlotSaveData();

                if (slot != null && slot.itemData != null)
                {
                    slotSaveData.itemId = slot.itemData.itemId;
                    slotSaveData.quantity = slot.quantity;
                }
                else
                {
                    slotSaveData.itemId = "";
                    slotSaveData.quantity = 0;
                }

                saveData.slots.Add(slotSaveData);
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

    // Загрузка инвентаря
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
                ClearInventory();

                if (saveData.slots != null)
                {
                    for (int i = 0; i < saveData.slots.Count && i < inventory.GetSlotCount(); i++)
                    {
                        InventorySlotSaveData slotData = saveData.slots[i];

                        if (!string.IsNullOrEmpty(slotData.itemId))
                        {
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

            RestoreWeapon(saveData);

            Debug.Log("Inventory loaded successfully!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading inventory: {e.Message}");
        }
    }

    // Поиск предмета в списке
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

        foreach (WeaponData weapon in allWeapons)
        {
            if (weapon != null && weapon.weaponId == weaponId) return weapon;
        }
        return null;
    }

    private ItemData FindItemById(string itemId)
    {
        if (allItems == null) return null;

        foreach (var item in allItems)
        {
            if (item != null && item.itemId == itemId)
                return item;
        }
        return null;
    }

    // Очистка инвентаря
    private void ClearInventory()
    {
        for (int i = 0; i < inventory.GetSlotCount(); i++)
        {
            var slot = inventory.GetSlot(i);
            if (slot.itemData != null)
            {
                inventory.RemoveItem(i, slot.quantity);
            }
        }
    }

    // Автоматическое сохранение
    private void OnApplicationQuit() => SaveInventory();
    private void OnApplicationPause(bool pauseStatus) { if (pauseStatus) SaveInventory(); }

    // Удаление сохранения
    public void DeleteSave()
    {
        if (File.Exists(savePath))
        {
            File.Delete(savePath);
            Debug.Log("Save file deleted");
        }
    }

    public bool SaveExists() => File.Exists(savePath);
}