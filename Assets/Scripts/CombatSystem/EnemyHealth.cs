using System;
using UnityEngine;

/// <summary>
/// Health for the combat enemy. Implements IDamageable so CombatHitbox can damage it.
/// Blocking is handled by EnemyCombatAI before this component receives damage.
/// </summary>
[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 120f;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField, Min(0f)] private float destroyDelay = 6f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyCombatAI ai;
    [SerializeField] private Collider bodyCollider;

    private float currentHealth;
    private bool dead;
    private bool deathPending;

    public bool IsDead => dead;
    public Transform DamageRoot => transform;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (ai == null) ai = GetComponent<EnemyCombatAI>();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider>();

        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(DamageInfo info)
    {
        if (dead) return;

        float amount = Mathf.Max(0f, info.Amount);
        if (amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (ai != null) ai.NotifyDamaged(info);

        // Смерть доводится в конце кадра: урон приходит из физического колбэка
        // хитбокса, а отключение коллайдера и Destroy внутри него роняют редактор.
        if (currentHealth <= 0f) deathPending = true;
    }

    private void LateUpdate()
    {
        if (!deathPending) return;
        deathPending = false;
        Die();
    }

    public void Heal(float amount)
    {
        if (dead || amount <= 0f) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        if (dead) return;
        dead = true;
        deathPending = false;

        if (ai != null) ai.NotifyDeath();
        if (animator != null) animator.SetBool("IsDead", true);
        if (bodyCollider != null) bodyCollider.enabled = false;

        OnDied?.Invoke();

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.7f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 1.1f, new Vector3(0.9f, 2.2f, 0.9f));
    }
}
