using UnityEngine;

/// <summary>
/// Аура магии: живёт на игроке и периодически бьёт вокруг него.
/// Навешивается в рантайме на инстанс VFX-префаба; урон — через CombatHitbox.
/// </summary>
public class MagicAura : MonoBehaviour
{
    private const float TickInterval = 0.5f;

    private float damage;
    private float radius;
    private float duration;
    private float zoneLifetime;
    private LayerMask targetLayers;
    private Transform owner;
    private float elapsed;
    private float tickTimer;
    private bool configured;

    /// <summary>Настройка ауры перед первым тиком.</summary>
    public void Configure(float auraDamage, float auraRadius, float auraDuration,
        float zoneLifetimeSeconds, Transform ownerRoot, LayerMask layers)
    {
        damage = Mathf.Max(0f, auraDamage);
        radius = Mathf.Max(0.1f, auraRadius);
        duration = Mathf.Max(0.1f, auraDuration);
        zoneLifetime = Mathf.Max(0.05f, zoneLifetimeSeconds);
        owner = ownerRoot;
        targetLayers = layers;
        configured = true;

        // Первый тик — сразу, чтобы аура чувствовалась в момент каста.
        tickTimer = 0f;
    }

    private void Update()
    {
        if (!configured) return;

        elapsed += Time.deltaTime;
        tickTimer -= Time.deltaTime;

        if (tickTimer <= 0f)
        {
            tickTimer = TickInterval;
            Tick();
        }

        if (elapsed >= duration) Destroy(gameObject);
    }

    /// <summary>Тик вокруг игрока: урон получают все, кто в радиусе.</summary>
    private void Tick()
    {
        MagicZone.Spawn(transform.position, radius, damage, zoneLifetime,
            owner != null ? owner : transform.root, targetLayers);
    }
}
