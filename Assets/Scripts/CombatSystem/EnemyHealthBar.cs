using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Полоса HP врага в world-space. Вся иерархия UI (Canvas, CanvasGroup, подложка и заливка)
/// строится кодом при инициализации: префаб и ссылки в сцене не нужны. Источник данных —
/// <see cref="EnemyHealth"/>: полоса появляется при первом уроне, сама затухает после
/// <c>hideDelay</c> секунд покоя и скрывается после смерти врага.
/// </summary>
[DisallowMultipleComponent]
public class EnemyHealthBar : MonoBehaviour
{
    /// <summary>Порядок отрисовки world-space canvas: поверх геометрии сцены.</summary>
    private const int SortingOrder = 100;

    [Header("Геометрия")]
    [Tooltip("Смещение полосы над точкой врага, в метрах.")]
    [SerializeField] private float worldOffset = 2.1f;
    [Tooltip("Размер полосы в метрах (ширина x высота): у world-space canvas 1 unit = 1 метр.")]
    [SerializeField] private Vector2 barSize = new Vector2(0.9f, 0.1f);

    [Header("Тайминги")]
    [Tooltip("Секунд без урона, после которых полоса начинает затухать.")]
    [SerializeField, Min(0f)] private float hideDelay = 3f;
    [Tooltip("Скорость затухания, единиц альфы в секунду.")]
    [SerializeField, Min(0f)] private float fadeSpeed = 2f;

    [Header("Цвета")]
    [Tooltip("Цвет подложки полосы.")]
    [SerializeField] private Color backgroundColor = new Color(0.05f, 0.06f, 0.08f, 0.95f);
    [Tooltip("Цвет заливки HP.")]
    [SerializeField] private Color fillColor = new Color(0.78f, 0.25f, 0.18f);

    /// <summary>Общий белый спрайт: Image без спрайта не рисует ничего.</summary>
    private static Sprite sharedWhiteSprite;

    private EnemyHealth health;
    private Transform followTarget;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform canvasRect;
    private Image fillImage;
    private Camera viewCamera;

    private bool built;
    private bool subscribed;
    private bool shown;
    private bool dead;
    private float idleTimer;

    /// <summary>Точка, за которой следует полоса: явная цель, иначе сам враг.</summary>
    private Transform Anchor => followTarget != null ? followTarget : (health != null ? health.transform : null);

    /// <summary>Логическое состояние полосы для EditMode-тестов.</summary>
    internal bool IsVisibleForTests => shown;

    /// <summary>Прогон автоскрытия без <see cref="Update"/> — для EditMode-тестов.</summary>
    internal void TickForTests(float deltaSeconds)
    {
        Tick(deltaSeconds);
    }

    private void Awake()
    {
        EnsureBuilt();

        if (health == null) health = GetComponentInParent<EnemyHealth>();
        Subscribe();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    /// <summary>
    /// Подключает полосу к здоровью врага: строит UI (ровно один раз) и подписывается
    /// на события. EditMode-тесты входят этим же путём, потому что Unity не вызывает
    /// <c>Awake</c> вне play mode.
    /// </summary>
    public void Initialize(EnemyHealth enemyHealth, Transform followTarget)
    {
        EnsureBuilt();

        if (health != enemyHealth)
        {
            Unsubscribe();
            health = enemyHealth;
        }

        this.followTarget = followTarget;
        dead = false;
        Subscribe();
        Hide();
    }

    /// <summary>
    /// Обновляет заливку и видимость полосы. Подписан на
    /// <see cref="EnemyHealth.OnHealthChanged"/> (current, max).
    /// </summary>
    public void UpdateHealth(float current, float max)
    {
        if (dead) return;

        if (fillImage != null && max > 0f)
            fillImage.fillAmount = Mathf.Clamp01(current / max);

        // Полоса появляется только когда HP реально просело ниже максимума: стартовое
        // событие EnemyHealth приходит с current == max и показывать полосу не должно.
        if (current < max) Show();
    }

    /// <summary>Скрыть полосу немедленно (смерть врага или ручной вызов).</summary>
    public void Hide()
    {
        shown = false;
        idleTimer = 0f;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    /// <summary>Смерть врага: полоса больше не нужна.</summary>
    private void HandleDied()
    {
        dead = true;
        Hide();
    }

    /// <summary>Подписка идемпотентна: её делят Awake, OnEnable и Initialize.</summary>
    private void Subscribe()
    {
        if (subscribed || health == null) return;
        subscribed = true;

        health.OnHealthChanged += UpdateHealth;
        health.OnDied += HandleDied;

        // Стартовое событие EnemyHealth уходит из его Awake и может опередить подписку:
        // синхронизируем заливку вручную, иначе полоса осталась бы полной.
        UpdateHealth(health.CurrentHealth, health.MaxHealth);
    }

    private void Unsubscribe()
    {
        subscribed = false;
        if (health == null) return;

        health.OnHealthChanged -= UpdateHealth;
        health.OnDied -= HandleDied;
    }

    /// <summary>Строит UI ровно один раз; повторные вызовы ничего не делают.</summary>
    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        var canvasObject = new GameObject("EnemyHealthBar_Canvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.localPosition = Vector3.zero;
        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale = Vector3.one;
        canvasRect.sizeDelta = barSize; // метры: при localScale 1 полоса ~1 метр шириной

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = SortingOrder;

        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f; // скрыта до первого урона
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        CreateImage("Background", backgroundColor);
        fillImage = CreateImage("Fill", fillColor);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;
    }

    /// <summary>Создаёт Image, растянутый по родительскому rect (подложка идёт первой — она снизу).</summary>
    private Image CreateImage(string objectName, Color color)
    {
        var imageObject = new GameObject(objectName, typeof(RectTransform));
        imageObject.transform.SetParent(canvasRect, false);

        var rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = imageObject.AddComponent<Image>();
        image.sprite = WhiteSprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void Show()
    {
        shown = true;
        idleTimer = 0f;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    /// <summary>
    /// Отсчёт покоя и затухание. Держится отдельно от <see cref="Update"/>, чтобы
    /// EditMode-тесты прогоняли логику детерминированно.
    /// </summary>
    private void Tick(float deltaSeconds)
    {
        if (canvasGroup == null || deltaSeconds <= 0f) return;

        if (shown)
        {
            idleTimer += deltaSeconds;
            if (idleTimer >= hideDelay) shown = false; // урона не было hideDelay секунд — прячем
        }

        float targetAlpha = shown ? 1f : 0f;
        if (canvasGroup.alpha == targetAlpha) return;

        canvasGroup.alpha = fadeSpeed > 0f
            ? Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * deltaSeconds)
            : targetAlpha;
    }

    private void Update()
    {
        Tick(Time.deltaTime);
        UpdatePose();
    }

    /// <summary>Держит полосу над целью и разворачивает её лицом к камере.</summary>
    private void UpdatePose()
    {
        Transform anchor = Anchor;
        if (anchor == null) return;

        // Camera.main ищет объект по тегу — зовём только при потере кэша, не каждый кадр.
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
            if (viewCamera == null) return;
        }

        Vector3 position = anchor.position + Vector3.up * worldOffset;
        Quaternion rotation = viewCamera.transform.rotation;

        // Компонент может висеть и на самом враге: тогда полосу несёт дочерний canvas,
        // иначе мы бы сдвигали самого врага на worldOffset вверх.
        if (anchor == transform)
        {
            canvasRect.position = position;
            canvasRect.rotation = rotation;
            return;
        }

        transform.position = position;
        transform.rotation = rotation;
    }

    /// <summary>Белый спрайт 1x1 из встроенной текстуры; создаётся один раз на всё приложение.</summary>
    private static Sprite WhiteSprite
    {
        get
        {
            if (sharedWhiteSprite == null)
                sharedWhiteSprite = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f));

            return sharedWhiteSprite;
        }
    }
}
