using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class DropDialogController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private Slider quantitySlider;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Image itemIcon;

    private InventorySlot currentSlot;
    private Action<int> onConfirm;
    private Action onCancel;

    private void Awake()
    {
        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(OnCancel);
        quantitySlider.onValueChanged.AddListener(OnSliderChanged);
    }

    public void Show(InventorySlot slot, Action<int> confirmCallback, Action cancelCallback)
    {
        if (slot?.itemData == null) return;

        currentSlot = slot;
        onConfirm = confirmCallback;
        onCancel = cancelCallback;

        // Настраиваем UI
        itemNameText.text = slot.itemData.itemName;
        itemIcon.sprite = slot.itemData.icon;

        // Настраиваем слайдер
        quantitySlider.minValue = 1;
        quantitySlider.maxValue = slot.quantity;
        quantitySlider.value = slot.quantity;

        UpdateQuantityText((int)quantitySlider.value);

        gameObject.SetActive(true);
    }

    private void OnSliderChanged(float value)
    {
        int intValue = (int)value;
        UpdateQuantityText(intValue);
    }

    private void UpdateQuantityText(int quantity)
    {
        quantityText.text = $"{quantity}/{currentSlot.quantity}";
    }

    private void OnConfirm()
    {
        int dropQuantity = (int)quantitySlider.value;
        onConfirm?.Invoke(dropQuantity);
        Close();
    }

    private void OnCancel()
    {
        onCancel?.Invoke();
        Close();
    }

    private void Close()
    {
        gameObject.SetActive(false);
    }

    // Закрытие диалога по клику на фон
    public void OnBackgroundClick()
    {
        OnCancel();
    }
}