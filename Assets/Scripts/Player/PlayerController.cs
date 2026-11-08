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
    [SerializeField] private string attackTrigger = "Attack";

    private NavMeshAgent agent;
    private Animator animator;
    private PlayerControls controls;
    private Camera mainCamera;
    private Vector2 moveInput;
    private Transform aimTarget;
    private float attackAimTimer;
    private Vector3 attackAimPoint;

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
        // ��������� �������������� ����������, ����� ����� �� �������� ��� ��� ������ ����
        agent.autoBraking = false;
        // ����������� ��������� ��� ����� ������� ������/��������� (����� ���������� ��� ��������)
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
        // ����� � �������������� �������������� ����� ������� ������
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
            // ���� ��� �����, ������������� ������ ���������
            agent.velocity = Vector3.zero;
            return;
        }

        // ����������� ������������ ������
        Vector3 forward = mainCamera.transform.forward;
        Vector3 right = mainCamera.transform.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;
        Vector3 desiredVelocity = moveDirection * moveSpeed;

        // ��������� �������� �������� ������. ������������� ��� ��� ��������� �������� ����� �����������.
        agent.velocity = desiredVelocity;
    }

    private void HandleRotation()
    {
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
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
            return;
        }

        // ������� �� ����������� �������� (������ ����� ���� ����)
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
        animator.SetFloat(speedParam, Mathf.Clamp01(speed));
    }

    public void FaceTowards(Vector3 worldPoint, float duration)
    {
        attackAimPoint = worldPoint;
        attackAimTimer = duration;
    }

    public bool PlayAttackAnimation()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("Attack") && state.normalizedTime < 0.95f)
        {
            return false;
        }

        animator.ResetTrigger(attackTrigger);
        animator.SetTrigger(attackTrigger);
        return true;
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