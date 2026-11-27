using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Смерть и возврат игрока: теряет часть опыта ступени, переносится к точке
/// возрождения и получает короткую неуязвимость.
/// Не трогает <c>PlayerController</c> — только перемещает трансформ.
/// </summary>
[DisallowMultipleComponent]
public class PlayerRespawnService : MonoBehaviour
{
    [Header("Точка возрождения")]
    [SerializeField] private Transform respawnPoint;
    [Tooltip("Корень игрока. Пусто — используется объект этого компонента.")]
    [SerializeField] private Transform playerRoot;

    [Header("Штрафы и восстановление")]
    [SerializeField, Range(0f, 1f)] private float experienceLossPercent = 0.25f;
    [SerializeField, Range(0f, 1f)] private float hpRestorePercent = 0.5f;
    [SerializeField, Min(0f)] private float invulnerabilityAfterRespawn = 3f;
    [Tooltip("Пауза между смертью и респавном: анимация смерти, затемнение и сообщение.")]
    [SerializeField, Min(0f)] private float respawnDelay = 1.8f;
    [Tooltip("Неуязвимость продлевается, пока живой враг ближе его aggroRadius + этот запас.")]
    [SerializeField, Min(0f)] private float invulnerabilitySafetyMargin = 5f;
    [Tooltip("Предел продления неуязвимости после респауна, сек. Дальше она истекает даже рядом с врагом.")]
    [SerializeField, Min(0f)] private float invulnerabilityMaxExtension = 10f;

    [Header("Ссылки")]
    [SerializeField] private PlayerHealth health;
    [SerializeField] private CultivationSystem cultivation;
    [SerializeField] private BreakthroughSequence breakthrough;

    /// <summary>Игрок погиб: экран смерти и другие подписчики реагируют здесь же.</summary>
    public event System.Action OnDied;

    /// <summary>Респавн завершён: экран смерти гаснет после этого события.</summary>
    public event System.Action OnRespawned;

    public float ExperienceLossPercent => experienceLossPercent;

    /// <summary>Пауза между смертью и респавном, секунд.</summary>
    public float RespawnDelay => respawnDelay;

    /// <summary>Сколько опыта потеряно за последнюю смерть. Экран смерти берёт число отсюда.</summary>
    public int LastExperienceLost { get; private set; }

    private EnemyCombatAI[] enemies;
    private float invulnerabilityExtension;
    private bool deathPending;
    private float diedAtUnscaledTime;

    private void OnEnable()
    {
        if (health != null)
            health.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDied -= HandleDied;
    }

    /// <summary>Меняет точку возрождения (алтарь, стартовая площадка).</summary>
    public void SetRespawnPoint(Transform point)
    {
        respawnPoint = point;
    }

    /// <summary>Подключает ссылки после создания компонента в рантайме.</summary>
    public void Initialize(PlayerHealth playerHealth, CultivationSystem cultivationSystem,
        BreakthroughSequence sequence, Transform point, Transform player)
    {
        if (health != null)
            health.OnDied -= HandleDied;

        health = playerHealth;
        cultivation = cultivationSystem;
        breakthrough = sequence;
        respawnPoint = point;
        playerRoot = player;

        if (isActiveAndEnabled && health != null)
            health.OnDied += HandleDied;
    }

    /// <summary>
    /// Смерть игрока: сначала пауза с анимацией и экраном, только потом респавн.
    /// Мгновенный перенос в том же кадре читался как «мигнуло и я на месте».
    /// </summary>
    private void HandleDied()
    {
        diedAtUnscaledTime = Time.unscaledTime;
        deathPending = true;
        OnDied?.Invoke();
    }

    /// <summary>Идёт ли ещё пауза перед респавном (для UI и тестов).</summary>
    public bool IsDeathPending => deathPending;

    /// <summary>Сколько секунд уже идёт пауза смерти.</summary>
    public float DeathElapsed => deathPending ? Time.unscaledTime - diedAtUnscaledTime : 0f;

    /// <summary>
    /// Отсчёт задержки смерти и продление неуязвимости после респавна.
    /// Пауза смерти идёт на unscaled-времени: экран не должен глохнуть на паузе меню.
    /// </summary>
    private void Update()
    {
        if (deathPending)
        {
            if (Time.unscaledTime - diedAtUnscaledTime >= respawnDelay)
            {
                deathPending = false;
                Respawn();
            }
            return;
        }

        if (health == null || playerRoot == null) return;
        if (health.IsDead || invulnerabilityExtension <= 0f) return;
        if (!EnemyWithinAggro(playerRoot.position)) return;

        invulnerabilityExtension = Mathf.Max(0f, invulnerabilityExtension - Time.deltaTime);
        health.SetInvulnerable(0.5f);
    }

    /// <summary>
    /// Выполняет респавн: отмена прорыва, потеря опыта, перенос, лечение,
    /// неуязвимость и событие о завершении.
    /// </summary>
    public void Respawn()
    {
        if (health == null)
        {
            Debug.LogWarning("[PlayerRespawnService] Не назначен PlayerHealth — респавн пропущен.");
            return;
        }

        // Смерть во время прорыва иначе оставит корутину и второй штраф.
        if (breakthrough != null && breakthrough.IsRunning)
            breakthrough.CancelBreakthrough();

        if (cultivation != null)
            LastExperienceLost = cultivation.LoseExperiencePercent(experienceLossPercent);

        Transform target = playerRoot != null ? playerRoot : transform;

        if (respawnPoint != null)
            Teleport(target, respawnPoint.position, respawnPoint.rotation);
        else
            Debug.LogWarning("[PlayerRespawnService] Точка возрождения не задана — игрок остался на месте.");

        health.RestorePercent(hpRestorePercent);
        health.SetInvulnerable(invulnerabilityAfterRespawn);
        invulnerabilityExtension = invulnerabilityMaxExtension;
        enemies = null;
        deathPending = false;
        health.NotifyRespawned();
        OnRespawned?.Invoke();
    }

    private bool EnemyWithinAggro(Vector3 position)
    {
        // ponytail: пересканируем только пока идёт продление (<=10 с). При десятках врагов — реестр.
        if (enemies == null)
            enemies = FindObjectsByType<EnemyCombatAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (EnemyCombatAI enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;

            float radius = enemy.AggroRadius + invulnerabilitySafetyMargin;
            if ((enemy.transform.position - position).sqrMagnitude <= radius * radius) return true;
        }

        return false;
    }

    private static void Teleport(Transform target, Vector3 position, Quaternion rotation)
    {
        if (target == null) return;

        var controller = target.GetComponent<CharacterController>();
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled) controller.enabled = false;

        var agent = target.GetComponent<NavMeshAgent>();
        bool agentWasEnabled = agent != null && agent.enabled;
        if (agentWasEnabled) agent.enabled = false;

        target.SetPositionAndRotation(position, rotation);

        if (agentWasEnabled) agent.enabled = true;
        if (controllerWasEnabled) controller.enabled = true;
    }
}
