using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Панель инвентаря в игровой сцене: открытие по клавише, пауза,
/// кнопки «Сортировать», «Выбросить», «Использовать» и выбранный слот.
/// Сама логика предметов живёт в <see cref="Inventory"/>.
/// </summary>
public class InventoryPanelUI : MonoBehaviour
{
    [Header("Окно")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private Inventory inventory;
    [SerializeField] private InventoryTooltipUI tooltip;
    [SerializeField] private InventoryDragController dragController;
    [SerializeField] private InventoryConfirmDialog confirmDialog;
    [SerializeField] private InventoryTheme theme;

    [Header("Экипировка")]
    [SerializeField] private List<EquipmentSlotUI> equipmentSlots = new List<EquipmentSlotUI>();

    [Header("Кнопки")]
    [SerializeField] private Button sortButton;
    [SerializeField] private Button dropButton;
    [SerializeField] private Button useButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI footerText;

    [Header("Управление")]
    [Tooltip("Клавиша открытия и закрытия панели.")]
    [SerializeField] private Key toggleKey = Key.I;

    [Tooltip("Ставить игру на паузу, пока панель открыта.")]
    [SerializeField] private bool pauseWhileOpen = true;

    [Tooltip("Компоненты, которые отключаются на время открытой панели (игрок, взаимодействие).")]
    [SerializeField] private List<MonoBehaviour> disableWhileOpen = new List<MonoBehaviour>();

    [Tooltip("Выбрасывать весь стак или одну единицу.")]
    [SerializeField] private bool dropWholeStack = true;

    private readonly List<bool> disablePreviousStates = new List<bool>();

    private int selectedIndex = -1;
    private bool isOpen;
    private float previousTimeScale = 1f;
    private CursorLockMode previousLockMode = CursorLockMode.None;
    private bool previousCursorVisible;

    /// <summary>Открыта ли панель прямо сейчас.</summary>
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<Inventory>();
        if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
        if (inventoryUI == null) inventoryUI = GetComponentInChildren<InventoryUI>(true);
        if (tooltip == null) tooltip = GetComponentInChildren<InventoryTooltipUI>(true);
        if (dragController == null) dragController = GetComponentInChildren<InventoryDragController>(true);

        if (sortButton != null) sortButton.onClick.AddListener(SortInventory);
        if (dropButton != null) dropButton.onClick.AddListener(DropSelected);
        if (useButton != null) useButton.onClick.AddListener(UseSelected);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (windowRoot != null) windowRoot.SetActive(false);
        ApplyTheme();
        UpdateButtons();
    }

    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged += HandleInventoryChanged;
            inventory.OnItemEquipped += HandleItemEquipped;
            inventory.OnItemUnequipped += HandleItemUnequipped;
        }
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= HandleInventoryChanged;
            inventory.OnItemEquipped -= HandleItemEquipped;
            inventory.OnItemUnequipped -= HandleItemUnequipped;
        }
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            Toggle();
            return;
        }

        if (isOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    private void OnDestroy()
    {
        // Чтобы выход из сцены не оставил игру на паузе.
        if (isOpen) RestoreGameState();
    }

    /// <summary>Открывает панель, если она закрыта, и наоборот.</summary>
    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    /// <summary>Открывает панель: показывает окно, обновляет данные и ставит игру на паузу.</summary>
    public void Open()
    {
        if (isOpen) return;
        isOpen = true;

        previousTimeScale = Time.timeScale;
        previousLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        if (windowRoot != null) windowRoot.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pauseWhileOpen) Time.timeScale = 0f;

        SetControlsEnabled(false);
        ClearSelection();
        RefreshViews();
        UpdateButtons();
    }

    /// <summary>Закрывает панель и возвращает игру в исходное состояние.</summary>
    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        dragController?.EndDrag();
        tooltip?.Hide();
        confirmDialog?.Hide();
        ClearSelection();

        if (windowRoot != null) windowRoot.SetActive(false);
        RestoreGameState();
    }

    /// <summary>Выделяет слот для кнопок действий.</summary>
    public void SelectSlot(InventorySlotUI slot)
    {
        if (slot == null) return;
        if (selectedIndex == slot.SlotIndex)
        {
            UpdateButtons();
            return;
        }

        InventorySlotUI previous = inventoryUI != null ? inventoryUI.GetSlotView(selectedIndex) : null;
        if (previous != null) previous.SetSelected(false);

        selectedIndex = slot.SlotIndex;
        slot.SetSelected(true);

        UpdateButtons();
        UpdateFooter();
    }

    /// <summary>Сортирует сетку по типу и имени.</summary>
    public void SortInventory()
    {
        if (inventory == null) return;

        inventory.SortByTypeAndName();

        // После сортировки индексы поменялись — выделение сбрасываем.
        ClearSelection();
        RefreshViews();
    }

    /// <summary>Удаляет выбранный предмет с подтверждением.</summary>
    public void DropSelected()
    {
        if (inventory == null) return;

        InventorySlot slot = inventory.GetSlot(selectedIndex);
        if (slot == null || slot.IsEmpty) return;

        ItemData item = slot.itemData;
        int quantity = dropWholeStack ? slot.quantity : 1;
        int index = selectedIndex;

        string message = $"Выбросить {quantity} × {item.DisplayName}?";

        if (confirmDialog != null)
        {
            confirmDialog.Show(message, () => DropItem(index, quantity));
        }
        else
        {
            // Без окна подтверждения не выбрасываем: защита от случайного удаления.
            Debug.LogWarning("[InventoryPanelUI] Нет InventoryConfirmDialog — выброс отменён.");
        }
    }

    /// <summary>Просит применить выбранный предмет.</summary>
    public void UseSelected()
    {
        if (inventory == null) return;

        InventorySlot slot = inventory.GetSlot(selectedIndex);
        if (slot == null || slot.IsEmpty) return;

        if (!inventory.RequestUse(selectedIndex))
        {
            SetFooter($"«{slot.itemData.DisplayName}» пока нельзя применить.");
        }

        UpdateButtons();
    }

    private void DropItem(int index, int quantity)
    {
        inventory?.RemoveItem(index, quantity);
        ClearSelection();
        RefreshViews();
    }

    private void RestoreGameState()
    {
        SetControlsEnabled(true);

        if (pauseWhileOpen) Time.timeScale = previousTimeScale;

        Cursor.lockState = previousLockMode;
        Cursor.visible = previousCursorVisible;
    }

    /// <summary>Отключает и возвращает управление игроком на время панели.</summary>
    private void SetControlsEnabled(bool enabled)
    {
        if (disableWhileOpen == null) return;

        if (enabled)
        {
            for (int i = 0; i < disableWhileOpen.Count && i < disablePreviousStates.Count; i++)
            {
                if (disableWhileOpen[i] != null) disableWhileOpen[i].enabled = disablePreviousStates[i];
            }

            disablePreviousStates.Clear();
            return;
        }

        disablePreviousStates.Clear();
        for (int i = 0; i < disableWhileOpen.Count; i++)
        {
            MonoBehaviour component = disableWhileOpen[i];
            if (component == null) continue;

            disablePreviousStates.Add(component.enabled);
            component.enabled = false;
        }
    }

    private void HandleInventoryChanged(Inventory changed)
    {
        UpdateButtons();
        UpdateFooter();
    }

    private void HandleItemEquipped(ItemData item, EquipmentSlotType slotType)
    {
        UpdateButtons();
        UpdateFooter();
    }

    private void HandleItemUnequipped(ItemData item, EquipmentSlotType slotType)
    {
        UpdateButtons();
        UpdateFooter();
    }

    private void RefreshViews()
    {
        inventoryUI?.UpdateUI(inventory);

        if (equipmentSlots == null) return;
        for (int i = 0; i < equipmentSlots.Count; i++)
        {
            if (equipmentSlots[i] != null) equipmentSlots[i].Refresh();
        }
    }

    private void ClearSelection()
    {
        if (inventoryUI != null && selectedIndex >= 0)
        {
            InventorySlotUI view = inventoryUI.GetSlotView(selectedIndex);
            if (view != null) view.SetSelected(false);
        }

        selectedIndex = -1;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool hasSelection = false;

        if (inventory != null && selectedIndex >= 0)
        {
            InventorySlot slot = inventory.GetSlot(selectedIndex);
            hasSelection = slot != null && !slot.IsEmpty;
        }

        if (dropButton != null) dropButton.interactable = hasSelection;
        if (useButton != null) useButton.interactable = hasSelection;
        if (sortButton != null) sortButton.interactable = inventory != null;

        UpdateFooter();
    }

    private void UpdateFooter()
    {
        if (footerText == null) return;

        if (inventory == null || selectedIndex < 0)
        {
            SetFooter("ЛКМ — выбрать · двойной клик — использовать · I — закрыть");
            return;
        }

        InventorySlot slot = inventory.GetSlot(selectedIndex);
        if (slot == null || slot.IsEmpty)
        {
            SetFooter("ЛКМ — выбрать · двойной клик — использовать · I — закрыть");
            return;
        }

        SetFooter($"{slot.itemData.DisplayName} · {ItemTypeInfo.GetDisplayName(slot.itemData.type)} · ×{slot.quantity}");
    }

    private void SetFooter(string text)
    {
        if (footerText != null) footerText.text = text;
    }

    private void ApplyTheme()
    {
        if (theme == null) return;

        if (footerText != null) footerText.color = theme.mutedTextColor;

        TMP_FontAsset font = theme.ResolveFont();
        if (font != null && footerText != null) footerText.font = font;
    }
}
