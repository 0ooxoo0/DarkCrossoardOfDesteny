using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Фабрика UI культивации в стиле тёмного фэнтези с китайской эстетикой.
/// Всё строится в рантайме под существующим Canvas и не трогает чужие элементы.
/// </summary>
public static class CultivationUIFactory
{
    public static readonly Color Ink = Hex("#0B0C0E");
    public static readonly Color Panel = Hex("#12141A");
    public static readonly Color PanelLight = Hex("#1A1D25");
    public static readonly Color Gold = Hex("#C8A45C");
    public static readonly Color GoldDim = Hex("#6E5A32");
    public static readonly Color TextMain = Hex("#E6E1D6");
    public static readonly Color TextMuted = Hex("#9A958A");
    public static readonly Color Danger = Hex("#B5432F");
    public static readonly Color Success = Hex("#4FA97A");

    private static Sprite roundedSprite;
    private static Sprite radialSprite;
    private static Sprite vignetteSprite;
    private static Sprite silhouetteSprite;
    private static TMP_FontAsset resolvedFont;

    public static TMP_FontAsset Font
    {
        get
        {
            if (resolvedFont == null) resolvedFont = TMP_Settings.defaultFontAsset;
            return resolvedFont;
        }
    }

    public static Color Hex(string html)
    {
        return ColorUtility.TryParseHtmlString(html, out Color color) ? color : Color.white;
    }

    public static Sprite RoundedSprite => roundedSprite != null ? roundedSprite : (roundedSprite = BuildRoundedSprite());
    public static Sprite RadialSprite => radialSprite != null ? radialSprite : (radialSprite = BuildRadialSprite());
    public static Sprite VignetteSprite => vignetteSprite != null ? vignetteSprite : (vignetteSprite = BuildVignetteSprite());
    public static Sprite SilhouetteSprite => silhouetteSprite != null ? silhouetteSprite : (silhouetteSprite = BuildSilhouetteSprite());

    public static Canvas FindOrCreateCanvas()
    {
        Canvas fallback = null;
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);

        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (canvas == null || !canvas.isActiveAndEnabled) continue;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;

            if (canvas.transform.Find("PlayerInfo") != null)
                return canvas;

            if (fallback == null) fallback = canvas;
            else if (canvas.name == "Canvas") fallback = canvas;
        }

        if (fallback != null) return fallback;

        var go = new GameObject("Cultivation Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var created = go.GetComponent<Canvas>();
        created.renderMode = RenderMode.ScreenSpaceOverlay;
        created.sortingOrder = 90;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        EnsureEventSystem();
        return created;
    }

    public static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem", typeof(EventSystem));

        // В проекте используется Input System — ставим его модуль, иначе legacy-фолбэк.
        Type inputModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModule != null) go.AddComponent(inputModule);
        else go.AddComponent<StandaloneInputModule>();

        go.transform.SetAsLastSibling();
    }

    public static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    public static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static Image CreateImage(string name, Transform parent, Color color, Sprite sprite = null, bool sliced = true)
    {
        var rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite != null ? sprite : RoundedSprite;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        return image;
    }

    public static Image CreatePanel(string name, Transform parent, Color color)
    {
        var image = CreateImage(name, parent, color);
        AddGoldBorder(image, 0.45f);
        return image;
    }

    public static void AddGoldBorder(Graphic graphic, float alpha = 0.5f, float thickness = 1.5f)
    {
        var outline = graphic.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(Gold.r, Gold.g, Gold.b, alpha);
        outline.effectDistance = new Vector2(thickness, -thickness);
        outline.useGraphicAlpha = false;
    }

    public static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var rect = CreateRect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (Font != null) text.font = Font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    public static Button CreateButton(string name, Transform parent, string label, Vector2 size, Action onClick,
        Color? accent = null, float fontSize = 24f)
    {
        var rect = CreateRect(name, parent);
        rect.sizeDelta = size;

        var background = rect.gameObject.AddComponent<Image>();
        background.sprite = RoundedSprite;
        background.type = Image.Type.Sliced;
        background.color = PanelLight;

        Color accentColor = accent ?? Gold;
        var outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.5f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        outline.useGraphicAlpha = false;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        var text = CreateText("Label", rect, label, fontSize, TextMain, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        text.margin = new Vector4(10f, 0f, 10f, 0f);

        if (onClick != null)
            button.onClick.AddListener(() => onClick());

        return button;
    }

    public static Slider CreateBar(string name, Transform parent, Color fillColor, out Image background, out Image fill)
    {
        var rect = CreateRect(name, parent);

        background = rect.gameObject.AddComponent<Image>();
        background.sprite = RoundedSprite;
        background.type = Image.Type.Sliced;
        background.color = new Color(0.05f, 0.06f, 0.08f, 0.95f);
        AddGoldBorder(background, 0.3f, 1f);

        var slider = rect.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        var fillArea = CreateRect("Fill Area", rect);
        fillArea.anchorMin = Vector2.zero;
        fillArea.anchorMax = Vector2.one;
        fillArea.offsetMin = new Vector2(3f, 3f);
        fillArea.offsetMax = new Vector2(-3f, -3f);

        var fillRect = CreateRect("Fill", fillArea);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        fill = fillRect.gameObject.AddComponent<Image>();
        fill.sprite = RoundedSprite;
        fill.type = Image.Type.Sliced;
        fill.color = fillColor;
        fill.raycastTarget = false;

        slider.fillRect = fillRect;
        slider.targetGraphic = fill;
        slider.handleRect = null;
        return slider;
    }

    public static Sprite CreateSolidSprite(Color color)
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
        {
            name = "CultivationSolid",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
    }

    public static void DestroyChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
    }

    private static Sprite BuildRoundedSprite()
    {
        const int size = 64;
        const int radius = 16;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "CultivationRounded",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(radius - x, x - (size - 1 - radius)));
                float dy = Mathf.Max(0f, Mathf.Max(radius - y, y - (size - 1 - radius)));
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius - distance);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
    }

    private static Sprite BuildRadialSprite()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "CultivationRadial",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite BuildVignetteSprite()
    {
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "CultivationVignette",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 1f, distance)) * 0.85f;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite BuildSilhouetteSprite()
    {
        const int width = 128;
        const int height = 160;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "CultivationPortrait",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[width * height];
        float centerX = (width - 1) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float headDx = (x - centerX) / 26f;
                float headDy = (y - 116f) / 30f;
                bool head = headDx * headDx + headDy * headDy <= 1f;

                float bodyDx = (x - centerX) / 52f;
                float bodyDy = (y - 18f) / 62f;
                bool body = bodyDy <= 0f && bodyDx * bodyDx + bodyDy * bodyDy <= 1f;

                float alpha = head || body ? 0.92f : 0f;
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f), 100f);
    }
}
