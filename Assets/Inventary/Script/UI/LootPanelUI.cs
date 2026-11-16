using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Экранная панель содержимого контейнера лута: список предметов, кнопка
/// «Забрать всё» и строка сообщений. Появляется при наведении на контейнер
/// и следует за ним по экрану.
/// </summary>
public class LootPanelUI : MonoBehaviour
{
    private const string NoSpaceMessage = "В инвентаре нет места.";
    private const string FailedMessage = "Не удалось забрать.";

    [Header("Корень")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform panelRect;

    [Header("Список")]
    [SerializeField] private RectTransform listContent;
    [SerializeField] private LootRowUI rowPrefab;

    [Header("Кнопки и тексты")]
    [SerializeField] private Button takeAllButton;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Ссылки")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private LootPickupController pickupController;

    [Header("Позиция")]
    [Tooltip("Смещение от курсора или контейнера на экране.")]
    [SerializeField] private Vector2 screenOffset = new Vector2(16f, -16f);

    /// <summary>Панель сейчас на экране.</summary>
    public bool IsVisible { get; private set; }

    private WorldLootContainer currentContainer;
    private readonly List<LootRowUI> rows = new List<LootRowUI>();
    private bool initialized;

    private void Awake()
    {
        EnsureInitialized();
        SetVisible(false);
    }

    private void OnEnable()
    {
        if (pickupController != null) pickupController.OnStatus += HandleStatus;
    }

    private void OnDisable()
    {
        if (pickupController != null) pickupController.OnStatus -= HandleStatus;
    }

    private void OnDestroy()
    {
        ClearRows();
        Unsubscribe();
    }

    /// <summary>
    /// Показывает содержимое контейнера рядом с точкой на экране.
    /// Повторный вызов для того же контейнера только перемещает панель.
    /// </summary>
    public void Show(WorldLootContainer container, Vector2 screenPoint)
    {
        EnsureInitialized();

        if (container == null)
        {
            Hide();
            return;
        }

        if (IsVisible && currentContainer == container)
        {
            Reposition(screenPoint);
            return;
        }

        if (currentContainer != container) Unsubscribe();

        currentContainer = container;
        Subscribe();

        Refresh();

        // Refresh мог скрыть панель: контейнер опустел или исчез.
        if (currentContainer == null) return;

        SetVisible(true);
        Reposition(screenPoint);
    }

    /// <summary>Скрывает панель и отписывается от контейнера.</summary>
    public void Hide()
    {
        Unsubscribe();
        currentContainer = null;
        ClearRows();
        SetVisible(false);
    }

    /// <summary>Пересобирает список по текущему контейнеру.</summary>
    public void Refresh()
    {
        EnsureInitialized();
        ClearRows();

        if (currentContainer == null || currentContainer.IsEmpty)
        {
            Hide();
            return;
        }

        if (messageText != null)
        {
            messageText.text = string.Empty;
            messageText.gameObject.SetActive(false);
        }

        if (titleText != null) titleText.text = "Содержимое";

        if (rowPrefab != null && listContent != null)
        {
            IReadOnlyList<LootEntry> entries = currentContainer.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                LootEntry entry = entries[i];

                LootRowUI row = Instantiate(rowPrefab, listContent);
                row.Bind(entry, () => Take(entry));
                rows.Add(row);
            }
        }

        if (takeAllButton != null) takeAllButton.interactable = true;
    }

    /// <summary>Показывает сообщение под списком (нет места, отмена подхода и т. п.).</summary>
    public void ShowMessage(string message)
    {
        if (messageText == null || string.IsNullOrEmpty(message)) return;

        messageText.text = message;
        messageText.gameObject.SetActive(true);
    }

    private void Take(LootEntry entry)
    {
        if (currentContainer == null || pickupController == null)
        {
            ShowMessage(FailedMessage);
            return;
        }

        switch (pickupController.RequestTake(currentContainer, entry))
        {
            case LootTakeResult.Taken:
                Refresh();
                break;

            case LootTakeResult.Walking:
                Hide();
                break;

            case LootTakeResult.NoSpace:
                ShowMessage(NoSpaceMessage);
                break;

            default:
                ShowMessage(FailedMessage);
                break;
        }
    }

    private void TakeAll()
    {
        if (currentContainer == null || pickupController == null)
        {
            ShowMessage(FailedMessage);
            return;
        }

        switch (pickupController.RequestTakeAll(currentContainer))
        {
            case LootTakeResult.Taken:
                Refresh();
                break;

            case LootTakeResult.Walking:
                Hide();
                break;

            case LootTakeResult.NoSpace:
                ShowMessage(NoSpaceMessage);
                break;

            default:
                ShowMessage(FailedMessage);
                break;
        }
    }

    private void HandleStatus(string message)
    {
        Debug.Log("[Loot] " + message);
        if (IsVisible) ShowMessage(message);
    }

    private void HandleChanged(WorldLootContainer container)
    {
        if (container != currentContainer) return;
        Refresh();
    }

    private void HandleEmptied(WorldLootContainer container)
    {
        if (container != currentContainer) return;
        Hide();
    }

    private void Subscribe()
    {
        if (currentContainer == null) return;

        currentContainer.OnChanged += HandleChanged;
        currentContainer.OnEmptied += HandleEmptied;
    }

    private void Unsubscribe()
    {
        if (currentContainer == null) return;

        currentContainer.OnChanged -= HandleChanged;
        currentContainer.OnEmptied -= HandleEmptied;
    }

    private void ClearRows()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] == null) continue;

            // Выключаем сразу: Destroy сработает в конце кадра, и без этого
            // старая и новая строки наложились бы друг на друга.
            rows[i].gameObject.SetActive(false);
            Destroy(rows[i].gameObject);
        }

        rows.Clear();
    }

    private void SetVisible(bool visible)
    {
        IsVisible = visible;

        if (root != null && root.activeSelf != visible) root.SetActive(visible);
        if (canvasGroup == null) return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }

    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        if (root == null) root = gameObject;
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (panelRect == null) panelRect = transform as RectTransform;

        if (takeAllButton != null) takeAllButton.onClick.AddListener(TakeAll);
    }

    private void Reposition(Vector2 screenPoint)
    {
        if (panelRect == null) return;

        RectTransform parent = panelRect.parent as RectTransform;
        if (parent == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, screenPoint, null, out Vector2 local))
        {
            return;
        }

        Vector2 size = panelRect.rect.size;
        Rect parentBounds = parent.rect;

        float x = local.x + screenOffset.x;

        // Справа не помещается — раскрываем влево от точки.
        if (x + size.x > parentBounds.xMax)
            x = local.x - screenOffset.x - size.x;

        float y = local.y + screenOffset.y;

        float minX = parentBounds.xMin;
        float maxX = Mathf.Max(minX, parentBounds.xMax - size.x);
        float minY = parentBounds.yMin + size.y;
        float maxY = parentBounds.yMax;

        panelRect.anchoredPosition = new Vector2(
            Mathf.Clamp(x, minX, maxX),
            Mathf.Clamp(y, minY, Mathf.Max(minY, maxY)));
    }
}
