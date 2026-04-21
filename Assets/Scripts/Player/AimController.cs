using UnityEngine;
using UnityEngine.InputSystem;

public class AimController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float distanceFromPlayer = 2f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private bool useMouse = true; // если true Ч мышка управл€ет aimTarget

    private Camera mainCamera;
    private PlayerControls controls;
    private Vector2 lookInput;
    private Vector3 targetPosition;

    private void Awake()
    {
        mainCamera = Camera.main;
        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Enable();
        controls.Player.Look.performed += OnLook;
        controls.Player.Look.canceled += OnLook;
    }

    private void OnDisable()
    {
        controls.Player.Look.performed -= OnLook;
        controls.Player.Look.canceled -= OnLook;
        controls.Disable();
    }

    private void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    private void Update()
    {
        if (playerTransform == null) return;

        if (useMouse)
        {
            // Ќаправление от игрока к точке под мышкой
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane groundPlane = new Plane(Vector3.up, playerTransform.position);
            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 mouseWorldPos = ray.GetPoint(enter);
                Vector3 direction = (mouseWorldPos - playerTransform.position).normalized;
                targetPosition = playerTransform.position + direction * distanceFromPlayer;
            }
        }
        else
        {
            // ”правление стиком
            if (lookInput.sqrMagnitude > 0.1f)
            {
                Vector3 right = mainCamera.transform.right;
                Vector3 forward = Vector3.Cross(right, Vector3.up).normalized;
                Vector3 direction = (forward * lookInput.y + right * lookInput.x).normalized;
                targetPosition = playerTransform.position + direction * distanceFromPlayer;
            }
        }

        // ѕлавное перемещение AimTarget
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * rotationSpeed);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}