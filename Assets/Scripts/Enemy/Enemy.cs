using UnityEngine;

public class Enemy1 : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private HarvestableObject harvestable;
    [SerializeField] private GameObject healthBarPrefab;

    private HealthBar healthBar;
    private float maxHealth = 100f;
    private float currentHealth;

    private void Start()
    {
        currentHealth = maxHealth;

        if (healthBarPrefab != null)
        {
            GameObject barObj = Instantiate(healthBarPrefab, transform.position, Quaternion.identity);
            healthBar = barObj.GetComponent<HealthBar>();
            healthBar.Initialize(transform, maxHealth);
        }

        if (harvestable != null)
        {
            harvestable.Damaged += OnTakeDamage;
        }
    }

    private void OnDestroy()
    {
        if (harvestable != null)
        {
            harvestable.Damaged -= OnTakeDamage;
        }
    }

    public void OnTakeDamage(float damage)
    {
        currentHealth -= damage;
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth);

        if (currentHealth <= 0)
        {
            if (healthBar != null)
                healthBar.Hide();
        }
    }
}
