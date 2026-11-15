using TMPro;
using UnityEngine;

/// <summary>
/// Палитра и шрифт интерфейса инвентаря. Все цвета вынесены в ассет,
/// чтобы не держать «магические» значения в коде и легко менять тему.
/// </summary>
[CreateAssetMenu(fileName = "InventoryTheme", menuName = "Inventory/Theme")]
public class InventoryTheme : ScriptableObject
{
    [Header("Фон")]
    [Tooltip("#1A1612 — основной тёмный фон окна.")]
    public Color windowBackground = new Color(0.102f, 0.086f, 0.071f, 0.97f);

    [Tooltip("Затемнение под окном.")]
    public Color dimBackground = new Color(0f, 0f, 0f, 0.72f);

    [Tooltip("Фон пустого слота.")]
    public Color slotBackground = new Color(0.063f, 0.055f, 0.043f, 1f);

    [Header("Рамки и подсветка")]
    [Tooltip("#4A3F35 — рамка слота и окна.")]
    public Color borderColor = new Color(0.290f, 0.247f, 0.208f, 1f);

    [Tooltip("#C9A55C — подсветка выбранного слота и акцентов.")]
    public Color highlightColor = new Color(0.788f, 0.647f, 0.361f, 1f);

    [Header("Текст")]
    [Tooltip("#E8DCC8 — основной цвет текста.")]
    public Color textColor = new Color(0.910f, 0.863f, 0.784f, 1f);

    [Tooltip("Приглушённый текст: подписи слотов, цена.")]
    public Color mutedTextColor = new Color(0.639f, 0.584f, 0.502f, 1f);

    [Header("Заглушки")]
    [Tooltip("Цвет квадрата, если у предмета не назначена иконка.")]
    public Color placeholderColor = new Color(0.404f, 0.243f, 0.243f, 1f);

    [Header("Шрифт")]
    [Tooltip("Если пусто — используется шрифт TMP по умолчанию.")]
    public TMP_FontAsset font;

    [Header("Размеры")]
    public Vector2 slotSize = new Vector2(88f, 88f);
    public Vector2 slotSpacing = new Vector2(8f, 8f);

    /// <summary>Шрифт темы или шрифт TMP по умолчанию.</summary>
    public TMP_FontAsset ResolveFont()
    {
        if (font != null) return font;
        return TMP_Settings.defaultFontAsset;
    }
}
