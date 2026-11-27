using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Разрушаемый объект мира: здоровье, урон, смерть и выпадение добычи.
/// Если задана <see cref="LootTable"/>, предметы падают в контейнер через
/// <see cref="WorldLootService"/>; иначе работает прежний <see cref="DropManager"/>.
/// </summary>
public class HarvestableObject : MonoBehaviour, IInteractable, IDamageable
{
    [Header("Параметры объекта")]
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int objectLevel = 1;
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("Эффекты и точка выброса")]
    [SerializeField] private GameObject destroyEffectPrefab;
    [SerializeField] private Transform dropSpawnPoint;

    [Header("Добыча")]
    [Tooltip("Если задана, предметы падают в контейнер через WorldLootService.")]
    [SerializeField] private LootTable lootTable;

    private bool isDead;
    private bool deathPending;

    public event Action<float> Damaged;

    /// <summary>Объект умер: второй аргумент — точка смерти для эффектов.</summary>
    public event Action<HarvestableObject, Vector3> OnDied;

    public LootTable LootTable => lootTable;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Смерть доводится до конца здесь. Урон приходит из физического колбэка
    /// (OnTriggerStay/FixedUpdate хитбокса), а Instantiate эффекта, выброс лута
    /// и Destroy внутри физического шага роняют редактор нативным падением.
    /// </summary>
    private void LateUpdate()
    {
        if (!deathPending) return;
        deathPending = false;
        Die();
    }

    public void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (isDead) return;

        currentHealth -= damage;
        Damaged?.Invoke(damage);

        if (currentHealth <= 0f)
            deathPending = true;
    }

    /// <summary>
    /// Единый контур боя: тот же хитбокс, что бьёт врагов, бьёт дерево и кабана.
    /// </summary>
    public void TakeDamage(DamageInfo info)
    {
        TakeDamage(Mathf.Max(0f, info.Amount), info.HitPoint);
    }

    /// <summary>Мёртв ли объект.</summary>
    public bool IsDead => isDead;

    /// <summary>Смерть уже запланирована, но доводится в конце кадра.</summary>
    public bool WillDie => deathPending;

    /// <summary>Ресурсы и животные нейтральны: урон проходит и от игрока, и от врага.</summary>
    public CombatTeam Team => CombatTeam.Neutral;

    /// <summary>Корень, по которому CombatHitbox отличает владельца от цели.</summary>
    public Transform DamageRoot => transform;

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        deathPending = false;

        if (destroyEffectPrefab != null)
            Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);

        // Сначала сообщаем о смерти: EnemyAI и UI успевают отреагировать до удаления.
        OnDied?.Invoke(this, transform.position);

        DropLoot();

        Destroy(gameObject);
    }

    private void DropLoot()
    {
        if (lootTable == null)
        {
            DropLegacyPrefab();
            return;
        }

        WorldLootService service = WorldLootService.Instance;
        if (service == null)
        {
            // Без сервиса таблица не может создать контейнер — откатываемся на старый путь,
            // иначе предметы исчезнут молча.
            Debug.LogWarning("[HarvestableObject] Нет WorldLootService — используется старый DropManager.");
            DropLegacyPrefab();
            return;
        }

        var drops = new List<LootDrop>();
        lootTable.Roll(drops);

        Transform origin = dropSpawnPoint != null ? dropSpawnPoint : transform;
        for (int i = 0; i < drops.Count; i++)
        {
            if (!service.TryDrop(drops[i].Item, drops[i].Quantity, origin))
                Debug.LogError($"[HarvestableObject] Не удалось выбросить {drops[i].Item.DisplayName} x{drops[i].Quantity}.");
        }
    }

    private void DropLegacyPrefab()
    {
        GameObject itemPrefab = DropManager.GetDropItem(resourceType, objectLevel);
        if (itemPrefab == null) return;

        Vector3 spawnPos = dropSpawnPoint != null ? dropSpawnPoint.position : transform.position;
        Instantiate(itemPrefab, spawnPos, Quaternion.identity);
    }

    public ResourceType GetResourceType() => resourceType;
    public int GetLevel() => objectLevel;
}
