using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class PlayerController : MonoBehaviour, ICombatAnimationReceiver
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Animation")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string comboTrigger = "Combo";
    [SerializeField] private string attackIndexParam = "AttackIndex";
    [SerializeField, Min(0)] private int maxComboIndex = 2;

    [Header("Jump")]
    [Tooltip("Слой Animator с фазами прыжка. 0 — слой выключен.")]
    [SerializeField] private string jumpLayerName = "Jump";
    [Tooltip("Int-параметр слоя Jump: 1 — взлёт, 2 — падение, 3 — приземление.")]
    [SerializeField] private string jumpPhaseParam = "JumpPhase";
    [SerializeField, Min(0.1f)] private float jumpHeight = 1.2f;
    [SerializeField, Min(0.2f)] private float jumpDuration = 0.9f;

    [Header("Attack")]
    [SerializeField] private Weapon weapon;
    [SerializeField] private WeaponManager weaponManager;

    [Header("Combat References")]
    [Tooltip("Здоровье игрока. Обязательно: смерть останавливает управление.")]
    [SerializeField] private PlayerHealth health;
    [Tooltip("Блок игрока. Обязателен: блок запрещает атаку и движение.")]
    [SerializeField] private PlayerBlockController blockController;

    private NavMeshAgent agent;
    private Animator animator;
    private PlayerControls controls;
    private PlayerInteraction playerInteraction;
    private Camera mainCamera;
    private Vector2 moveInput;
    private Transform aimTarget;
    private float attackAimTimer;
    private Vector3 attackAimPoint;
    private bool isAttacking;
    private bool isDead;
    private float currentSpeed;
    private Vector3 lastMoveDirection;
    private bool comboQueued;
    private bool comboWindowOpen;
    private int comboIndex;
    public event System.Action AttackStarted;
    private float attackElapsed;
    private bool isJumping;
    private float jumpElapsed;
    private int jumpLayerIndex = -1;
    private int jumpPhaseHash;
    private bool initialized;
    private bool subscribed;

    private static readonly int AttackStateTag = Animator.StringToHash("Attack");

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// Идемпотентная активация: ссылки, агент и подписка на смерть.
    /// В EditMode Unity не вызывает <c>Awake</c>/<c>OnEnable</c>, поэтому тесты
    /// входят этим же путём, что и игра.
    /// </summary>
    internal void EnsureInitialized()
    {
        if (!initialized)
        {
            initialized = true;

            agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();
            mainCamera = Camera.main;
            controls = new PlayerControls();
            playerInteraction = GetComponent<PlayerInteraction>();

            if (agent != null)
            {
                agent.updateRotation = false;
                agent.speed = moveSpeed;
                agent.autoBraking = false;
                agent.acceleration = 20f;
            }

            if (weapon == null) weapon = GetComponentInChildren<Weapon>(true);
            if (weapon != null) weapon.SetHitboxActive(false);
        }

        SubscribeHealth();
    }

    private void SubscribeHealth()
    {
        if (subscribed) return;
        subscribed = true;

        health.OnDied += HandleDied;
        health.OnRespawned += HandleRespawned;
    }

    private void UnsubscribeHealth()
    {
        if (!subscribed) return;
        subscribed = false;

        health.OnDied -= HandleDied;
        health.OnRespawned -= HandleRespawned;
    }

    private void OnEnable()
    {
        EnsureInitialized();
        EnableControls();
    }

    private void EnableControls()
    {
        controls.Enable();
        controls.Player.Move.performed += OnMove;
        controls.Player.Move.canceled += OnMove;
        controls.Player.Interact.performed += OnInteract;
        controls.Player.Attack.performed += OnAttackPressed;
        controls.Player.Jump.performed += OnJumpPressed;
    }

    private void DisableControls()
    {
        controls.Player.Move.performed -= OnMove;
        controls.Player.Move.canceled -= OnMove;
        controls.Player.Interact.performed -= OnInteract;
        controls.Player.Attack.performed -= OnAttackPressed;
        controls.Player.Jump.performed -= OnJumpPressed;
        controls.Disable();
    }

    private void Start()
    {
        AimController aim = GetComponentInChildren<AimController>();
        if (aim != null) SetAimTarget(aim.transform);

        // Смерть могла наступить до подписки: приводим управление в согласованное состояние.
        if (health.IsDead) HandleDied();
    }

    private void OnDisable()
    {
        DisableControls();
        UnsubscribeHealth();
    }

    private void OnDestroy()
    {
        UnsubscribeHealth();
    }

    public void SetAimTarget(Transform target)
    {
        aimTarget = target;
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        // Взаимодействие обрабатывается отдельными компонентами.
    }

    private void OnAttackPressed(InputAction.CallbackContext context)
    {
        if (playerInteraction != null && !playerInteraction.CanStartAttack(comboWindowOpen))
            return;

        if (TryAttack())
            AttackStarted?.Invoke();
    }

    private void Update()
    {
        UpdateAttackState();
        UpdateJump();
        HandleMovement();
        HandleRotation();
        UpdateAnimation();
    }

    /// <summary>Текущая скорость как доля от максимума. Тесты проверяют остановку после смерти.</summary>
    internal float CurrentSpeedForTests => currentSpeed;

    /// <summary>Задаёт ввод движения. Тесты вызывают ту же логику, что и Input System.</summary>
    internal void SetMoveInputForTests(Vector2 input)
    {
        moveInput = input;
    }

    /// <summary>Прогон шага движения. В EditMode <c>Update</c> не тикает.</summary>
    internal void HandleMovementForTests()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        if (isAttacking || isDead || blockController.IsBlocking)
        {
            StopVelocity();
            return;
        }

        if (moveInput == Vector2.zero)
        {
            StopVelocity();
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        Vector3 forward = mainCamera.transform.forward;
        Vector3 right = mainCamera.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;
        currentSpeed = moveSpeed * Mathf.Clamp01(moveInput.magnitude);
        lastMoveDirection = moveDirection;

        if (AgentReady)
        {
            // Ручная запись agent.velocity конфликтовала с внутренним состоянием
            // NavMeshAgent: агент гасил скорость и персонаж залипал на месте.
            agent.isStopped = false;
            agent.Move(moveDirection * (moveSpeed * Time.deltaTime));
        }
        else
        {
            // Навмеш не запечён или агент вне сетки — двигаем трансформ напрямую.
            transform.position += moveDirection * (moveSpeed * Time.deltaTime);
        }
    }

    /// <summary>Агент реально стоит на навмеше и может принимать velocity/isStopped.</summary>
    private bool AgentReady
    {
        get { return agent != null && agent.enabled && agent.isOnNavMesh; }
    }

    private void StopVelocity()
    {
        currentSpeed = 0f;
        if (AgentReady) agent.isStopped = true;
    }

    private void HandleRotation()
    {
        if (isDead) return;

        if (attackAimTimer > 0f)
        {
            attackAimTimer -= Time.deltaTime;
            Vector3 attackDirection = attackAimPoint - transform.position;
            attackDirection.y = 0f;
            if (attackDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion attackRotation = Quaternion.LookRotation(attackDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, attackRotation, rotationSpeed * Time.deltaTime);
            }
            return;
        }

        if (aimTarget != null)
        {
            Vector3 direction = aimTarget.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
            return;
        }

        if (moveInput != Vector2.zero && currentSpeed > 0.1f)
        {
            Vector3 moveDir = lastMoveDirection.sqrMagnitude > 0.0001f ? lastMoveDirection : transform.forward;
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void UpdateAnimation()
    {
        float speed = isDead ? 0f : currentSpeed / Mathf.Max(0.1f, moveSpeed);
        SetAnimationSpeed(Mathf.Clamp01(speed));
    }

    private void SetAnimationSpeed(float normalizedSpeed)
    {
        if (animator != null) animator.SetFloat(speedParam, normalizedSpeed);
    }

    public void FaceTowards(Vector3 worldPoint, float duration)
    {
        attackAimPoint = worldPoint;
        attackAimTimer = duration;
    }

    private void UpdateAttackState()
    {
        if (!isAttacking)
        {
            if (animator != null)
            {
                AnimatorStateInfo idleState = animator.GetCurrentAnimatorStateInfo(0);
                if (idleState.tagHash != AttackStateTag) comboIndex = 0;
            }
            return;
        }

        attackElapsed += Time.deltaTime;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.tagHash != AttackStateTag)
        {
            // Переход из атаки в Locomotion/Block/Hit.
            if (attackElapsed > 0.2f) EndAttack();
            return;
        }

        if (comboQueued && comboWindowOpen && comboIndex < maxComboIndex)
        {
            comboIndex++;
            comboQueued = false;
            comboWindowOpen = false;

            animator.SetInteger(attackIndexParam, comboIndex);
            animator.ResetTrigger(comboTrigger);
            animator.SetTrigger(comboTrigger);
        }

        if (state.normalizedTime >= 0.99f)
            EndAttack();
    }

    private void EndAttack()
    {
        isAttacking = false;
        comboQueued = false;
        comboWindowOpen = false;
        attackElapsed = 0f;

        if (weaponManager != null) weaponManager.SetHitboxActive(false, true);
        else if (weapon != null) weapon.SetHitboxActive(false);

        ResumeMovement();
    }

    public void ResetHitboxState()
    {
        if (weaponManager != null) weaponManager.SetHitboxActive(false, true);
        else if (weapon != null) weapon.SetHitboxActive(false);
    }

    public bool IsAttacking => isAttacking;
    public bool IsJumping => isJumping;

    private void OnJumpPressed(InputAction.CallbackContext context)
    {
        TryJump();
    }

    /// <summary>
    /// Прыжок по дуге: NavMeshAgent.baseOffset поднимает агента над сеткой,
    /// горизонтальное движение при этом остаётся обычным.
    /// </summary>
    public bool TryJump()
    {
        if (isDead || isJumping) return false;
        if (isAttacking) return false;
        if (blockController.IsBlocking) return false;
        if (!AgentReady) return false;

        isJumping = true;
        jumpElapsed = 0f;

        ResolveJumpLayer();
        SetJumpPhase(1);
        if (weaponManager != null) weaponManager.SetHitboxActive(false, true);

        return true;
    }

    private void UpdateJump()
    {
        if (!isJumping) return;

        float duration = Mathf.Max(0.2f, jumpDuration);
        jumpElapsed += Time.deltaTime;

        float t = Mathf.Clamp01(jumpElapsed / duration);
        if (agent != null) agent.baseOffset = Mathf.Sin(t * Mathf.PI) * Mathf.Max(0.1f, jumpHeight);

        if (t >= 0.85f) SetJumpPhase(3);
        else if (t >= 0.45f) SetJumpPhase(2);

        if (t >= 1f) CancelJump();
    }

    private void ResolveJumpLayer()
    {
        if (animator == null || string.IsNullOrEmpty(jumpLayerName)) return;
        jumpLayerIndex = animator.GetLayerIndex(jumpLayerName);
        jumpPhaseHash = Animator.StringToHash(jumpPhaseParam);
    }

    private void SetJumpPhase(int phase)
    {
        if (animator == null || jumpLayerIndex < 0) return;
        animator.SetLayerWeight(jumpLayerIndex, phase > 0 ? 1f : 0f);
        animator.SetInteger(jumpPhaseHash, phase);
    }

    private void CancelJump()
    {
        isJumping = false;
        jumpElapsed = 0f;

        if (agent != null) agent.baseOffset = 0f;
        if (animator != null && jumpLayerIndex >= 0) animator.SetInteger(jumpPhaseHash, 0);
    }

    /// <summary>Запускает новую атаку или ставит следующую атаку комбо в буфер.</summary>
    public bool PlayAttackAnimation()
    {
        return TryAttack();
    }

    public bool TryAttack()
    {
        if (isDead) return false;
        if (isJumping) return false;
        if (blockController.IsBlocking) return false;
        if (animator == null) return false;

        if (!isAttacking)
        {
            StartAttack(0);
            return true;
        }

        // Одно нажатие ставит одно комбо только в открытом окне.
        if (comboWindowOpen && comboIndex < maxComboIndex && !comboQueued)
        {
            comboQueued = true;
            return true;
        }

        return false;
    }

    private void StartAttack(int index)
    {
        isAttacking = true;
        comboIndex = Mathf.Clamp(index, 0, maxComboIndex);
        comboQueued = false;
        comboWindowOpen = false;
        attackElapsed = 0f;

        animator.SetInteger(attackIndexParam, comboIndex);
        animator.ResetTrigger(attackTrigger);
        animator.SetTrigger(attackTrigger);

        StopMovement();
    }

    public void StopMovement()
    {
        if (AgentReady) agent.isStopped = true;
    }

    public void ResumeMovement()
    {
        if (isDead) return;
        if (blockController.IsBlocking) return;
        if (AgentReady) agent.isStopped = false;
    }

    public void OnAttackWindowOpen()
    {
        if (weaponManager != null) weaponManager.SetHitboxActive(true, true);
        else if (weapon != null) weapon.SetHitboxActive(true);
    }

    public void OnAttackWindowClose()
    {
        if (weaponManager != null) weaponManager.SetHitboxActive(false, true);
        else if (weapon != null) weapon.SetHitboxActive(false);
    }

    public void OnComboWindowOpen()
    {
        comboWindowOpen = true;
    }

    public void OnComboWindowClose()
    {
        comboWindowOpen = false;
    }

    /// <summary>
    /// Смерть касается только управления: движение и атака прекращаются.
    /// Анимация смерти и хитбокс — забота <see cref="PlayerDeathPresentation"/>.
    /// </summary>
    private void HandleDied()
    {
        isDead = true;
        isAttacking = false;
        comboQueued = false;
        comboWindowOpen = false;

        CancelJump();
        StopMovement();
        SetAnimationSpeed(0f);
    }

    private void HandleRespawned()
    {
        isDead = false;
        isAttacking = false;
        comboQueued = false;
        comboWindowOpen = false;

        ResumeMovement();
    }
}
