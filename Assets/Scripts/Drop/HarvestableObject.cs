using UnityEngine;

public class HarvestableObject : MonoBehaviour, IInteractable
{
    [Header("�������� ���������")]
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int objectLevel = 1;
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("���������� �������")]
    [SerializeField] private GameObject destroyEffectPrefab; // ������ ��� �����������

    [Header("��������� �����")]
    [SerializeField] private Transform dropSpawnPoint; // ����� ��������� �������� (���� null - ������� �������)

    private bool isDead = false;

    public event System.Action<float> Damaged;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (isDead) return;

        currentHealth -= damage;
        Damaged?.Invoke(damage);
        Debug.Log($"{gameObject.name} ������� ���� {damage}, �������� {currentHealth} HP");

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // ���������� ������
        if (destroyEffectPrefab != null)
            Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);

        // �������� �����
        GameObject itemPrefab = DropManager.GetDropItem(resourceType, objectLevel);
        if (itemPrefab != null)
        {
            Vector3 spawnPos = dropSpawnPoint != null ? dropSpawnPoint.position : transform.position;
            Instantiate(itemPrefab, spawnPos, Quaternion.identity);
        }

        // ����������� �������
        Destroy(gameObject);
    }

    public ResourceType GetResourceType() => resourceType;
    public int GetLevel() => objectLevel;
}