using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target; // объект, за которым следит камера

    [Header("Offset")]
    [SerializeField] private Vector3 offset = new Vector3(0, 10, -10); // смещение относительно цели

    [Header("Smoothness")]
    [SerializeField] private float smoothSpeed = 5f; // скорость плавного перемещения

    private void LateUpdate()
    {
        if (target == null) return;

        // Желаемая позиция камеры
        Vector3 desiredPosition = target.position + offset;
        // Плавное перемещение
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;

        // Камера смотрит на цель (если нужно, можно отключить)
        transform.LookAt(target);
    }
}