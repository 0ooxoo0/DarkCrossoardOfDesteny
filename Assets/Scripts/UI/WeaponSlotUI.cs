using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Слот оружия: в SampleScene принимает предмет из сетки drag-and-drop,
/// в Game.unity работает как display-only HUD и показывает экипированное оружие.
/// </summary>
public class WeaponSlotUI : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private Image background;

    [Header("References")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private InventorySaveSystem saveSystem;

    [Header("Mode")]
    [SerializeField] private bool displayOnly;
    [SerializeField] private List<ItemData> weaponItems = new List<ItemData>();

    private ItemData ownedItem;
    private int ownedQuantity;
    private ItemData displayItem;
    private string currentLabel = string.Empty;
    private bool isEquipped;

    public ItemData OwnedItem => ownedItem;
    public WeaponData OwnedWeapon => ownedItem != null ? ownedItem.weaponData : null;
    public bool IsDisplayOnly => displayOnly;

    private void Awake()
    {
        if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
        if (weaponManager == null)
            weaponManager = FindFirstObjectByType<WeaponManager>(FindObjectsInactive.Include);
        if (saveSystem == null)
            saveSystem = FindFirstObjectByType<InventorySaveSystem>(FindObjectsInactive.Include);
    }

    private void Start()
    {
        if (weaponManager != null)
        {
            weaponManager.OnWeaponEquipped += ApplyEquippedWeapon;
            ApplyEquippedWeapon(weaponManager.CurrentWeapon);
        }
        else
        {
            RefreshVisual();
        }
    }

    private void OnDestroy()
    {
        if (weaponManager != null)
            weaponManager.OnWeaponEquipped -= ApplyEquippedWeapon;
    }

    public void SetVisualReferences(Image iconImage, TMP_Text label, Image backgroundImage)
    {
        icon = iconImage;
        nameLabel = label;
        background = backgroundImage;
        RefreshVisual();
    }

    public bool CanAccept(ItemData item)
    {
        return !displayOnly && item != null &&
               item.type == ItemType.Weapon && item.weaponData != null;
    }

    public bool TryAccept(InventorySlotUI source)
    {
        if (displayOnly || source == null) return false;

        InventorySlot sourceSlot = source.GetSlot();
        if (sourceSlot == null || sourceSlot.itemData == null) return false;
        if (!CanAccept(sourceSlot.itemData)) return false;

        Inventory sourceInventory = source.GetInventory();
        if (sourceInventory == null) return false;

        ItemData incoming = sourceSlot.itemData;
        int sourceIndex = source.GetSlotIndex();
        InventorySlot taken = sourceInventory.TakeSlot(sourceIndex);
        if (taken == null) return false;

        ItemData previousItem = ownedItem;
        int previousQuantity = ownedQuantity;

        ownedItem = taken.itemData;
        ownedQuantity = Mathf.Max(1, taken.quantity);
        displayItem = ownedItem;

        if (previousItem != null)
            sourceInventory.SetSlot(sourceIndex, previousItem, previousQuantity);

        if (weaponManager != null)
            weaponManager.EquipWeapon(incoming.weaponData);

        ApplyEquippedWeapon(incoming.weaponData);

        if (saveSystem != null)
            saveSystem.SaveInventory();

        return true;
    }

    public void RestoreOwnedItem(ItemData item, int quantity = 1)
    {
        if (displayOnly)
        {
            displayItem = item;
            currentLabel = item != null ? item.itemName : string.Empty;
            RefreshVisual();
            return;
        }

        ownedItem = item;
        ownedQuantity = item != null ? Mathf.Max(1, quantity) : 0;
        displayItem = ownedItem;
        isEquipped = ownedItem != null;
        currentLabel = ownedItem != null ? ownedItem.itemName : string.Empty;
        RefreshVisual();
    }

    public void ApplyEquippedWeapon(WeaponData weapon)
    {
        if (displayOnly)
        {
            displayItem = FindItemForWeapon(weapon);
            currentLabel = displayItem != null
                ? displayItem.itemName
                : (weapon != null ? weapon.displayName : string.Empty);
            isEquipped = weapon != null;
        }
        else
        {
            isEquipped = ownedItem != null && ownedItem.weaponData == weapon;
            displayItem = ownedItem;
            currentLabel = ownedItem != null ? ownedItem.itemName : string.Empty;
        }

        RefreshVisual();
    }

    private ItemData FindItemForWeapon(WeaponData weapon)
    {
        if (weapon == null || weaponItems == null) return null;

        for (int i = 0; i < weaponItems.Count; i++)
        {
            ItemData item = weaponItems[i];
            if (item != null && item.weaponData == weapon) return item;
        }

        return null;
    }

    private void RefreshVisual()
    {
        if (icon != null)
        {
            Sprite sprite = displayItem != null ? displayItem.icon : null;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.color = (isEquipped || displayOnly)
                ? Color.white
                : new Color(1f, 1f, 1f, 0.45f);
        }

        if (nameLabel != null)
            nameLabel.text = currentLabel ?? string.Empty;

        if (background != null)
        {
            background.color = isEquipped
                ? new Color(0.22f, 0.42f, 0.22f, 0.9f)
                : new Color(0.08f, 0.08f, 0.08f, 0.9f);
        }
    }
}
