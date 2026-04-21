using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Animation")]
    [SerializeField] private string speedParam = "Speed";

    private NavMeshAgent agent;
    private Animator animator;
    private PlayerControls controls;
    private Camera mainCamera;
    private Vector2 moveInput;
    private Transform aimTarget;

    void Start()
    {
        AimController aim = GetComponentInChildren<AimController>();
        if (aim != null) SetAimTarget(aim.transform);
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        mainCamera = Camera.main;
        controls = new PlayerControls();

        agent.updateRotation = false;
        agent.speed = moveSpeed;
        // Отключаем автоматическое торможение, чтобы агент не тормозил сам при потере пути
        agent.autoBraking = false;
        // Увеличиваем ускорение для более резвого старта/остановки (можно подстроить под ощущения)
        agent.acceleration = 20f;
    }

    private void OnEnable()
    {
        controls.Enable();
        controls.Player.Move.performed += OnMove;
        controls.Player.Move.canceled += OnMove;
        controls.Player.Interact.performed += OnInteract;
    }

    private void OnDisable()
    {
        controls.Player.Move.performed -= OnMove;
        controls.Player.Move.canceled -= OnMove;
        controls.Player.Interact.performed -= OnInteract;
        controls.Disable();
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
        // Пусто — взаимодействие обрабатывается через триггер оружия
    }

    private void Update()
    {
        HandleMovement();
        HandleRotation();
        UpdateAnimation();
    }

    private void HandleMovement()
    {
        if (moveInput == Vector2.zero)
        {
            // Если нет ввода, останавливаем агента мгновенно
            agent.velocity = Vector3.zero;
            return;
        }

        // Направление относительно камеры
        Vector3 forward = mainCamera.transform.forward;
        Vector3 right = mainCamera.transform.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;
        Vector3 desiredVelocity = moveDirection * moveSpeed;

        // Назначаем желаемую скорость агенту. Навигационный меш сам ограничит движение через препятствия.
        agent.velocity = desiredVelocity;
    }

    private void HandleRotation()
    {
        if (aimTarget != null)
        {
            Vector3 direction = aimTarget.position - transform.position;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
            return;
        }

        // Поворот по направлению движения (только когда есть ввод)
        if (moveInput != Vector2.zero && agent.velocity.magnitude > 0.1f)
        {
            Vector3 moveDir = agent.velocity.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void UpdateAnimation()
    {
        float speed = agent.velocity.magnitude / moveSpeed;
        // Раскомментируйте, если аниматор использует параметр Speed
        // animator.SetFloat(speedParam, speed);
    }

    public void StopMovement()
    {
        agent.isStopped = true;
    }

    public void ResumeMovement()
    {
        agent.isStopped = false;
    }
}