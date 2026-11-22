using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Simple, readable enemy combat AI: Idle / Chase / Attack / Return (and Block/Hit/Dead).
/// Intelligence 1..4 changes reaction, block chance, combo chance, damage and speed.
/// Intelligence 5 is the "no chance" level: near-perfect blocks and counters.
/// Damage is applied only by CombatHitbox during the attack animation window.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public class EnemyCombatAI : MonoBehaviour, ICombatAnimationReceiver, ICombatBlockable
{
    public enum State { Idle, Chase, Attack, Block, Return, Hit, Dead }

    [Header("Intelligence (1 = weak, 4 = strong, 5 = no chance)")]
    [Range(1, 5)] [SerializeField] private int intelligence = 2;

    [Header("Ranges")]
    [SerializeField, Min(0f)] private float aggroRadius = 12f;
    [SerializeField, Min(0.5f)] private float attackRange = 2.2f;
    [SerializeField, Min(0.5f)] private float blockRange = 3f;
    [SerializeField, Min(1f)] private float leashRadius = 30f;
    [SerializeField, Min(0f)] private float loseTargetSeconds = 8f;

    [Header("Combat (base values; intelligence applies multipliers)")]
    [SerializeField, Min(0f)] private float attackDamage = 12f;
    [SerializeField, Min(0.2f)] private float attackCooldown = 2f;
    [SerializeField, Min(0.5f)] private float moveSpeed = 3.5f;
    [SerializeField, Min(0.1f)] private float blockDuration = 0.9f;
    [SerializeField, Range(1, 3)] private int maxCombo = 2;
    [SerializeField, Min(0f)] private float hitStun = 0.35f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private EnemyHealth health;
    [SerializeField] private CombatHitbox attackHitbox;
    [SerializeField] private Transform target;

    [Header("Animator Parameters")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string comboTrigger = "Combo";
    [SerializeField] private string attackIndexParam = "AttackIndex";
    [SerializeField] private string blockBool = "IsBlocking";
    [SerializeField] private string blockHitTrigger = "BlockHit";
    [SerializeField] private string hitTrigger = "Hit";

    private State state = State.Idle;
    private PlayerController targetController;
    private PlayerHealth targetHealth;
    private Vector3 homePosition;
    private float nextAttackTime;
    private float blockUntil;
    private float hitStunUntil;
    private float nextBlockDecision;
    private float lostTimer;
    private int comboIndex;
    private bool comboWindowOpen;
    private bool blocking;
    private float manualSpeed;

    public bool IsBlocking => blocking;
    public bool IsDead => state == State.Dead;
    public int Intelligence => intelligence;
    public State CurrentState => state;
    public float AggroRadius => aggroRadius;
    public float AttackRange => attackRange;
    public float BlockRange => blockRange;
    public float LeashRadius => leashRadius;
    public float EffectiveDamage => attackDamage * DamageMultiplier;
    public float EffectiveMoveSpeed => moveSpeed * SpeedMultiplier;
    public float EffectiveCooldown => attackCooldown * CooldownMultiplier;
    public float BlockChance => blockChance;

    private float blockChance;

    private float DamageMultiplier
    {
        get
        {
            switch (intelligence)
            {
                case 1: return 0.7f;
                case 2: return 1f;
                case 3: return 1.2f;
                case 4: return 1.45f;
                default: return 1.9f;
            }
        }
    }

    private float SpeedMultiplier
    {
        get
        {
            switch (intelligence)
            {
                case 1: return 0.8f;
                case 2: return 1f;
                case 3: return 1.15f;
                case 4: return 1.3f;
                default: return 1.55f;
            }
        }
    }

    private float CooldownMultiplier
    {
        get
        {
            switch (intelligence)
            {
                case 1: return 1.35f;
                case 2: return 1f;
                case 3: return 0.8f;
                case 4: return 0.6f;
                default: return 0.35f;
            }
        }
    }

    private float ReactionDelay
    {
        get
        {
            switch (intelligence)
            {
                case 1: return 0.8f;
                case 2: return 0.5f;
                case 3: return 0.3f;
                case 4: return 0.15f;
                default: return 0.02f;
            }
        }
    }

    private float ComboChance
    {
        get
        {
            switch (intelligence)
            {
                case 1: return 0f;
                case 2: return 0.25f;
                case 3: return 0.55f;
                case 4: return 0.8f;
                default: return 1f;
            }
        }
    }

    private void Awake()
    {
        homePosition = transform.position;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (health == null) health = GetComponent<EnemyHealth>();

        blockChance = GetBlockChance();

        if (agent != null)
        {
            agent.speed = EffectiveMoveSpeed;
            agent.acceleration = 30f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = Mathf.Max(1f, attackRange * 0.65f);
            agent.updateRotation = false;
        }
    }

    private void Start()
    {
        ResolveTarget();
        if (attackHitbox != null)
            attackHitbox.Configure(EffectiveDamage, CombatTeam.Enemy, transform);
    }

    private float GetBlockChance()
    {
        switch (intelligence)
        {
            case 1: return 0f;
            case 2: return 0.25f;
            case 3: return 0.55f;
            case 4: return 0.8f;
            default: return 1f;
        }
    }

    private void Update()
    {
        if (state == State.Dead) return;
        if (health != null && health.IsDead)
        {
            NotifyDeath();
            return;
        }

        if (target == null || targetHealth == null || targetHealth.IsDead)
            ResolveTarget();

        float dt = Time.deltaTime;
        if (animator != null)
        {
            float planarSpeed = AgentReady ? agent.velocity.magnitude : manualSpeed;
            animator.SetFloat(speedParam, planarSpeed / Mathf.Max(0.1f, EffectiveMoveSpeed));
        }

        if (target == null || targetHealth == null || targetHealth.IsDead)
        {
            TickIdleOrReturn();
            return;
        }

        float distance = Vector3.Distance(transform.position, target.position);
        float distanceFromHome = Vector3.Distance(transform.position, homePosition);

        if (state == State.Hit)
        {
            if (Time.time >= hitStunUntil) EnterChase();
            return;
        }

        if (state == State.Block)
        {
            FaceTarget();
            if (Time.time >= blockUntil) SetBlocking(false);
            if (distance > blockRange * 1.4f || Time.time >= blockUntil) EnterChase();
            return;
        }

        if (distanceFromHome > leashRadius || distance > aggroRadius * 1.8f)
        {
            lostTimer += dt;
            if (lostTimer >= loseTargetSeconds) EnterReturn();
        }
        else
        {
            lostTimer = 0f;
        }

        // Smart defence: read the player's current attack and sometimes block it.
        if (intelligence >= 2 && targetController != null && targetController.IsAttacking &&
            distance <= blockRange && Time.time >= nextBlockDecision)
        {
            nextBlockDecision = Time.time + ReactionDelay;
            if (Random.value < blockChance)
            {
                EnterBlock();
                return;
            }
        }

        switch (state)
        {
            case State.Idle:
                if (distance <= aggroRadius) EnterChase();
                else TickIdleOrReturn();
                break;

            case State.Chase:
                if (distance <= attackRange)
                {
                    if (Time.time >= nextAttackTime) StartAttack();
                    else StopMoving();
                }
                else
                {
                    MoveTo(target.position);
                }
                break;

            case State.Attack:
                FaceTarget();
                if (distance > attackRange * 1.45f)
                {
                    EnterChase();
                    break;
                }

                if (comboWindowOpen && comboIndex < maxCombo - 1 && Random.value < ComboChance)
                    AdvanceCombo();

                if (Time.time >= nextAttackTime && !comboWindowOpen)
                    StartAttack();
                break;

            case State.Return:
                TickIdleOrReturn();
                break;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetController = newTarget != null ? newTarget.GetComponent<PlayerController>() : null;
        targetHealth = newTarget != null ? newTarget.GetComponent<PlayerHealth>() : null;
        lostTimer = 0f;
        if (target != null && state != State.Dead) EnterChase();
    }

    private void ResolveTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        target = player.transform;
        targetController = player.GetComponent<PlayerController>();
        targetHealth = player.GetComponent<PlayerHealth>();
    }

    private void EnterChase()
    {
        if (state == State.Dead) return;
        state = State.Chase;
        lostTimer = 0f;
        SetBlocking(false);
        SetAgentStopped(false);
    }

    private void EnterBlock()
    {
        if (state == State.Dead) return;
        state = State.Block;
        blocking = true;
        blockUntil = Time.time + blockDuration;
        if (animator != null) animator.SetBool(blockBool, true);
        StopMoving();
        FaceTarget();
    }

    private void SetBlocking(bool value)
    {
        if (blocking == value) return;
        blocking = value;
        if (animator != null) animator.SetBool(blockBool, value);
    }

    private void StartAttack()
    {
        if (state == State.Dead) return;

        state = State.Attack;
        comboIndex = 0;
        comboWindowOpen = false;
        nextAttackTime = Time.time + EffectiveCooldown;

        if (attackHitbox != null)
            attackHitbox.Configure(EffectiveDamage, CombatTeam.Enemy, transform);

        if (animator != null)
        {
            animator.SetInteger(attackIndexParam, 0);
            animator.ResetTrigger(attackTrigger);
            animator.SetTrigger(attackTrigger);
        }

        StopMoving();
        FaceTarget();
    }

    private void AdvanceCombo()
    {
        if (animator == null) return;
        comboIndex++;
        comboWindowOpen = false;
        animator.SetInteger(attackIndexParam, comboIndex);
        animator.ResetTrigger(comboTrigger);
        animator.SetTrigger(comboTrigger);
        nextAttackTime = Time.time + EffectiveCooldown * 0.65f;
    }

    private void TickIdleOrReturn()
    {
        if (state != State.Return && Vector3.Distance(transform.position, homePosition) < 1f)
        {
            state = State.Idle;
            StopMoving();
            return;
        }

        state = State.Return;
        if (Vector3.Distance(transform.position, homePosition) > 0.8f)
            MoveTo(homePosition);
        else
        {
            state = State.Idle;
            StopMoving();
        }
    }

    private void EnterReturn()
    {
        state = State.Return;
        lostTimer = 0f;
        SetBlocking(false);
        SetAgentStopped(false);
        MoveTo(homePosition);
    }

    private void MoveTo(Vector3 position)
    {
        if (AgentReady)
        {
            manualSpeed = 0f;
            agent.isStopped = false;
            agent.SetDestination(position);
            return;
        }

        Vector3 direction = position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            manualSpeed = 0f;
            return;
        }

        transform.position += direction.normalized * (EffectiveMoveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(direction.normalized);
        manualSpeed = EffectiveMoveSpeed;
    }

    private void StopMoving()
    {
        manualSpeed = 0f;
        if (AgentReady)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    /// <summary>Агент реально стоит на навмеше; без запечённой сетки работаем напрямую.</summary>
    private bool AgentReady
    {
        get { return agent != null && agent.enabled && agent.isOnNavMesh; }
    }

    private void SetAgentStopped(bool stopped)
    {
        if (!AgentReady) return;
        agent.isStopped = stopped;
        if (stopped) agent.ResetPath();
    }

    private void FaceTarget()
    {
        if (target == null) return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion wanted = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 12f * Time.deltaTime);
    }

    public bool CanBlockFrom(Vector3 attackerPosition)
    {
        if (!blocking) return false;

        Vector3 direction = attackerPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return true;

        return Vector3.Angle(transform.forward, direction) <= 110f * 0.5f;
    }

    public void OnBlocked(DamageInfo info)
    {
        if (animator != null) animator.SetTrigger(blockHitTrigger);
        blockUntil = Time.time + 0.4f;

        if (intelligence >= 4)
        {
            nextAttackTime = 0f;
            StartAttack();
        }
    }

    public void NotifyDamaged(DamageInfo info)
    {
        if (state == State.Dead) return;

        if (animator != null) animator.SetTrigger(hitTrigger);
        state = State.Hit;
        hitStunUntil = Time.time + Mathf.Max(0.15f, hitStun);
        SetBlocking(false);
        StopMoving();
    }

    public void NotifyDeath()
    {
        if (state == State.Dead) return;

        state = State.Dead;
        SetBlocking(false);
        if (attackHitbox != null) attackHitbox.SetActive(false);
        if (animator != null)
        {
            animator.SetFloat(speedParam, 0f);
            animator.SetBool("IsDead", true);
        }
        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
        }
    }

    public void OnAttackWindowOpen()
    {
        if (attackHitbox != null)
        {
            attackHitbox.Configure(EffectiveDamage, CombatTeam.Enemy, transform);
            attackHitbox.SetActive(true);
        }
    }

    public void OnAttackWindowClose()
    {
        if (attackHitbox != null) attackHitbox.SetActive(false);
    }

    public void OnComboWindowOpen()
    {
        comboWindowOpen = true;
    }

    public void OnComboWindowClose()
    {
        comboWindowOpen = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.45f);
        Gizmos.DrawWireSphere(Application.isPlaying ? homePosition : transform.position, aggroRadius);

        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, blockRange);
    }
}
