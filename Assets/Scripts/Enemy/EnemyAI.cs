using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Простой боевой ИИ: агр по радиусу, преследование, удар в ближнем бою и возврат домой.
/// Урон наносит через <see cref="PlayerHealth"/>, смерть определяет по
/// <see cref="HarvestableObject"/>. <c>PlayerController</c> не трогает.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAI : MonoBehaviour
{
    private enum State
    {
        Idle,
        Chase,
        Attack,
        Return,
        Dead
    }

    [Header("Радиусы")]
    [SerializeField, Min(0f)] private float aggroRadius = 10f;
    [SerializeField, Min(0f)] private float attackRange = 2f;
    [SerializeField, Min(0f)] private float leashRadius = 18f;
    [SerializeField, Min(0f)] private float loseTargetSeconds = 6f;

    [Header("Бой")]
    [SerializeField, Min(0f)] private float attackDamage = 8f;
    [SerializeField, Min(0.1f)] private float attackCooldown = 1.6f;
    [SerializeField, Min(0f)] private float moveSpeed = 3.2f;

    [Header("Ссылки")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private HarvestableObject health;
    [SerializeField] private Animator animator;
    [Tooltip("Имя триггера атаки в Animator. Пусто — анимация не проигрывается.")]
    [SerializeField] private string attackTriggerName = string.Empty;

    private State state = State.Idle;
    private Transform target;
    private PlayerHealth targetHealth;
    private Vector3 homePosition;
    private float attackTimer;
    private float lostTimer;

    /// <summary>Агрессивен ли враг: используется проверкой медитации.</summary>
    public bool IsAggroed => state == State.Chase || state == State.Attack;

    /// <summary>Текущая цель или null.</summary>
    public Transform Target => target;

    private void Awake()
    {
        homePosition = transform.position;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (health == null) health = GetComponent<HarvestableObject>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (agent != null) agent.speed = moveSpeed;
    }

    private void OnEnable()
    {
        if (health != null) health.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        if (health != null) health.OnDied -= HandleDied;
    }

    private void Start()
    {
        ResolveTarget();
    }

    private void Update()
    {
        if (state == State.Dead) return;

        if (targetHealth == null || targetHealth.IsDead)
            ResolveTarget();

        attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);

        if (targetHealth == null)
        {
            TickReturnOrIdle();
            return;
        }

        float distance = Vector3.Distance(transform.position, target.position);
        float distanceFromHome = Vector3.Distance(transform.position, homePosition);

        if (distanceFromHome > leashRadius)
        {
            EnterReturn();
            return;
        }

        switch (state)
        {
            case State.Idle:
                if (distance <= aggroRadius)
                    EnterChase();
                else
                    StopMoving();
                break;

            case State.Chase:
                if (distance <= attackRange)
                    EnterAttack();
                else
                    MoveTo(target.position);
                break;

            case State.Attack:
                FaceTarget();
                if (distance > attackRange)
                {
                    EnterChase();
                    break;
                }

                if (attackTimer <= 0f)
                {
                    Strike();
                    attackTimer = attackCooldown;
                }

                break;

            case State.Return:
                TickReturnOrIdle();
                break;
        }

        if (distance > aggroRadius * 1.5f)
        {
            lostTimer += Time.deltaTime;
            if (lostTimer >= loseTargetSeconds)
                EnterReturn();
        }
        else
        {
            lostTimer = 0f;
        }
    }

    /// <summary>Принудительно задаёт цель (например, из скрипта события).</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetHealth = newTarget != null ? newTarget.GetComponent<PlayerHealth>() : null;
        lostTimer = 0f;

        if (targetHealth != null && state != State.Dead)
            EnterChase();
    }

    private void ResolveTarget()
    {
        PlayerHealth player = FindFirstObjectByType<PlayerHealth>();
        target = player != null ? player.transform : null;
        targetHealth = player;
    }

    private void EnterChase()
    {
        state = State.Chase;
        lostTimer = 0f;
        SetAgentStopped(false);
    }

    private void EnterAttack()
    {
        state = State.Attack;
        StopMoving();
        FaceTarget();
    }

    private void EnterReturn()
    {
        state = State.Return;
        lostTimer = 0f;
        SetAgentStopped(false);
        MoveTo(homePosition);
    }

    private void TickReturnOrIdle()
    {
        if (state != State.Return)
        {
            StopMoving();
            state = State.Idle;
            return;
        }

        if (Vector3.Distance(transform.position, homePosition) > 0.5f)
        {
            MoveTo(homePosition);
            return;
        }

        StopMoving();
        state = State.Idle;
    }

    private void MoveTo(Vector3 position)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(position);
            return;
        }

        // Без NavMesh враг всё равно двигается — иначе на кривой сцене он замирает.
        Vector3 direction = position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Vector3 step = direction.normalized * (moveSpeed * Time.deltaTime);
        transform.position += step;
        transform.rotation = Quaternion.LookRotation(direction.normalized);
    }

    private void StopMoving()
    {
        SetAgentStopped(true);
    }

    private void SetAgentStopped(bool stopped)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        agent.isStopped = stopped;
        if (stopped) agent.ResetPath();
    }

    private void FaceTarget()
    {
        if (target == null) return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(direction.normalized);
    }

    private void Strike()
    {
        if (targetHealth == null) return;

        var damage = new DamageInfo(attackDamage, gameObject, target.position);

        // Блок игрока важнее прямого урона: удачный блок не отнимает здоровье.
        ICombatBlockable blockable = targetHealth.GetComponent<ICombatBlockable>();
        if (blockable != null && blockable.IsBlocking && blockable.CanBlockFrom(transform.position))
            blockable.OnBlocked(damage);
        else
            targetHealth.TakeDamage(damage);

        PlayAttackAnimation();
    }

    private void PlayAttackAnimation()
    {
        if (animator == null || string.IsNullOrEmpty(attackTriggerName)) return;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name != attackTriggerName) continue;
            if (parameters[i].type != AnimatorControllerParameterType.Trigger) continue;

            animator.SetTrigger(attackTriggerName);
            return;
        }
    }

    private void HandleDied(HarvestableObject deadObject, Vector3 position)
    {
        state = State.Dead;
        StopMoving();
        if (agent != null) agent.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(Application.isPlaying ? homePosition : transform.position, aggroRadius);
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.7f);
        Gizmos.DrawWireSphere(Application.isPlaying ? homePosition : transform.position, attackRange);
    }
}
