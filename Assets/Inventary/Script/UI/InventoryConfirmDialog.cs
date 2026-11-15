using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Модальное подтверждение внутри панели инвентаря: «Выбросить 5 × Зелье?».
/// Не зависит от устаревшего ConfirmationDialog и его звуков.
/// </summary>
public class InventoryConfirmDialog : MonoBehaviour
{
    [Header("Корень")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Тексты")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Кнопки")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Оформление")]
    [SerializeField] private InventoryTheme theme;

    private Action onConfirm;
    private Action onCancel;

    private void Awake()
    {
        if (root == null) root = gameObject;
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(HandleConfirm);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(HandleCancel);
        }

        ApplyTheme();
        Hide();
    }

    /// <summary>Показывает вопрос и запоминает обработчики.</summary>
    public void Show(string message, Action confirmAction, Action cancelAction = null)
    {
        onConfirm = confirmAction;
        onCancel = cancelAction;

        if (messageText != null) messageText.text = message;

        root.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        transform.SetAsLastSibling();
    }

    /// <summary>Скрывает окно и забывает обработчики.</summary>
    public void Hide()
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        onConfirm = null;
        onCancel = null;
    }

    private void HandleConfirm()
    {
        Action callback = onConfirm;
        Hide();
        callback?.Invoke();
    }

    private void HandleCancel()
    {
        Action callback = onCancel;
        Hide();
        callback?.Invoke();
    }

    private void ApplyTheme()
    {
        if (theme == null) return;
        if (messageText != null) messageText.color = theme.textColor;

        TMP_FontAsset font = theme.ResolveFont();
        if (font != null && messageText != null) messageText.font = font;
    }
}
