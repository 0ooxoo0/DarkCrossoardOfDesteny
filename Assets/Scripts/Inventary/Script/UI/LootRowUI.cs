using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Строка списка лута: иконка, имя, количество и кнопка «Забрать».
/// Данные приходят через <see cref="Bind"/>, своей логики у строки нет.
/// </summary>
public class LootRowUI : MonoBehaviour
{
    [Header("Визуал")]
    [SerializeField] private Image icon;
    [SerializeField] private Image placeholder;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Кнопка")]
    [SerializeField] private Button takeButton;

    /// <summary>
    /// Заполняет строку записью лута. Иконка без спрайта заменяется заглушкой,
    /// чтобы список не разъезжался.
    /// </summary>
    public void Bind(LootEntry entry, Action onTake)
    {
        bool valid = entry.IsValid;
        gameObject.SetActive(valid);
        if (!valid) return;

        if (nameText != null) nameText.text = entry.item.DisplayName;
        if (countText != null) countText.text = "×" + entry.quantity;

        bool hasIcon = entry.item.icon != null;
        if (icon != null)
        {
            icon.sprite = entry.item.icon;
            icon.enabled = hasIcon;
        }

        if (placeholder != null) placeholder.enabled = !hasIcon;

        if (takeButton != null)
        {
            takeButton.onClick.RemoveAllListeners();
            if (onTake != null) takeButton.onClick.AddListener(() => onTake());
        }
    }
}
