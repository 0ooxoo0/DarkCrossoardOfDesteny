using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using System.Collections.Generic;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IEndDragHandler, IDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image background;

    [Header("Item Info")]
    public TextMeshProUGUI DescriptText; 
    public string Descript; // Поле для хранения описания предмета

    [Header("Drag Settings")]
    [SerializeField] private GameObject dragIcon;

    [Header("Sound Settings")]
    private string UsePotionSound = "UsePotionSound";
    private string UseQuestSound = "UseQuestSound";

    private InventorySlot currentSlot;
    private int slotIndex;
    private Inventory inventory;

    private CanvasGroup canvasGroup;
    private Transform originalParent;
    private bool isDragging = false;
    private Vector3 originalPosition;
    private RectTransform rectTransform;

    // Статические события
    public static event Action<InventorySlotUI> OnBeginDragEvent;
    public static event Action<InventorySlotUI> OnEndDragEvent;
    public static event Action<InventorySlotUI> OnDropEvent;
    public static event Action<InventorySlotUI> OnPointerEnterEvent;
    public static event Action<InventorySlotUI> OnPointerExitEvent;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (icon == null) icon = transform.Find("Icon")?.GetComponent<Image>();
        if (quantityText == null) quantityText = transform.Find("QuantityText")?.GetComponent<TextMeshProUGUI>();
        if (background == null) background = GetComponent<Image>();

        FindAndAssignDragIcon();
    }

    private void FindAndAssignDragIcon()
    {
        if (dragIcon != null)
        {
            dragIcon.SetActive(false);
            return;
        }

        dragIcon = transform.Find("DragIcon")?.gameObject;

        if (dragIcon == null)
        {
            Transform parent = transform.parent;
            while (parent != null && dragIcon == null)
            {
                dragIcon = parent.Find("DragIcon")?.gameObject;
                parent = parent.parent;
            }
        }

        if (dragIcon == null)
        {
            GameObject taggedDragIcon = GameObject.FindGameObjectWithTag("DragIcon");
            if (taggedDragIcon != null)
            {
                dragIcon = taggedDragIcon;
            }
        }

        if (dragIcon == null)
        {
            GameObject sceneDragIcon = GameObject.Find("DragIcon");
            if (sceneDragIcon != null)
            {
                dragIcon = sceneDragIcon;
            }
        }

        if (dragIcon != null)
        {
            Image dragImage = dragIcon.GetComponent<Image>();
            if (dragImage == null)
            {
                dragImage = dragIcon.AddComponent<Image>();
            }
            dragImage.raycastTarget = false;

            CanvasGroup dragCanvasGroup = dragIcon.GetComponent<CanvasGroup>();
            if (dragCanvasGroup == null)
            {
                dragCanvasGroup = dragIcon.AddComponent<CanvasGroup>();
            }
            dragCanvasGroup.alpha = 1f;

            dragIcon.SetActive(false);
        }
        else
        {
            Debug.LogWarning($"DragIcon не найдена для слота {name}.");
        }
    }

    public void Initialize(int index, Inventory inv)
    {
        slotIndex = index;
        inventory = inv;
        UpdateSlot(new InventorySlot(null, 0));
    }

    public void UpdateSlot(InventorySlot slot)
    {
        currentSlot = slot;

        // Обновляем описание при обновлении слота
        if (slot?.itemData != null)
        {
            Descript = slot.itemData.description;
        }
        else
        {
            Descript = "";
        }

        if (icon != null)
        {
            if (slot?.itemData != null)
            {
                icon.sprite = slot.itemData.icon;
                icon.color = Color.white;
            }
            else
            {
                icon.sprite = null;
                icon.color = Color.clear;
            }
        }

        if (quantityText != null)
        {
            if (slot?.itemData != null && slot.quantity > 1)
            {
                quantityText.text = slot.quantity.ToString();
                quantityText.gameObject.SetActive(true);
            }
            else
            {
                quantityText.text = "";
                quantityText.gameObject.SetActive(false);
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 2)
        {
            UseItem();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentSlot?.itemData == null) return;

        isDragging = true;

        if (dragIcon != null)
        {
            dragIcon.SetActive(true);

            Image dragImage = dragIcon.transform.GetChild(0).GetComponent<Image>();
            if (dragImage != null && icon != null)
            {
                dragImage.sprite = icon.sprite;
                dragImage.color = Color.white;
            }

            RectTransform dragRect = dragIcon.GetComponent<RectTransform>();
            dragRect.position = eventData.position;
            dragRect.SetAsLastSibling();
        }
        else
        {
            Debug.LogError("DragIcon не назначена!");
            return;
        }

        canvasGroup.alpha = 0.3f;
        canvasGroup.blocksRaycasts = false;

        originalParent = transform.parent;
        originalPosition = rectTransform.anchoredPosition;

        OnBeginDragEvent?.Invoke(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || dragIcon == null) return;

        dragIcon.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        if (dragIcon != null)
        {
            dragIcon.SetActive(false);
        }

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        isDragging = false;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        InventorySlotUI targetSlot = null;
        GameObject trashArea = null;

        foreach (RaycastResult result in results)
        {
            if (targetSlot == null)
                targetSlot = result.gameObject.GetComponent<InventorySlotUI>();
            if (trashArea == null)
                trashArea = result.gameObject;

            if (targetSlot != null && trashArea != null) break;
        }

        if (targetSlot != null && targetSlot != this)
        {
            Debug.Log($"Moving item from slot {slotIndex} to slot {targetSlot.slotIndex}");
            inventory.MoveItem(slotIndex, targetSlot.slotIndex);
        }
        else if (trashArea.tag == "TrashArea")
        {
            ShowDropConfirmation();
        }
        else
        {
            ReturnToOriginalPosition();
        }

        OnEndDragEvent?.Invoke(this);
    }

    public void OnDrop(PointerEventData eventData)
    {
        OnDropEvent?.Invoke(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // При наведении обновляем описание из текущего слота
        if (currentSlot?.itemData != null)
        {
            Descript = currentSlot.itemData.description;
            if (DescriptText==null)
            {
                SoundManager.Instance.PlaySound("Move", 0.3f);
                DescriptText = GameObject.FindGameObjectWithTag("DescriptObject").GetComponent<TextMeshProUGUI>();
                DescriptText.text = Descript;
            }
            else
            {
                SoundManager.Instance.PlaySound("Move", 0.3f);
                DescriptText.text = Descript;
            }
        }

        OnPointerEnterEvent?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (DescriptText == null)
        {
            DescriptText = GameObject.FindGameObjectWithTag("DescriptObject").GetComponent<TextMeshProUGUI>();
            DescriptText.text = "";
        }
        else
        {
            DescriptText.text = "";
        }
            OnPointerExitEvent?.Invoke(this);
    }

    private void UseItem()
    {
        if (currentSlot?.itemData != null && inventory != null)
        {
            Debug.Log($"Using item {currentSlot.itemData.itemName}");

            switch (currentSlot.itemData.type)
            {
                case ItemType.Potion:
                    PlaySound(UsePotionSound);
                    inventory.RemoveItem(slotIndex, 1);
                    break;
                case ItemType.Weapon:
                    Debug.Log($"Equipping weapon: {currentSlot.itemData.itemName}");
                    break;
                case ItemType.Quest:
                    PlaySound(UseQuestSound);
                    inventory.RemoveItem(slotIndex, 1);
                    break;
            }
        }
    }

    private void ReturnToOriginalPosition()
    {
        transform.SetParent(originalParent);
        rectTransform.anchoredPosition = originalPosition;
    }

    public void TryRemoveItem()
    {
        if (currentSlot?.itemData != null)
        {
            ShowDropConfirmation();
        }
        else
        {
            ReturnToOriginalPosition();
        }
    }

    private void ShowDropConfirmation()
    {
        if (currentSlot?.itemData != null)
        {
            ConfirmationDialog.ShowDialog(
                "Выбросить предмет",
                $"Вы уверены, что хотите выбросить {currentSlot.itemData.itemName}?",
                () => {
                    OnDropConfirmed(currentSlot.quantity);
                },
                () => {
                    ReturnToOriginalPosition();
                }
            );
        }
        else
        {
            ReturnToOriginalPosition();
        }
    }

    private void OnDropConfirmed(int quantity)
    {
        if (inventory != null && currentSlot?.itemData != null)
        {
            Debug.Log($"Dropping {quantity} of {currentSlot.itemData.itemName}");
            inventory.RemoveItem(slotIndex, quantity);
        }
    }

    private void PlaySound(string soundName)
    {
        if (SoundManager.Instance != null && !string.IsNullOrEmpty(soundName))
        {
            SoundManager.Instance.PlaySound(soundName);
        }
        else
        {
            Debug.LogWarning($"SoundManager not found or sound name is empty: {soundName}");
        }
    }

    // Public методы для доступа из других скриптов
    public int GetSlotIndex() => slotIndex;
    public InventorySlot GetSlot() => currentSlot;
    public Inventory GetInventory() => inventory;

    // Методы для получения информации о предмете
    public string GetItemDescription()
    {
        // Возвращаем описание из поля Descript
        return Descript;
    }

    public string GetItemName()
    {
        return currentSlot?.itemData?.itemName ?? "";
    }

    public bool HasItem()
    {
        return currentSlot?.itemData != null;
    }

    // Метод для принудительного обновления описания
    public void UpdateDescription(TextMeshProUGUI text)
    {
        DescriptText = text;
        if (currentSlot?.itemData != null)
        {
            Descript = currentSlot.itemData.description;
            if (text != null)
                text.text = Descript;
            else
                Debug.LogWarning("DescriptText == Null");
        }
        else
        {
            Descript = "";
        }
    }
}