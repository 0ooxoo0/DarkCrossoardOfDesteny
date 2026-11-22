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

    [Header("Attack")]
    [SerializeField] private Weapon weapon;
    [SerializeField] private WeaponManager weaponManager;

    [Header("Combat References")]
    [SerializeField] private PlayerBlockController blockController;
    [SerializeField] private PlayerHealth health;

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

    private static readonly int AttackStateTag = Animator.StringToHash("Attack");

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        mainCamera = Camera.main;
        controls = new PlayerControls();
        playerInteraction = GetComponent<PlayerInteraction>();

        if (blockController == null) blockController = GetComponent<PlayerBlockController>();
        if (health == null) health = GetComponent<PlayerHealth>();

        agent.updateRotation = false;
        agent.speed = moveSpeed;
        agent.autoBraking = false;
        agent.acceleration = 20f;

        if (weapon == null) weapon = GetComponentInChildren<Weapon>(true);
        if (weapon != null) weapon.SetHitboxActive(false);
    }

    private void OnEnable()
    {
        controls.Enable();
        controls.Player.Move.performed += OnMove;
        controls.Player.Move.canceled += OnMove;
        controls.Player.Interact.performed += OnInteract;
        controls.Player.Attack.performed += OnAttackPressed;
    }

    private void OnDisable()
    {
        controls.Player.Move.performed -= OnMove;
        controls.Player.Move.canceled -= OnMove;
        controls.Player.Interact.performed -= OnInteract;
        controls.Player.Attack.performed -= OnAttackPressed;
        controls.Disable();
    }

    private void Start()
    {
        AimController aim = GetComponentInChildren<AimController>();
        if (aim != null) SetAimTarget(aim.transform);

        if (health != null)
        {
            health.OnDied += HandleDied;
            health.OnRespawned += HandleRespawned;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
            health.OnRespawned -= HandleRespawned;
        }
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
        HandleMovement();
        HandleRotation();
        UpdateAnimation();
    }

    private void HandleMovement()
    {
        if (isAttacking || isDead || (blockController != null && blockController.IsBlocking))
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
            agent.velocity = moveDirection * moveSpeed;
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
        if (AgentReady) agent.velocity = Vector3.zero;
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
        }    }

    private void UpdateAnimation()
    {
        if (animator == null) return;
        float speed = isDead ? 0f : currentSpeed / Mathf.Max(0.1f, moveSpeed);
        animator.SetFloat(speedParam, Mathf.Clamp01(speed));
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

    /// <summary>Запускает новую атаку или ставит следующую атаку комбо в буфер.</summary>
    public bool PlayAttackAnimation()
    {
        return TryAttack();
    }

    public bool TryAttack()
    {
        if (isDead) return false;
        if (blockController != null && blockController.IsBlocking) return false;
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
        if (blockController != null && blockController.IsBlocking) return;
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

    private void HandleDied()
    {
        isDead = true;
        isAttacking = false;
        comboQueued = false;
        comboWindowOpen = false;

        if (animator != null) animator.SetBool("IsDead", true);
        StopMovement();
        if (weaponManager != null) weaponManager.SetHitboxActive(false, true);
    }

    private void HandleRespawned()
    {
        isDead = false;
        isAttacking = false;
        comboQueued = false;
        comboWindowOpen = false;

        if (animator != null)
        {
            animator.SetBool("IsDead", false);
            animator.ResetTrigger("Hit");
        }

        ResumeMovement();
    }
}
