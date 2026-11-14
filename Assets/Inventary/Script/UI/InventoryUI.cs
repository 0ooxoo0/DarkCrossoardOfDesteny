using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Сетка инвентаря: создаёт вьюхи слотов из префаба и обновляет их
/// по событию <see cref="Inventory.OnInventoryChanged"/>.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Слоты")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotParent;

    [Header("Связи")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private InventoryDragController dragController;
    [SerializeField] private InventoryTooltipUI tooltip;
    [SerializeField] private InventoryPanelUI panel;
    [SerializeField] private InventoryTheme theme;

    private readonly List<InventorySlotUI> slotViews = new List<InventorySlotUI>();

    /// <summary>Инвентарь, который отображает сетка.</summary>
    public Inventory Inventory => inventory;

    /// <summary>Созданные вьюхи слотов в порядке индексов.</summary>
    public IReadOnlyList<InventorySlotUI> SlotViews => slotViews;

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<Inventory>();
        if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
        if (panel == null) panel = GetComponent<InventoryPanelUI>();
    }

    private void OnEnable()
    {
        if (inventory != null) inventory.OnInventoryChanged += UpdateUI;
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.OnInventoryChanged -= UpdateUI;
    }

    private void Start()
    {
        BuildSlots();
        UpdateUI(inventory);
    }

    /// <summary>Пересоздаёт вьюхи слотов под текущее количество слотов инвентаря.</summary>
    public void BuildSlots()
    {
        if (inventory == null)
        {
            Debug.LogError("[InventoryUI] Не назначен Inventory — сетка не будет создана.");
            return;
        }

        if (slotPrefab == null)
        {
            Debug.LogError("[InventoryUI] Не назначен префаб слота.");
            return;
        }

        if (slotParent == null) slotParent = transform;

        for (int i = slotParent.childCount - 1; i >= 0; i--)
        {
            Destroy(slotParent.GetChild(i).gameObject);
        }

        slotViews.Clear();

        int count = inventory.GetSlotCount();
        for (int i = 0; i < count; i++)
        {
            GameObject instance = Instantiate(slotPrefab, slotParent);
            instance.name = $"Slot_{i:00}";

            InventorySlotUI view = instance.GetComponent<InventorySlotUI>();
            if (view == null)
            {
                Debug.LogError("[InventoryUI] В префабе слота нет InventorySlotUI.");
                continue;
            }

            view.Setup(dragController, tooltip, panel, theme);
            view.Initialize(i, inventory);
            slotViews.Add(view);
        }
    }

    /// <summary>Обновляет все вьюхи по данным инвентаря.</summary>
    public void UpdateUI(Inventory inv)
    {
        if (inv == null) inv = inventory;
        if (inv == null) return;

        int count = Mathf.Min(slotViews.Count, inv.GetSlotCount());
        for (int i = 0; i < count; i++)
        {
            if (slotViews[i] == null) continue;
            slotViews[i].UpdateSlot(inv.GetSlot(i));
        }
    }

    /// <summary>Вьюха слота по индексу или null.</summary>
    public InventorySlotUI GetSlotView(int index)
    {
        if (index < 0 || index >= slotViews.Count) return null;
        return slotViews[index];
    }
}
