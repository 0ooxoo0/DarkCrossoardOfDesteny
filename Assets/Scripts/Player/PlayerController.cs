using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f; // для поворота к AimTarget

    [Header("Animation")]
    [SerializeField] private string speedParam = "Speed";

    private NavMeshAgent agent;
    private Animator animator;
    private PlayerControls controls;
    private Camera mainCamera;
    private Vector2 moveInput;
    private Transform aimTarget; // объект, за которым поворачиваемся (управляется AimController)

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

        // Отключаем авто-поворот агента
        agent.updateRotation = false;
        agent.speed = moveSpeed;
    }

    private void OnEnable()
    {
        controls.Enable();
        controls.Player.Move.performed += OnMove;
        controls.Player.Move.canceled += OnMove;
        // Можно добавить кнопку для взаимодействия, если нужна (например, подбирать предметы)
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
        // Оставим пустым, взаимодействие будет через триггер оружия
        // Но можно оставить для подбора предметов и т.п.
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
            agent.ResetPath();
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
        Vector3 targetPosition = transform.position + moveDirection * moveSpeed * Time.deltaTime;

        // Используем агента для перемещения с учётом препятствий
        if (agent.isOnNavMesh)
        {
            agent.SetDestination(targetPosition);
        }
    }

    private void HandleRotation()
    {
        // Если есть aimTarget, плавно поворачиваемся к нему (например, когда атакуем)
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

        // Если aimTarget нет, поворачиваемся в направлении движения
        if (moveInput != Vector2.zero)
        {
            Vector3 moveDir = (mainCamera.transform.forward * moveInput.y + mainCamera.transform.right * moveInput.x);
            moveDir.y = 0;
            if (moveDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    private void UpdateAnimation()
    {
        float speed = agent.velocity.magnitude / agent.speed;
        //animator.SetFloat(speedParam, speed);
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