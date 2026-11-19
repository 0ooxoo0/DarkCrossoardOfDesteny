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

    [Header("Ссылки")]
    [SerializeField] private PlayerHealth health;
    [SerializeField] private CultivationSystem cultivation;
    [SerializeField] private BreakthroughSequence breakthrough;

    public float ExperienceLossPercent => experienceLossPercent;

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

    private void HandleDied()
    {
        Respawn();
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
            cultivation.LoseExperiencePercent(experienceLossPercent);

        Transform target = playerRoot != null ? playerRoot : transform;

        if (respawnPoint != null)
            Teleport(target, respawnPoint.position, respawnPoint.rotation);
        else
            Debug.LogWarning("[PlayerRespawnService] Точка возрождения не задана — игрок остался на месте.");

        health.RestorePercent(hpRestorePercent);
        health.SetInvulnerable(invulnerabilityAfterRespawn);
        health.NotifyRespawned();
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
