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
    [Tooltip("Длительность клипа приземления. Слой Jump гасится по концу клипа.")]
    [SerializeField, Min(0.05f)] private float landDuration = 0.45f;
    [Tooltip("Через сколько секунд приземления атака или уворот отменяют его.")]
    [SerializeField, Min(0f)] private float landCancelDelay = 0.18f;

    [Header("Dodge")]
    [Tooltip("Слой Animator с кувырками. 0 — слой выключен.")]
    [SerializeField] private string dodgeLayerName = "Dodge";
    [Tooltip("Int-параметр выбора кувырка: 0 — вперёд, 1 — назад, 2 — влево, 3 — вправо.")]
    [SerializeField] private string dodgeIndexParam = "DodgeIndex";
    [SerializeField] private string dodgeActiveParam = "IsDodging";
    [SerializeField] private Key dodgeKey = Key.LeftCtrl;
    [SerializeField] private bool alsoGamepadDodge = true;
    [SerializeField, Min(0.1f)] private float dodgeDistance = 3.2f;
    [SerializeField, Min(0.1f)] private float dodgeDuration = 0.85f;
    [Tooltip("Окно неуязвимости от начала кувырка, в секундах.")]
    [SerializeField, Min(0f)] private float dodgeInvulnerability = 0.45f;
    [SerializeField, Min(0f)] private float dodgeCooldown = 0.12f;
    [Tooltip("Доля кувырка, после которой атака отменяет его.")]
    [SerializeField, Range(0.1f, 1f)] private float dodgeCancelPhase = 0.6f;

    [Header("Attack buffer")]
    [Tooltip("Нажатие атаки запоминается на это время и срабатывает при открытии окна комбо.")]
    [SerializeField, Min(0f)] private float attackBufferWindow = 0.35f;
    [Tooltip("Сколько секунд атака неотменяема: защита от спама уворота.")]
    [SerializeField, Min(0f)] private float attackCommitTime = 0.12f;

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
    private bool isLanding;
    private float jumpLayerWeight;
    private float jumpElapsed;
    private float landElapsed;
    private int jumpLayerIndex = -1;
    private int jumpPhaseHash;
    private bool isDodging;
    private float dodgeElapsed;
    private Vector3 dodgeDirection;
    private int dodgeLayerIndex = -1;
    private int dodgeIndexHash;
    private int dodgeActiveHash;
    private float nextDodgeTime;
    // Момент истечения буфера, а не время нажатия: сравнение с нулём
    // ломалось на самом старте игры, когда Time.time ещё равен нулю.
    private float attackBufferedUntil = -1f;
    private bool comboWindowSeen;
    private float testTimeOffset;
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

        // Нажатие запоминается: пресс до окна комбо не должен пропадать.
        BufferAttack();

        if (TryAttack())
            AttackStarted?.Invoke();
    }

    private void Update()
    {
        PollDodgeInput();
        UpdateAttackState();
        UpdateJump();
        UpdateDodge();
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

    /// <summary>Индекс текущего удара комбо. Тесты проверяют рост комбо.</summary>
    internal int ComboIndexForTests => comboIndex;

    /// <summary>Нажатие атаки мимо контроллера ввода: тем же путём, что и игрок.</summary>
    internal void PressAttackForTests()
    {
        BufferAttack();
        TryAttack();
    }

    /// <summary>Запоминает нажатие атаки на окно буфера.</summary>
    private void BufferAttack()
    {
        attackBufferedUntil = Time.time + attackBufferWindow;
    }

    /// <summary>Прогон шага атаки. В EditMode <c>Update</c> не тикает.</summary>
    internal void UpdateAttackStateForTests()
    {
        UpdateAttackState();
    }

    /// <summary>Завершение атаки для тестов: в EditMode Update не тикает.</summary>
    internal void EndAttackForTests()
    {
        EndAttack();
    }

    /// <summary>
    /// Продвигает внутренние таймеры без ожидания реальных секунд.
    /// Сдвигается виртуальное «сейчас», а не метка буфера: иначе проверка
    /// возраста нажатия теряла бы смысл.
    /// </summary>
    internal void AdvanceAttackTimersForTests(float seconds)
    {
        attackElapsed += seconds;
        testTimeOffset += seconds;
    }

    private void HandleMovement()
    {
        // Кувырок ведёт себя сам: WASD во время него не мешает.
        if (isAttacking || isDead || isDodging || blockController.IsBlocking)
        {
            StopVelocity();
            return;
        }

        if (moveInput == Vector2.zero)
        {
            StopVelocity();
            return;
        }

        Vector3 moveDirection = CameraRelativeDirection(moveInput);
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

        // Во время кувырка персонаж смотрит туда, куда катится.
        if (isDodging)
        {
            if (dodgeDirection.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dodgeDirection),
                    rotationSpeed * 2f * Time.deltaTime);
            return;
        }

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

        if (!isDead && HasBufferedAttack && comboIndex < maxComboIndex)
        {
            // Окно комбо открыто — бьём сразу. Окно уже прошло — отменяем
            // восстановление анимации, чтобы следующий удар не ждал конца клипа.
            // Проверка до чтения состояния Animator: иначе буфер сгорал бы впустую.
            bool recovery = comboWindowSeen && !comboWindowOpen && attackElapsed >= attackCommitTime;

            if (comboWindowOpen || recovery) AdvanceCombo();
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.tagHash != AttackStateTag)
        {
            // Переход из атаки в Locomotion/Block/Hit.
            if (attackElapsed > 0.2f) EndAttack();
            return;
        }

        if (state.normalizedTime >= 0.99f)
            EndAttack();
    }

    private void EndAttack()
    {
        isAttacking = false;
        comboQueued = false;
        comboWindowOpen = false;
        comboWindowSeen = false;
        attackElapsed = 0f;
        attackBufferedUntil = -1f;
        testTimeOffset = 0f;

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
    public bool IsDodging => isDodging;

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
        if (isDead || isJumping || isDodging) return false;
        if (isAttacking && !CanCancelAttack) return false;
        if (blockController.IsBlocking) return false;
        if (!AgentReady) return false;

        if (isAttacking) EndAttack();

        isJumping = true;
        isLanding = false;
        jumpElapsed = 0f;
        landElapsed = 0f;

        ResolveJumpLayer();
        SetJumpPhase(1);
        if (weaponManager != null) weaponManager.SetHitboxActive(false, true);

        return true;
    }

    private void UpdateJump()
    {
        if (!isJumping) return;

        if (!isLanding)
        {
            float duration = Mathf.Max(0.2f, jumpDuration);
            jumpElapsed += Time.deltaTime;

            float t = Mathf.Clamp01(jumpElapsed / duration);
            if (agent != null) agent.baseOffset = Mathf.Sin(t * Mathf.PI) * Mathf.Max(0.1f, jumpHeight);

            if (t >= 0.85f) SetJumpPhase(3);
            else if (t >= 0.45f) SetJumpPhase(2);

            if (t >= 1f)
            {
                isLanding = true;
                landElapsed = 0f;
                if (agent != null) agent.baseOffset = 0f;
            }

            return;
        }

        // Приземление живёт своей длительностью: без явного конца слой Jump
        // остаётся с весом 1 и Animator застывает в последней позе.
        landElapsed += Time.deltaTime;
        if (landElapsed >= Mathf.Max(0.05f, landDuration)) CancelJump();
    }

    private void ResolveJumpLayer()
    {
        if (animator == null) return;

        if (!string.IsNullOrEmpty(jumpLayerName))
        {
            jumpLayerIndex = animator.GetLayerIndex(jumpLayerName);
            jumpPhaseHash = Animator.StringToHash(jumpPhaseParam);
        }

        ResolveDodgeLayer();
    }

    private void SetJumpPhase(int phase)
    {
        // Нулевая фаза обязана гасить вес слоя: забытый вес держал бы позу приземления вечно.
        jumpLayerWeight = phase > 0 ? 1f : 0f;

        if (animator == null || jumpLayerIndex < 0) return;

        animator.SetLayerWeight(jumpLayerIndex, jumpLayerWeight);
        animator.SetInteger(jumpPhaseHash, phase);
    }

    /// <summary>Вес слоя Jump. Тесты проверяют, что после приземления он возвращается в 0.</summary>
    internal float JumpLayerWeightForTests => jumpLayerWeight;

    /// <summary>Сброс прыжка для тестов: в EditMode Update не тикает.</summary>
    internal void CancelJumpForTests()
    {
        CancelJump();
    }

    private void CancelJump()
    {
        isJumping = false;
        isLanding = false;
        jumpElapsed = 0f;
        landElapsed = 0f;

        if (agent != null) agent.baseOffset = 0f;
        SetJumpPhase(0);
    }

    /// <summary>
    /// Кувырок: движение ведёт сам ролл, а не ввод. i-frames берутся у PlayerHealth.
    /// </summary>
    public bool TryDodge()
    {
        if (isDead || isDodging || isJumping) return false;
        if (blockController.IsBlocking) return false;
        if (!AgentReady) return false;
        if (Time.time < nextDodgeTime) return false;
        if (isAttacking && !CanCancelAttack) return false;

        if (isAttacking) EndAttack();

        dodgeDirection = ResolveDodgeDirection();
        if (dodgeDirection.sqrMagnitude < 0.0001f) dodgeDirection = transform.forward;

        isDodging = true;
        dodgeElapsed = 0f;
        nextDodgeTime = Time.time + dodgeDuration + dodgeCooldown;

        ResolveDodgeLayer();
        if (animator != null && dodgeLayerIndex >= 0)
        {
            animator.SetInteger(dodgeIndexHash, DodgeIndexFor(dodgeDirection));
            animator.SetLayerWeight(dodgeLayerIndex, 1f);
            // Bool, а не Trigger: AnyState потратил бы Trigger на первую ветку.
            animator.SetBool(dodgeActiveHash, false);
            animator.SetBool(dodgeActiveHash, true);
        }

        if (health != null && dodgeInvulnerability > 0f)
            health.SetInvulnerable(dodgeInvulnerability);

        // Хитбокс закрыт: кувырок не бьёт.
        if (weaponManager != null) weaponManager.SetHitboxActive(false, true);
        else if (weapon != null) weapon.SetHitboxActive(false);

        return true;
    }

    private void UpdateDodge()
    {
        if (!isDodging) return;

        float duration = Mathf.Max(0.1f, dodgeDuration);
        dodgeElapsed += Time.deltaTime;

        // Кувырок едет сам: фиксированная дистанция за время ролла.
        float speed = dodgeDistance / duration;
        if (AgentReady) agent.Move(dodgeDirection * (speed * Time.deltaTime));
        else transform.position += dodgeDirection * (speed * Time.deltaTime);

        if (dodgeElapsed >= duration) CancelDodge();
    }

    private void CancelDodge()
    {
        if (!isDodging) return;

        isDodging = false;
        dodgeElapsed = 0f;
        dodgeDirection = Vector3.zero;

        if (animator != null && dodgeLayerIndex >= 0)
        {
            // Вес в ноль обязателен: иначе слой держит последнюю позу ролла.
            animator.SetLayerWeight(dodgeLayerIndex, 0f);
            animator.SetBool(dodgeActiveHash, false);
            animator.SetInteger(dodgeIndexHash, 0);
        }
    }

    private void ResolveDodgeLayer()
    {
        if (animator == null || string.IsNullOrEmpty(dodgeLayerName)) return;

        dodgeLayerIndex = animator.GetLayerIndex(dodgeLayerName);
        dodgeIndexHash = Animator.StringToHash(dodgeIndexParam);
        dodgeActiveHash = Animator.StringToHash(dodgeActiveParam);
    }

    /// <summary>Направление кувырка: по вводу, при пустом вводе — назад.</summary>
    private Vector3 ResolveDodgeDirection()
    {
        if (moveInput.sqrMagnitude < 0.0001f) return -transform.forward;
        return CameraRelativeDirection(moveInput);
    }

    /// <summary>
    /// Индекс клипа по стороне кувырка. Направление переводится в локальные оси:
    /// в мировых осях ролл выбирался бы по сторонам света, а не по спине персонажа.
    /// </summary>
    private int DodgeIndexFor(Vector3 worldDirection)
    {
        Vector3 local = transform.InverseTransformDirection(worldDirection);

        if (Mathf.Abs(local.z) >= Mathf.Abs(local.x)) return local.z >= 0f ? 0 : 1;
        return local.x >= 0f ? 3 : 2;
    }


    /// <summary>Запускает новую атаку или ставит следующую атаку комбо в буфер.</summary>
    public bool PlayAttackAnimation()
    {
        return TryAttack();
    }

    public bool TryAttack()
    {
        if (isDead) return false;
        if (blockController.IsBlocking) return false;
        if (animator == null) return false;

        if (isDodging)
        {
            if (!CanCancelDodge) return false;
            CancelDodge();
        }

        if (isJumping)
        {
            if (!isLanding || landElapsed < landCancelDelay) return false;
            CancelJump();
        }

        if (!isAttacking)
        {
            StartAttack(0);
            return true;
        }

        // Новый удар — только после обязательных кадров: спам не рвёт анимацию.
        if (!CanCancelAttack) return false;
        if (comboIndex >= maxComboIndex) return false;

        comboQueued = true;
        return true;
    }

    /// <summary>Есть ли в буфере свежее нажатие атаки.</summary>
    private bool HasBufferedAttack
    {
        get
        {
            return Time.time + testTimeOffset < attackBufferedUntil;
        }
    }

    /// <summary>Следующий удар комбо: индекс растёт, буфер сбрасывается.</summary>
    private void AdvanceCombo()
    {
        comboIndex++;
        comboQueued = false;
        comboWindowOpen = false;
        comboWindowSeen = false;
        attackBufferedUntil = -1f;

        animator.SetInteger(attackIndexParam, comboIndex);
        animator.ResetTrigger(comboTrigger);
        animator.SetTrigger(comboTrigger);
    }

    /// <summary>Атаку можно отменить после обязательных кадров замаха.</summary>
    private bool CanCancelAttack
    {
        get { return isAttacking && attackElapsed >= attackCommitTime; }
    }

    /// <summary>Кувырок отменяется на исходе: выход из ролла без рывка.</summary>
    private bool CanCancelDodge
    {
        get { return isDodging && dodgeElapsed >= dodgeDuration * dodgeCancelPhase; }
    }

    private void StartAttack(int index)
    {
        isAttacking = true;
        comboIndex = Mathf.Clamp(index, 0, maxComboIndex);
        comboQueued = false;
        comboWindowOpen = false;
        comboWindowSeen = false;
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
        comboWindowSeen = true;
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
        comboWindowSeen = false;
        attackBufferedUntil = -1f;

        CancelJump();
        CancelDodge();
        StopMovement();
        SetAnimationSpeed(0f);
    }

    private void HandleRespawned()
    {
        isDead = false;
        isAttacking = false;
        comboQueued = false;
        comboWindowOpen = false;
        comboWindowSeen = false;
        attackBufferedUntil = -1f;

        CancelJump();
        CancelDodge();
        ResumeMovement();
    }


    /// <summary>
    /// Уворот опрашивается напрямую: клавиша и кнопка геймпада не требуют
    /// отдельного action в PlayerControls, как и блок у PlayerBlockController.
    /// </summary>
    private void PollDodgeInput()
    {
        bool pressed = Keyboard.current != null && Keyboard.current[dodgeKey].wasPressedThisFrame;

        if (!pressed && alsoGamepadDodge && Gamepad.current != null)
            pressed = Gamepad.current.buttonEast.wasPressedThisFrame;

        if (pressed) TryDodge();
    }


    /// <summary>Направление ввода в мировых координатах относительно камеры.</summary>
    private Vector3 CameraRelativeDirection(Vector2 input)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return transform.forward;
        }

        Vector3 forward = mainCamera.transform.forward;
        Vector3 right = mainCamera.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 direction = (forward * input.y + right * input.x).normalized;
        return direction.sqrMagnitude > 0.0001f ? direction : transform.forward;
    }
}
