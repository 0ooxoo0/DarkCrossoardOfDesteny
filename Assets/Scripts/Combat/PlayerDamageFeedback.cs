using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Фидбек получения урона игроком: красная вспышка на весь экран и мигание модели
/// в кадрах неуязвимости. Работает без ссылок в сцене — Canvas строится кодом,
/// ссылки ищутся на игроке. Время не замедляет: <c>Time.timeScale</c> не трогает.
/// </summary>
[DisallowMultipleComponent]
public class PlayerDamageFeedback : MonoBehaviour
{
    /// <summary>Порядок оверлея: выше HUD, ниже экрана смерти.</summary>
    private const int FlashSortingOrder = 500;

    [Header("Вспышка")]
    [Tooltip("Цвет вспышки при получении урона.")]
    [SerializeField] private Color flashColor = new Color(1f, 0.15f, 0.12f, 1f);
    [Tooltip("Длительность вспышки, секунд.")]
    [SerializeField, Min(0.01f)] private float flashDuration = 0.35f;
    [Tooltip("Максимальная прозрачность вспышки: 1 — заливает экран полностью.")]
    [SerializeField, Range(0f, 1f)] private float flashAlpha = 0.28f;

    [Header("Мигание в i-frames")]
    [Tooltip("Период мигания модели, секунд.")]
    [SerializeField, Min(0.02f)] private float blinkInterval = 0.09f;
    [Tooltip("Автомигание после удара: 0 — не мигать, мигание зовут вручную через StartBlink.")]
    [SerializeField, Min(0f)] private float invulnerabilityBlinkDuration = 0f;

    [Header("Ссылки")]
    [Tooltip("Здоровье игрока. Пусто — ищется на этом объекте.")]
    [SerializeField] private PlayerHealth playerHealth;
    [Tooltip("Animator игрока. Оставлен для совместимости с префабом.")]
    [SerializeField] private Animator animator;
    [Tooltip("Картинка вспышки. Пусто — создаётся кодом на отдельном Canvas.")]
    [SerializeField] private Image flashImage;

    private Canvas createdCanvas;
    private Renderer[] cachedRenderers;
    private bool initialized;
    private bool subscribed;
    private float flashRemaining;
    private float blinkRemaining;
    private float blinkTimer;
    private bool renderersVisible = true;

    /// <summary>Идёт ли вспышка прямо сейчас.</summary>
    internal bool IsFlashActiveForTests => flashRemaining > 0f;

    /// <summary>Текущая альфа вспышки.</summary>
    internal float FlashAlphaForTests => flashImage != null ? flashImage.color.a : 0f;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void OnDisable()
    {
        Unsubscribe();

        // Выключенный компонент не тикает: рендереры иначе остались бы погашенными.
        StopBlink();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (createdCanvas != null)
            Destroy(createdCanvas.gameObject);
    }

    /// <summary>
    /// Идемпотентная инициализация: поиск ссылок, постройка UI и подписка на здоровье.
    /// В EditMode Unity не вызывает <c>Awake</c>, поэтому тесты входят этим же путём, что и игра.
    /// </summary>
    internal void EnsureInitialized()
    {
        if (!initialized)
        {
            initialized = true;

            if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            BuildFlashUI();
        }

        Subscribe();
    }

    /// <summary>
    /// Подписка на события здоровья. Респавн всегда снимает мигание:
    /// иначе после смерти рендереры могли остаться выключенными.
    /// </summary>
    private void Subscribe()
    {
        if (subscribed || playerHealth == null) return;
        subscribed = true;

        playerHealth.OnDamaged += HandleDamaged;
        playerHealth.OnRespawned += StopBlink;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;

        if (playerHealth == null) return;

        playerHealth.OnDamaged -= HandleDamaged;
        playerHealth.OnRespawned -= StopBlink;
    }

    /// <summary>
    /// Строит Canvas с одним Image на весь экран. Спрайт не задаётся: сплошная
    /// заливка низкой альфой дешевле процедурной виньетки и не тянет чужой модуль.
    /// </summary>
    private void BuildFlashUI()
    {
        if (flashImage == null)
        {
            var canvasObject = new GameObject("PlayerDamageFlashCanvas", typeof(Canvas));
            createdCanvas = canvasObject.GetComponent<Canvas>();
            createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            createdCanvas.sortingOrder = FlashSortingOrder;

            var imageObject = new GameObject("DamageFlash", typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);

            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            flashImage = imageObject.GetComponent<Image>();
        }

        flashImage.raycastTarget = false;
        flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        flashImage.enabled = false;
    }

    /// <summary>
    /// Тикает на unscaled-времени: вспышка и мигание должны идти и на паузе,
    /// а <c>Time.timeScale</c> компонент не трогает вообще.
    /// </summary>
    private void Update()
    {
        Tick(Time.unscaledDeltaTime);
    }

    /// <summary>Прогон вспышки и мигания для тестов: в EditMode Update не тикает.</summary>
    internal void TickForTests(float unscaledDeltaSeconds)
    {
        Tick(unscaledDeltaSeconds);
    }

    private void Tick(float unscaledDeltaSeconds)
    {
        if (flashRemaining > 0f)
        {
            flashRemaining = Mathf.Max(0f, flashRemaining - unscaledDeltaSeconds);
            RefreshFlashAlpha();
        }

        UpdateBlinkFromInvulnerability();

        if (blinkRemaining <= 0f) return;

        blinkRemaining = Mathf.Max(0f, blinkRemaining - unscaledDeltaSeconds);
        if (blinkRemaining <= 0f)
        {
            StopBlink();
            return;
        }

        blinkTimer -= unscaledDeltaSeconds;
        if (blinkTimer > 0f) return;

        blinkTimer = Mathf.Max(0.01f, blinkInterval);
        renderersVisible = !renderersVisible;
        ApplyRenderersVisible();
    }

    private void RefreshFlashAlpha()
    {
        if (flashImage == null) return;

        float ratio = flashRemaining <= 0f ? 0f : Mathf.Clamp01(flashRemaining / Mathf.Max(0.01f, flashDuration));
        float alpha = flashAlpha * flashColor.a * ratio;

        flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);
        flashImage.enabled = alpha > 0.001f;
    }

    private void HandleDamaged(DamageInfo info)
    {
        EnsureInitialized();

        flashRemaining = Mathf.Max(0.01f, flashDuration);
        RefreshFlashAlpha();

        if (invulnerabilityBlinkDuration > 0f)
            StartBlink(invulnerabilityBlinkDuration);
    }

    /// <summary>
    /// Мигание в i-frames: пока PlayerHealth неуязвим (кувырок или респавн),
    /// модель мигает — игрок видит, что урон в этот момент не проходит.
    /// </summary>
    private void UpdateBlinkFromInvulnerability()
    {
        if (playerHealth == null) return;

        if (playerHealth.IsInvulnerable)
        {
            if (blinkRemaining <= 0f) StartBlink(0.2f);
            return;
        }

        if (blinkRemaining > 0f) StopBlink();
    }

    /// <summary>Включает мигание модели на указанное время. Повторный вызов продлевает его.</summary>
    public void StartBlink(float duration)
    {
        EnsureInitialized();

        if (duration <= 0f)
        {
            StopBlink();
            return;
        }

        blinkRemaining = duration;
        blinkTimer = Mathf.Max(0.01f, blinkInterval);
        renderersVisible = true;
        ApplyRenderersVisible();
    }

    /// <summary>Снимает мигание и обязательно возвращает все рендереры включёнными.</summary>
    public void StopBlink()
    {
        blinkRemaining = 0f;
        blinkTimer = 0f;
        renderersVisible = true;
        ApplyRenderersVisible();
    }

    private void ApplyRenderersVisible()
    {
        if (cachedRenderers == null) return;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = renderersVisible;
        }
    }
}
