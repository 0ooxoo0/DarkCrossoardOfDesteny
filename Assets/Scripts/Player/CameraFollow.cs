using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target; // ������, �� ������� ������ ������

    [Header("Offset")]
    [SerializeField] private Vector3 offset = new Vector3(0, 10, -10); // �������� ������������ ����

    [Header("Smoothness")]
    [SerializeField] private float smoothSpeed = 5f; // �������� �������� �����������

    [Header("Shake")]
    [Tooltip("Сила тряски. CombatFeedback дёргает Shake() на попадании.")]
    [SerializeField, Min(0f)] private float shakeAmplitude = 0.22f;
    [SerializeField, Min(0.01f)] private float shakeDuration = 0.16f;

    private float shakeRemaining;
    private float shakeStrength;

    /// <summary>Добавляет тряску: берётся сильнейший импульс, остаток догорает.</summary>
    public void Shake(float strength = 1f)
    {
        shakeStrength = Mathf.Max(shakeStrength, shakeAmplitude * Mathf.Max(0f, strength));
        shakeRemaining = Mathf.Max(shakeRemaining, shakeDuration);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // �������� ������� ������
        Vector3 desiredPosition = target.position + offset;
        // ������� �����������
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;

        // ������ ������� �� ���� (���� �����, ����� ���������)
        transform.LookAt(target);

        if (shakeRemaining <= 0f) return;

        // Время нескалированное: хит-стоп не гасит тряску.
        shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.unscaledDeltaTime);
        float weight = shakeRemaining / Mathf.Max(0.01f, shakeDuration);
        transform.position += Random.insideUnitSphere * (shakeStrength * weight);

        if (shakeRemaining <= 0f) shakeStrength = 0f;
    }
}