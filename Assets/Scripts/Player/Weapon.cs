using UnityEngine;

public class Weapon : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float attackCooldown = 0.5f; // ����������� �������� ����� ���������� ����� ������ �������
    [SerializeField] private bool damageInteractables = false;

    private bool canDealDamage = true;
    private float lastAttackTime;
    private Collider hitboxCollider;

    public void SetHitboxActive(bool active)
    {
        if (hitboxCollider == null) hitboxCollider = GetComponent<Collider>();

        // false только выключает collider: Cube должен оставаться активным
        // для кулаков, а мечом управляет WeaponManager.
        if (active && !gameObject.activeSelf) gameObject.SetActive(true);
        if (hitboxCollider != null) hitboxCollider.enabled = active;
    }

    // ���� ����� ���������� �� �������� (Animation Event) ��� ��������� ����������
    public void EnableCollider()
    {
        canDealDamage = true;
        SetHitboxActive(true);
    }

    public void DisableCollider()
    {
        SetHitboxActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!damageInteractables) return;
        if (!canDealDamage) return;

        // ���������, ���� �� �� ������� ��������� IInteractable
        IInteractable interactable = other.GetComponent<IInteractable>();
        if (interactable != null)
        {
            // ��������� ������� ��� �������������� ������������� ����� �� ���� �����
            if (Time.time - lastAttackTime < attackCooldown) return;

            lastAttackTime = Time.time;
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            interactable.TakeDamage(damage, hitPoint);

            // ����� � �������
            Debug.Log($"<color=cyan>����� ������ {other.name} | ���: {interactable.GetResourceType()} | ����: {damage}</color>");

            // ������������� ����� ������� ������ �����, ���� � �.�.
        }
    }

    // ���� �����, ����� ���� ��������� ��� ������ ����� ���������� � ��������, ����� ������������ OnTriggerStay
    // �� ����� ���������� ���������
}