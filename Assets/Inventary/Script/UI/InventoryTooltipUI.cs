using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Всплывающая подсказка: имя, категория, описание и цена предмета.
/// Панель двигается за курсором и не выходит за пределы Canvas.
/// </summary>
public class InventoryTooltipUI : MonoBehaviour
{
    [Header("Корень")]
    [SerializeField] private RectTransform root;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Тексты")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI typeText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI priceText;

    [Header("Оформление")]
    [SerializeField] private Image background;
    [SerializeField] private Image border;
    [SerializeField] private InventoryTheme theme;

    [Header("Позиционирование")]
    [Tooltip("Смещение от курсора в пикселях экрана.")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(24f, -24f);

    private Canvas canvas;
    private RectTransform canvasRect;

    /// <summary>Подсказка сейчас видима.</summary>
    public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.01f;

    private void Awake()
    {
        if (root == null) root = transform as RectTransform;
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvasRect = canvas.rootCanvas.transform as RectTransform;

        ApplyTheme();
        Hide();
    }

    /// <summary>Показывает подсказку для предмета в точке экрана.</summary>
    public void Show(ItemData item, Vector2 screenPosition)
    {
        if (item == null)
        {
            Hide();
            return;
        }

        if (nameText != null) nameText.text = item.DisplayName;
        if (typeText != null) typeText.text = FormatType(item);
        if (descriptionText != null)
        {
            descriptionText.text = string.IsNullOrEmpty(item.description)
                ? "Без описания."
                : item.description;
        }
        if (priceText != null) priceText.text = FormatPrice(item);

        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        Follow(screenPosition);
        transform.SetAsLastSibling();
    }

    /// <summary>Двигает подсказку за курсором, удерживая её внутри Canvas.</summary>
    public void Follow(Vector2 screenPosition)
    {
        if (root == null || canvasRect == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, null, out Vector2 localPoint))
        {
            return;
        }

        Rect canvasBounds = canvasRect.rect;
        Vector2 size = root.rect.size;

        float x = localPoint.x + cursorOffset.x;
        float y = localPoint.y + cursorOffset.y;

        // Переворот у краёв: подсказка не должна уезжать за границу экрана.
        if (x + size.x > canvasBounds.xMax) x = localPoint.x - cursorOffset.x - size.x;
        if (y - size.y < canvasBounds.yMin) y = localPoint.y - cursorOffset.y + size.y;

        x = Mathf.Clamp(x, canvasBounds.xMin, Mathf.Max(canvasBounds.xMin, canvasBounds.xMax - size.x));
        y = Mathf.Clamp(y, canvasBounds.yMin + size.y, canvasBounds.yMax);

        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(x, y);
    }

    /// <summary>Скрывает подсказку.</summary>
    public void Hide()
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void ApplyTheme()
    {
        if (theme == null) return;

        if (background != null) background.color = theme.windowBackground;
        if (border != null) border.color = theme.borderColor;
        if (nameText != null) nameText.color = theme.textColor;
        if (typeText != null) typeText.color = theme.highlightColor;
        if (descriptionText != null) descriptionText.color = theme.mutedTextColor;
        if (priceText != null) priceText.color = theme.highlightColor;

        TMP_FontAsset font = theme.ResolveFont();
        if (font == null) return;

        if (nameText != null) nameText.font = font;
        if (typeText != null) typeText.font = font;
        if (descriptionText != null) descriptionText.font = font;
        if (priceText != null) priceText.font = font;
    }

    private static string FormatType(ItemData item)
    {
        string category = ItemTypeInfo.GetDisplayName(item.type);
        string kind = ItemUseKindInfo.GetDisplayName(item.useKind);
        return string.IsNullOrEmpty(kind) ? category : $"{category} · {kind}";
    }

    private static string FormatPrice(ItemData item)
    {
        return item.sellPrice > 0 ? $"Цена: {item.sellPrice}" : "Цена: —";
    }
}
