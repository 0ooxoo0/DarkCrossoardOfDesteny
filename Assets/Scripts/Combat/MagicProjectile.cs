using UnityEngine;

/// <summary>
/// Полёт магического снаряда к точке прицела. Навешивается в рантайме на инстанс
/// VFX-префаба: коллайдеры и Rigidbody в 57 префабах добавлять не нужно.
/// Урон наносит существующий CombatHitbox — блокировка и дедупликация уже внутри него.
/// </summary>
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public class MagicProjectile : MonoBehaviour
{
    private static readonly Collider[] HomingBuffer = new Collider[16];

    private Vector3 target;
    private float speed;
    private float radius;
    private float lifetime;
    private float damage;
    private float hitboxLifetime;
    private LayerMask targetLayers;
    private GameObject impactPrefab;
    private Transform owner;
    private bool homing;
    private float elapsed;
    private bool fired;
    private bool configured;

    /// <summary>Настройка снаряда перед первым кадром полёта.</summary>
    public void Configure(Vector3 aimPoint, float projectileSpeed, float life, float damageRadius,
        float projectileDamage, float zoneLifetime, Transform ownerRoot, LayerMask layers,
        GameObject impact, bool useHoming)
    {
        target = aimPoint;
        speed = Mathf.Max(0f, projectileSpeed);
        lifetime = Mathf.Max(0f, life);
        radius = Mathf.Max(0.05f, damageRadius);
        damage = Mathf.Max(0f, projectileDamage);
        hitboxLifetime = Mathf.Max(0.05f, zoneLifetime);
        owner = ownerRoot;
        targetLayers = layers;
        impactPrefab = impact;
        homing = useHoming;

        var sphere = GetComponent<SphereCollider>();
        if (sphere == null) sphere = gameObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = radius;

        var body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        // Скорость 0 — мгновенный эффект: взрываемся в точке прицела сразу.
        configured = true;
        if (speed <= 0.0001f) Explode();
    }

    private void Start()
    {
        // Компонент мог быть навешен на инстанс без Configure (ручная проверка в инспекторе).
        if (!configured) Explode();
    }

    private void Update()
    {
        if (fired || !configured) return;

        elapsed += Time.deltaTime;

        if (homing) target = ResolveHomingTarget();

        Vector3 position = transform.position;
        Vector3 next = Vector3.MoveTowards(position, target, speed * Time.deltaTime);
        transform.position = next;

        Vector3 direction = target - position;
        if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction);

        bool reached = (next - target).sqrMagnitude <= 0.0001f;
        if (reached || (lifetime > 0f && elapsed >= lifetime)) Explode();
    }

    /// <summary>Ближайшая цель по слоям урона — только для homing. Иначе фиксированная точка прицела.</summary>
    private Vector3 ResolveHomingTarget()
    {
        // NonAlloc: homing ищет цель каждый кадр, аллокации здесь недопустимы.
        int count = Physics.OverlapSphereNonAlloc(transform.position, Mathf.Max(6f, radius * 3f),
            HomingBuffer, targetLayers, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        Vector3 point = target;

        for (int i = 0; i < count; i++)
        {
            Collider hit = HomingBuffer[i];
            if (hit == null) continue;
            if (owner != null && hit.transform.root == owner) continue;

            IDamageable victim = hit.GetComponentInParent<IDamageable>();
            if (victim == null || victim.IsDead) continue;

            float distance = (hit.bounds.center - transform.position).sqrMagnitude;
            if (distance >= best) continue;

            best = distance;
            point = hit.bounds.center;
        }

        return point;
    }

    /// <summary>Вспышка попадания, временная зона урона с CombatHitbox, затем самоуничтожение.</summary>
    private void Explode()
    {
        if (fired) return;
        fired = true;

        if (impactPrefab != null)
            Destroy(Instantiate(impactPrefab, transform.position, transform.rotation), 5f);

        MagicZone.Spawn(transform.position, radius, damage, hitboxLifetime,
            owner != null ? owner : transform.root, targetLayers);

        Destroy(gameObject);
    }
}
