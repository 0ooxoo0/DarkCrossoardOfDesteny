using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotParent;
    [SerializeField] private Inventory inventory;

    private InventorySlotUI[] slotUIs;

    private void Start()
    {
        InitializeUI();
        inventory.OnInventoryChanged += UpdateUI;
    }

    private void InitializeUI()
    {
        slotUIs = new InventorySlotUI[inventory.GetSlotCount()];

        for (int i = 0; i < inventory.GetSlotCount(); i++)
        {
            GameObject slot = Instantiate(slotPrefab, slotParent);
            slotUIs[i] = slot.GetComponent<InventorySlotUI>();
            slotUIs[i].Initialize(i, inventory);
        }

        UpdateUI(inventory);
    }

    public void UpdateUI(Inventory inv)
    {
        for (int i = 0; i < slotUIs.Length; i++)
        {
            //Debug.Log("Update");
            InventorySlot slot = inventory.GetSlot(i);
            slotUIs[i].UpdateSlot(slot);
        }
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= UpdateUI;
        }
    }
}