using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Экран смерти: затемнение, «Вы погибли» и строка о потере опыта.
/// Строится кодом в стиле UI культивации и не требует ссылок в сцене.
/// Подписывается на <see cref="PlayerRespawnService"/>: показывается на смерти,
/// гаснет после респавна. Паузу не ставит — время не трогает.
/// </summary>
[DisallowMultipleComponent]
public class PlayerDeathScreen : MonoBehaviour
{
    [Header("Тайминги")]
    [Tooltip("Задержка перед появлением надписи: сначала успевает проиграть анимация смерти.")]
    [SerializeField, Min(0f)] private float fadeInDelay = 0.35f;
    [Tooltip("Скорость проявления и угасания экрана, единиц альфы в секунду.")]
    [SerializeField, Min(0.1f)] private float fadeSpeed = 2.5f;
    [Tooltip("Сколько держать экран после респавна, секунд.")]
    [SerializeField, Min(0f)] private float holdAfterRespawn = 0.6f;

    [Header("Ссылки")]
    [Tooltip("Источник события смерти. Пусто — ищется на игроке и через bootstrap.")]
    [SerializeField] private PlayerRespawnService respawnService;

    private CanvasGroup group;
    private TextMeshProUGUI lossLabel;
    private float visibleSince = -1f;
    private bool dying;
    private float respawnedAt = -1f;

    /// <summary>Показывается ли экран прямо сейчас (для тестов и внешних систем).</summary>
    public bool IsVisible => dying || (group != null && group.alpha > 0f);

    /// <summary>
    /// Идемпотентная инициализация: EditMode не вызывает <c>Awake</c>,
    /// поэтому тесты входят тем же путём, что и игра.
    /// </summary>
    internal void EnsureInitialized()
    {
        if (group != null) return;

        BuildHierarchy();
        ResolveRespawnService();
    }

    /// <summary>
    /// Сервис респавна появляется в рантайме через bootstrap, поэтому ищем его
    /// лениво: при первом успехе сразу подписываемся на события смерти и респавна.
    /// </summary>
    private void ResolveRespawnService()
    {
        if (respawnService != null) return;

        if (CultivationBootstrap.Instance != null)
            respawnService = CultivationBootstrap.Instance.Respawn;

        if (respawnService == null)
            respawnService = GetComponentInParent<PlayerRespawnService>();
        if (respawnService == null)
            respawnService = FindFirstObjectByType<PlayerRespawnService>();

        if (respawnService == null) return;

        respawnService.OnDied += Show;
        respawnService.OnRespawned += Hide;
    }

    private void OnDestroy()
    {
        if (respawnService == null) return;

        respawnService.OnDied -= Show;
        respawnService.OnRespawned -= Hide;
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void Update()
    {
        if (respawnService == null) ResolveRespawnService();
        Tick(Time.unscaledDeltaTime);
    }

    /// <summary>Смерть игрока: экран начинает проявляться.</summary>
    public void Show()
    {
        EnsureInitialized();
        dying = true;
        visibleSince = Time.unscaledTime;
        respawnedAt = -1f;
        UpdateLossLabel();
    }

    /// <summary>Респавн завершён: экран гаснет и снимается с экрана.</summary>
    public void Hide()
    {
        dying = false;
        respawnedAt = Time.unscaledTime;
    }

    private void Tick(float deltaSeconds)
    {
        if (group == null || deltaSeconds <= 0f) return;

        if (dying && visibleSince >= 0f && Time.unscaledTime - visibleSince < fadeInDelay)
            return;

        if (!dying && respawnedAt >= 0f && Time.unscaledTime - respawnedAt < holdAfterRespawn)
            return;

        float target = dying ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * deltaSeconds);

        if (!dying && group.alpha <= 0f) respawnedAt = -1f;
    }

    /// <summary>Прогон проявления/угасания без <c>Update</c> — для EditMode-тестов.</summary>
    internal void TickForTests(float unscaledDeltaSeconds)
    {
        Tick(unscaledDeltaSeconds);
    }

    /// <summary>Альфа оверлея для тестов.</summary>
    internal float AlphaForTests => group != null ? group.alpha : 0f;

    private void UpdateLossLabel()
    {
        if (lossLabel == null) return;

        int lost = respawnService != null ? respawnService.LastExperienceLost : 0;
        lossLabel.text = lost > 0 ? "Потеряно опыта: " + lost : string.Empty;
    }

    private void BuildHierarchy()
    {
        var root = CultivationUIFactory.CreateRect("DeathScreen", CultivationUIFactory.FindOrCreateCanvas().transform);
        CultivationUIFactory.Stretch(root);
        root.SetAsLastSibling();

        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        var dim = CultivationUIFactory.CreateImage("Dim", root, new Color(0.02f, 0.02f, 0.03f, 0.85f));
        CultivationUIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = false;

        var vignette = CultivationUIFactory.CreateImage("Vignette", root, new Color(0.35f, 0.02f, 0.02f, 0.9f),
            CultivationUIFactory.VignetteSprite, false);
        CultivationUIFactory.Stretch(vignette.rectTransform);
        vignette.raycastTarget = false;

        var title = CultivationUIFactory.CreateText("Title", root, "ВЫ ПОГИБЛИ", 52f,
            CultivationUIFactory.Danger, TextAlignmentOptions.Center);
        CultivationUIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 40f), new Vector2(1000f, 70f));
        title.characterSpacing = 12f;

        lossLabel = CultivationUIFactory.CreateText("Loss", root, string.Empty, 22f,
            CultivationUIFactory.TextMuted, TextAlignmentOptions.Center);
        CultivationUIFactory.Place(lossLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -30f), new Vector2(1000f, 34f));

        var hint = CultivationUIFactory.CreateText("Hint", root, "Возвращение к алтарю…", 17f,
            CultivationUIFactory.TextMuted, TextAlignmentOptions.Center);
        CultivationUIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -70f), new Vector2(1000f, 28f));
    }
}
