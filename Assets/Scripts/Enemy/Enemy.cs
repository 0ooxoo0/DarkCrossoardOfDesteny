using UnityEngine;

public class Enemy1 : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private HarvestableObject harvestable;
    [SerializeField] private GameObject healthBarPrefab; // Префаб HealthBar

    private HealthBar healthBar;
    private float maxHealth = 100f;
    private float currentHealth;

    private void Start()
    {
        currentHealth = maxHealth;

        // Создаём HealthBar только если указан префаб
        if (healthBarPrefab != null)
        {
            GameObject barObj = Instantiate(healthBarPrefab, transform.position, Quaternion.identity);
            healthBar = barObj.GetComponent<HealthBar>();
            healthBar.Initialize(transform, maxHealth);
        }

        // Подписываемся на событие получения урона (можно через события, здесь упрощённо)
        // В реальном проекте лучше использовать события или вызывать из HarvestableObject
    }

    // Этот метод должен вызываться из HarvestableObject при TakeDamage
    public void OnTakeDamage(float damage)
    {
        currentHealth -= damage;
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth);

        if (currentHealth <= 0)
        {
            if (healthBar != null)
                healthBar.Hide();
            // Смерть обрабатывается в HarvestableObject
        }
    }
}