using UnityEngine;

public class BazeCharacterRotation : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float sensitivity = 1.5f;
    [SerializeField] private float smoothTime = 10f;
    [SerializeField] private float cameraDistance = 5f;
    [SerializeField] private float minVerticalAngle = -60f;
    [SerializeField] private float maxVerticalAngle = 80f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private Vector3 offset;
    [SerializeField] private float obstacleCheckRadius = 0.3f;
    [SerializeField] private LayerMask obstacleLayer = -1;

    [Header("References")]
    [SerializeField] private Transform target; // Цель (персонаж)
    [SerializeField] private Transform cameraTransform; // Трансформ камеры

    // Углы вращения
    private float currentHorizontalAngle = 0f;
    private float currentVerticalAngle = 30f;

    // Плавное движение
    private Vector3 currentCameraPosition;
    private Vector3 cameraVelocity = Vector3.zero;

    void Start()
    {
        // Настройка курсора
        //MenuSystem.Instance.moustrue();

        // Инициализация позиции камеры
        if (cameraTransform != null)
        {
            currentCameraPosition = cameraTransform.position;
        }

        // Автоматическое назначение цели, если не задано
        if (target == null)
        {
            target = transform.parent;
        }
    }

    void LateUpdate()
    {
        if (target == null || cameraTransform == null) return;

        // Получение ввода мыши
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // Обновление углов
        currentHorizontalAngle += mouseX * sensitivity;
        currentVerticalAngle -= mouseY * sensitivity;
        currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, minVerticalAngle, maxVerticalAngle);

        // Расчет идеальной позиции камеры
        Vector3 idealCameraPosition = CalculateCameraPosition();

        // Проверка на препятствия
        Vector3 finalCameraPosition = AvoidObstacles(idealCameraPosition);

        // Плавное перемещение камеры
        currentCameraPosition = Vector3.SmoothDamp(
            currentCameraPosition,
            finalCameraPosition,
            ref cameraVelocity,
            smoothTime * Time.deltaTime
        );

        // Применение позиции и поворота
        cameraTransform.position = currentCameraPosition;
        cameraTransform.LookAt(target.position + offset); // Смотрим на уровень головы
    }

    Vector3 CalculateCameraPosition()
    {
        if (target == null) return transform.position;

        // Преобразуем углы в направление
        float horizontalRad = currentHorizontalAngle * Mathf.Deg2Rad;
        float verticalRad = currentVerticalAngle * Mathf.Deg2Rad;

        // Расчет позиции камеры в сферических координатах
        float x = cameraDistance * Mathf.Cos(verticalRad) * Mathf.Sin(horizontalRad);
        float y = cameraDistance * Mathf.Sin(verticalRad);
        float z = cameraDistance * Mathf.Cos(verticalRad) * Mathf.Cos(horizontalRad);

        return target.position + new Vector3(x, y + 1.5f, z); // 1.5f - высота от земли
    }

    Vector3 AvoidObstacles(Vector3 targetPosition)
    {
        if (target == null) return targetPosition;

        Vector3 direction = targetPosition - target.position;
        float distance = direction.magnitude;

        // Проверяем, нет ли препятствий между камерой и персонажем
        RaycastHit hit;
        if (Physics.SphereCast(
            target.position + offset,
            obstacleCheckRadius,
            direction.normalized,
            out hit,
            distance,
            obstacleLayer))
        {
            // Если попали во что-то, кроме самого персонажа
            if (hit.collider.transform != target && !hit.collider.transform.IsChildOf(target))
            {
                // Возвращаем точку перед препятствием
                return hit.point - direction.normalized * 0.9f;
            }
        }

        return targetPosition;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    void OnDrawGizmosSelected()
    {
        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(target.position + Vector3.up * 1.5f, CalculateCameraPosition());

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(target.position + Vector3.up * 1.5f, obstacleCheckRadius);
        }
    }
}