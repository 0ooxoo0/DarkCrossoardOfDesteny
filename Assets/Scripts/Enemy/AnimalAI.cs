using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Мирное животное: бродит вокруг стартовой точки и убегает после удара.
/// Не атакует; смерть и добычу обрабатывает <see cref="HarvestableObject"/>.
/// </summary>
[DisallowMultipleComponent]
public class AnimalAI : MonoBehaviour
{
    private enum State
    {
        Wander,
        Idle,
        Flee,
        Dead
    }

    [Header("Блуждание")]
    [SerializeField, Min(0f)] private float wanderRadius = 8f;
    [SerializeField, Min(0f)] private float wanderSpeed = 1.2f;
    [SerializeField, Min(0f)] private float idleSeconds = 2f;

    [Header("Побег")]
    [SerializeField, Min(0f)] private float fleeSpeed = 4f;
    [SerializeField, Min(0.1f)] private float fleeSeconds = 4f;

    [Header("Ссылки")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private HarvestableObject health;

    private State state = State.Wander;
    private Vector3 homePosition;
    private Vector3 wanderTarget;
    private float stateTimer;
    private float fleeTimer;
    private Transform threat;

    /// <summary>Убегает ли животное прямо сейчас.</summary>
    public bool IsFleeing => state == State.Flee;

    private void Awake()
    {
        homePosition = transform.position;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (health == null) health = GetComponent<HarvestableObject>();

        if (agent != null) agent.speed = wanderSpeed;
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Damaged += HandleDamaged;
            health.OnDied += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }
    }

    private void Start()
    {
        PickWanderTarget();
    }

    private void Update()
    {
        if (state == State.Dead) return;

        switch (state)
        {
            case State.Wander:
                if (HasArrived())
                {
                    state = State.Idle;
                    stateTimer = idleSeconds;
                    StopMoving();
                }
                else
                {
                    MoveTo(wanderTarget, wanderSpeed);
                }

                break;

            case State.Idle:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    PickWanderTarget();
                    state = State.Wander;
                }

                break;

            case State.Flee:
                fleeTimer -= Time.deltaTime;
                if (fleeTimer <= 0f)
                {
                    state = State.Idle;
                    stateTimer = idleSeconds;
                    StopMoving();
                    break;
                }

                MoveTo(ComputeFleeTarget(), fleeSpeed);
                break;
        }
    }

    private void HandleDamaged(float damage)
    {
        threat = ResolveThreat();
        state = State.Flee;
        fleeTimer = fleeSeconds;
    }

    private void HandleDied(HarvestableObject deadObject, Vector3 position)
    {
        state = State.Dead;
        StopMoving();
        if (agent != null) agent.enabled = false;
    }

    private Transform ResolveThreat()
    {
        PlayerHealth player = FindFirstObjectByType<PlayerHealth>();
        return player != null ? player.transform : null;
    }

    private void PickWanderTarget()
    {
        Vector2 circle = Random.insideUnitCircle * wanderRadius;
        Vector3 candidate = homePosition + new Vector3(circle.x, 0f, circle.y);

        if (agent != null && NavMesh.SamplePosition(candidate, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            candidate = hit.position;

        wanderTarget = candidate;
    }

    private Vector3 ComputeFleeTarget()
    {
        Vector3 away = threat != null
            ? transform.position - threat.position
            : transform.forward;

        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = -transform.forward;

        Vector3 candidate = transform.position + away.normalized * (wanderRadius * 2f);

        if (agent != null && NavMesh.SamplePosition(candidate, out NavMeshHit hit, wanderRadius * 2f, NavMesh.AllAreas))
            candidate = hit.position;

        return candidate;
    }

    private bool HasArrived()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.25f;

        Vector3 delta = wanderTarget - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= 0.09f;
    }

    private void MoveTo(Vector3 position, float speed)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = speed;
            agent.isStopped = false;
            agent.SetDestination(position);
            return;
        }

        Vector3 direction = position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        transform.position += direction.normalized * (speed * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(direction.normalized);
    }

    private void StopMoving()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        agent.isStopped = true;
        agent.ResetPath();
    }
}
