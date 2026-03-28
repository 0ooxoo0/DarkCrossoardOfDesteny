using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float stoppingDistance = 0.5f;
    [SerializeField] private float moveSpeed = 5f; // для WASD

    [Header("Animation")]
    [SerializeField] private string speedParam = "Speed";

    private NavMeshAgent agent;
    private Animator animator;
    private PlayerControls controls;
    private Camera mainCamera;

    // Текущий режим управления
    public enum ControlMode { ClickToMove, WASD }
    public ControlMode currentMode = ControlMode.ClickToMove;

    // События для расширения
    public System.Action OnMoveToPoint;
    public System.Action OnReachDestination;

    // Переменные для WASD
    private Vector2 moveInput;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        mainCamera = Camera.main;

        controls = new PlayerControls();
        agent.stoppingDistance = stoppingDistance;

        // Настраиваем агента для WASD (будем вручную управлять позицией)
        // Для ClickToMove агент сам идёт к цели
    }

    private void OnEnable()
    {
        controls.Enable();

        // Подписываемся на общие действия
        controls.Player.Interact.performed += OnInteract;

        // В зависимости от режима подписываемся на разные действия
        if (currentMode == ControlMode.ClickToMove)
            SubscribeClickToMove();
        else
            SubscribeWASD();
    }

    private void OnDisable()
    {
        controls.Player.Interact.performed -= OnInteract;
        UnsubscribeClickToMove();
        UnsubscribeWASD();
        controls.Disable();
    }

    private void SubscribeClickToMove()
    {
        controls.Player.ClickToMove.performed += OnClickToMove;
    }

    private void UnsubscribeClickToMove()
    {
        controls.Player.ClickToMove.performed -= OnClickToMove;
    }

    private void SubscribeWASD()
    {
        controls.Player.Move.performed += OnMove;
        controls.Player.Move.canceled += OnMove;
    }

    private void UnsubscribeWASD()
    {
        controls.Player.Move.performed -= OnMove;
        controls.Player.Move.canceled -= OnMove;
    }

    // Обработчик клика для движения (ClickToMove)
    private void OnClickToMove(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
        {
            if (hit.collider.CompareTag("Ground"))
            {
                MoveToPoint(hit.point);
            }
        }
    }

    // Обработчик ввода для WASD
    private void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    // Взаимодействие (можно расширить)
    private void OnInteract(InputAction.CallbackContext context)
    {
        // Здесь рейкаст по левой кнопке
        Debug.Log("Interact");
    }

    public void MoveToPoint(Vector3 point)
    {
        if (agent.isOnNavMesh)
        {
            agent.SetDestination(point);
            OnMoveToPoint?.Invoke();
        }
    }

    private void Update()
    {
        if (currentMode == ControlMode.WASD)
        {
            UpdateWASDMovement();
        }
        UpdateAnimation();
        CheckDestinationReached();
    }

    private void UpdateWASDMovement()
    {
        if (moveInput == Vector2.zero)
        {
            // Останавливаем агента, если нет ввода
            if (agent.hasPath) agent.ResetPath();
            return;
        }

        // Преобразуем направление относительно камеры (вид сверху)
        Vector3 forward = mainCamera.transform.forward;
        Vector3 right = mainCamera.transform.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        Vector3 desiredMove = (forward * moveInput.y + right * moveInput.x).normalized;
        Vector3 targetPosition = transform.position + desiredMove * moveSpeed * Time.deltaTime;

        // Используем NavMeshAgent для перемещения (чтобы избежать препятствий)
        if (agent.isOnNavMesh)
        {
            agent.SetDestination(targetPosition);
        }
    }

    private void UpdateAnimation()
    {
        float speed = agent.velocity.magnitude / agent.speed;
        animator.SetFloat(speedParam, speed);
    }

    private void CheckDestinationReached()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                OnReachDestination?.Invoke();
            }
        }
    }

    // Публичные методы для смены режима из настроек
    public void SetControlMode(ControlMode mode)
    {
        if (currentMode == mode) return;

        // Отписываемся от текущего режима
        if (currentMode == ControlMode.ClickToMove)
            UnsubscribeClickToMove();
        else
            UnsubscribeWASD();

        currentMode = mode;

        // Подписываемся на новый режим
        if (currentMode == ControlMode.ClickToMove)
        {
            SubscribeClickToMove();
            // Если был активный ввод WASD – сбрасываем
            moveInput = Vector2.zero;
            if (agent.hasPath) agent.ResetPath();
        }
        else
        {
            SubscribeWASD();
            // Останавливаем движение агента, т.к. теперь управляем вручную
            agent.ResetPath();
        }
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