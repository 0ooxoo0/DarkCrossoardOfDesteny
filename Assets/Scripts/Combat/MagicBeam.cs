using UnityEngine;

/// <summary>
/// Луч магии: тикает урон вдоль отрезка от кастера к точке прицела.
/// Навешивается в рантайме на инстанс VFX-префаба; урон — через CombatHitbox.
/// </summary>
public class MagicBeam : MonoBehaviour
{
    private const float TickInterval = 0.2f;

    private Vector3 origin;
    private Vector3 target;
    private float damage;
    private float radius;
    private float duration;
    private float zoneLifetime;
    private LayerMask targetLayers;
    private Transform owner;
    private float elapsed;
    private float tickTimer;
    private bool configured;

    /// <summary>Настройка луча перед первым тиком.</summary>
    public void Configure(Vector3 aimPoint, float beamDamage, float beamRadius, float beamDuration,
        float zoneLifetimeSeconds, Transform ownerRoot, LayerMask layers)
    {
        origin = transform.position;
        target = aimPoint;
        damage = Mathf.Max(0f, beamDamage);
        radius = Mathf.Max(0.1f, beamRadius);
        duration = Mathf.Max(0.1f, beamDuration);
        zoneLifetime = Mathf.Max(0.05f, zoneLifetimeSeconds);
        owner = ownerRoot;
        targetLayers = layers;

        direction = target - origin;
        length = direction.magnitude;
        if (length > 0.0001f) direction /= length;

        transform.rotation = length > 0.0001f ? Quaternion.LookRotation(direction) : transform.rotation;
        configured = true;
    }

    private Vector3 direction;
    private float length;

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

    /// <summary>Один тик: зоны урона вдоль луча. CombatHitbox сам дедуплицирует попадания за тик.</summary>
    private void Tick()
    {
        float step = Mathf.Max(0.5f, radius);
        for (float travelled = 0f; travelled <= length; travelled += step)
        {
            MagicZone.Spawn(origin + direction * travelled, radius, damage, zoneLifetime,
                owner != null ? owner : transform.root, targetLayers);
        }
    }
}
