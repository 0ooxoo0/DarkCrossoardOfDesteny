using UnityEngine;

public class HarvestableObject : MonoBehaviour, IInteractable
{
    [Header("Основные параметры")]
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int objectLevel = 1;
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("Визуальные эффекты")]
    [SerializeField] private GameObject destroyEffectPrefab; // Эффект при уничтожении

    [Header("Настройки дропа")]
    [SerializeField] private Transform dropSpawnPoint; // Точка появления предмета (если null - позиция объекта)

    private bool isDead = false;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log($"{gameObject.name} получил урон {damage}, осталось {currentHealth} HP");

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // Визуальный эффект
        if (destroyEffectPrefab != null)
            Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);

        // Создание дропа
        GameObject itemPrefab = DropManager.GetDropItem(resourceType, objectLevel);
        if (itemPrefab != null)
        {
            Vector3 spawnPos = dropSpawnPoint != null ? dropSpawnPoint.position : transform.position;
            Instantiate(itemPrefab, spawnPos, Quaternion.identity);
        }

        // Уничтожение объекта
        Destroy(gameObject);
    }

    public ResourceType GetResourceType() => resourceType;
    public int GetLevel() => objectLevel;
}