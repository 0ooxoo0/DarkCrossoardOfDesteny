using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class ConfirmationDialog : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Sound Settings")]
    [SerializeField] private string openSound = "OpenWindow";
    [SerializeField] private string confirmSound = "Crushed";
    [SerializeField] private string cancelSound = "OpenWindow";

    private Action onConfirm;
    private Action onCancel;

    private CanvasGroup canvasGroup;
    private bool isInitialized = false;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        // Находим компоненты если они не назначены в инспекторе
        if (dialogPanel == null)
        {
            // Ищем дочернюю панель или используем сам объект
            Transform panelTransform = transform.Find("Panel");
            if (panelTransform != null)
                dialogPanel = panelTransform.gameObject;
            else
                dialogPanel = gameObject;
        }

        if (messageText == null) messageText = transform.Find("MessageText")?.GetComponent<TextMeshProUGUI>();
        if (confirmButton == null) confirmButton = transform.Find("ConfirmButton")?.GetComponent<Button>();
        if (cancelButton == null) cancelButton = transform.Find("CancelButton")?.GetComponent<Button>();

        // Настраиваем кнопки
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirm);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancel);
        }


        // Получаем или добавляем CanvasGroup
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Гарантируем, что диалог скрыт при старте
        HideImmediate();

        isInitialized = true;
    }

    private void Start()
    {
        // Дополнительная проверка в Start
        if (dialogPanel != null && dialogPanel.activeSelf)
        {
            HideImmediate();
        }
    }

    public void Show(string title, string message, Action confirmAction, Action cancelAction = null)
    {
        Initialize(); // Убедимся, что инициализирован

        // Устанавливаем текст
        //if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;

        // Сохраняем колбэки
        onConfirm = confirmAction;
        onCancel = cancelAction;

        // Активируем диалог
        if (dialogPanel != null)
        {
            Debug.Log("DialogPanel активировалась");
            dialogPanel.SetActive(true);
        }
        else
        {
            Debug.Log("DialogPanel неактивировалась, dialogPanel == Null");
        }
            gameObject.SetActive(true);

        // Включаем интерактивность
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        Debug.Log("Alpha == " + canvasGroup.alpha);

        // Поднимаем на верхний слой
        transform.SetAsLastSibling();

        // Воспроизводим звук открытия
        SoundManager.Instance.PlaySound(openSound);

        Debug.Log("ConfirmationDialog shown: " + message);
    }

    private void OnConfirm()
    {
        Debug.Log("ConfirmationDialog confirmed");

        // Воспроизводим звук подтверждения

        SoundManager.Instance.PlaySound(confirmSound);

        onConfirm?.Invoke();
        Hide();
    }

    private void OnCancel()
    {
        Debug.Log("ConfirmationDialog cancelled");

        // Воспроизводим звук отмены
        SoundManager.Instance.PlaySound(cancelSound);

        onCancel?.Invoke();
        Hide();
        Debug.Log("HideOnCanale");
    }

    private void Hide()
    {
        Debug.Log("Hide");
        // Отключаем интерактивность
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // Деактивируем объекты
        //if (dialogPanel != null)
            //dialogPanel.SetActive(false);

        //gameObject.SetActive(false);

        // Очищаем колбэки
        onConfirm = null;
        onCancel = null;
    }

    private void HideImmediate()
    {
        // Немедленное скрытие без анимации
        if (canvasGroup != null)
        {
            Debug.Log("Hide1");
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        //if (dialogPanel != null)
           // dialogPanel.SetActive(false);

        //gameObject.SetActive(false);

        onConfirm = null;
        onCancel = null;
    }

    // Для закрытия по клику на фон
    public void OnBackgroundClick()
    {
        OnCancel();
    }

    // Статический метод для легкого доступа
    private static ConfirmationDialog instance;
    public static ConfirmationDialog Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ConfirmationDialog>(true);
                if (instance == null)
                {
                    Debug.LogError("ConfirmationDialog not found in scene!");
                }
            }
            return instance;
        }
    }

    // Статический метод для показа диалога
    public static void ShowDialog(string title, string message, Action confirmAction, Action cancelAction = null)
    {
        if (Instance != null)
        {
            Instance.Show(title, message, confirmAction, cancelAction);
        }
        else
        {
            Debug.LogError("Cannot show dialog - ConfirmationDialog instance not found!");
            // Резервный вариант - сразу подтверждаем
            confirmAction?.Invoke();
        }
    }
}