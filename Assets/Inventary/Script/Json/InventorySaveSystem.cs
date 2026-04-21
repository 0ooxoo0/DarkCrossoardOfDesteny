using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class InventorySaveData
{
    public List<InventorySlotSaveData> slots = new List<InventorySlotSaveData>();
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

    [Header("Item Database")]
    [SerializeField] private List<ItemData> allItems = new List<ItemData>(); // Перетащите сюда ВСЕ предметы из проекта

    private string savePath;

    private void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "inventory_save.json");
        Debug.Log($"Save path: {savePath}");
    }

    // Сохранение инвентаря
    public void SaveInventory()
    {
        if (inventory == null) return;

        var saveData = new InventorySaveData();

        for (int i = 0; i < inventory.GetSlotCount(); i++)
        {
            var slot = inventory.GetSlot(i);
            var slotSaveData = new InventorySlotSaveData();

            if (slot.itemData != null)
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
            var saveData = JsonUtility.FromJson<InventorySaveData>(json);

            // Очищаем инвентарь
            ClearInventory();

            // Загружаем данные
            for (int i = 0; i < saveData.slots.Count && i < inventory.GetSlotCount(); i++)
            {
                var slotData = saveData.slots[i];

                if (!string.IsNullOrEmpty(slotData.itemId))
                {
                    ItemData itemData = FindItemById(slotData.itemId);
                    if (itemData != null)
                    {
                        // Добавляем предмет
                        inventory.AddItem(itemData, slotData.quantity);
                    }
                    else
                    {
                        Debug.LogWarning($"Item with ID {slotData.itemId} not found!");
                    }
                }
            }

            Debug.Log("Inventory loaded successfully!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading inventory: {e.Message}");
        }
    }

    // Поиск предмета в списке
    private ItemData FindItemById(string itemId)
    {
        foreach (var item in allItems)
        {
            if (item.itemId == itemId)
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