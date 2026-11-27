using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Universal melee hitbox. Enable it only during active attack frames.
/// - ignores the owner and already-hit targets (one hit per swing)
/// - asks ICombatBlockable first; a successful block deals no damage
/// - has an overlap fallback so fast animations do not miss triggers
/// </summary>
[RequireComponent(typeof(Collider))]
public class CombatHitbox : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField] private CombatTeam team = CombatTeam.Player;
    [SerializeField] private Transform ownerRoot;
    [SerializeField] private bool useOverlapFallback = true;
    [SerializeField] private LayerMask targetLayers = ~0;

    [Header("Проверка препятствий")]
    [Tooltip("Не бить сквозь стены: между владельцем и целью не должно быть препятствия.")]
    [SerializeField] private bool requireLineOfSight = true;
    [Tooltip("Слои, которые считаются препятствием для удара.")]
    [SerializeField] private LayerMask obstacleLayers = ~0;
    [Tooltip("Слои владельца: его собственное тело не должно считаться препятствием.")]
    [SerializeField] private LayerMask ownerLayers;

    [Header("Debug")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.15f, 0.1f, 0.75f);
    [SerializeField] private Vector3 fallbackBoxSize = new Vector3(0.5f, 0.5f, 1.4f);

    private Collider hitboxCollider;
    private bool active;
    private readonly HashSet<int> hitTargets = new HashSet<int>();
    private static readonly Collider[] OverlapBuffer = new Collider[32];
    private float? damageOverride;
    private float damageMultiplier = 1f;

    // Диагностика немого промаха: коллайдер есть, а IDamageable на нём нет.
    // Общий таймстемп, чтобы поток warning'ов не залил консоль.
    private const float MissWarningInterval = 2f;
    private static float nextMissWarningTime;

    public bool IsActive => active;
    public float Damage => damage;
    public CombatTeam Team => team;
    public Transform OwnerRoot => ownerRoot != null ? ownerRoot : transform.root;

    /// <summary>Фактический урон удара: override техники, иначе урон оружия × множитель пассивов.</summary>
    public float CurrentDamage => damageOverride ?? damage * Mathf.Max(0f, damageMultiplier);

    /// <summary>Временная замена урона (сила техники). null — вернуться к урону оружия.</summary>
    public void SetDamageOverride(float? value)
    {
        damageOverride = value.HasValue ? Mathf.Max(0f, value.Value) : (float?)null;
    }

    /// <summary>Множитель урона оружия от пассивов. Не влияет на override техники.</summary>
    public float DamageMultiplier
    {
        get => damageMultiplier;
        set => damageMultiplier = Mathf.Max(0f, value);
    }

    /// <summary>Слои целей. Для зон магии приходит из AbilityData.damageLayers.</summary>
    public LayerMask TargetLayers
    {
        get => targetLayers;
        set => targetLayers = value;
    }

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider>();
        if (hitboxCollider != null) hitboxCollider.isTrigger = true;
        if (ownerRoot == null) ownerRoot = transform.root;
        if (ownerRoot != null) ownerLayers = 1 << ownerRoot.gameObject.layer;
    }

    public void Configure(float newDamage, CombatTeam newTeam, Transform newOwner)
    {
        damage = Mathf.Max(0f, newDamage);
        team = newTeam;
        ownerRoot = newOwner != null ? newOwner : transform.root;

        // Свой слой запоминаем сразу: собственное тело не должно занимать буфер оверлапа
        // и не может считаться препятствием для луча.
        if (ownerRoot != null)
        {
            int layer = ownerRoot.gameObject.layer;
            ownerLayers = 1 << layer;
        }
    }

    public void SetActive(bool value)
    {
        active = value;
        if (value) hitTargets.Clear();
        if (hitboxCollider != null) hitboxCollider.enabled = value;
    }

    private void FixedUpdate()
    {
        if (!active || !useOverlapFallback) return;

        Vector3 half = GetWorldHalfExtents();
        int count = Physics.OverlapBoxNonAlloc(
            transform.position,
            half,
            OverlapBuffer,
            transform.rotation,
            targetLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider col = OverlapBuffer[i];
            if (col == null) continue;
            // Своё тело не занимает буфер: иначе 32 слота съедаются собственными коллайдерами.
            if (IsOwnCollider(col)) continue;
            TryDamage(col);
        }
    }

    private void OnTriggerEnter(Collider other) => TryDamage(other);
    private void OnTriggerStay(Collider other) => TryDamage(other);

    /// <summary>Коллайдер принадлежит владельцу хитбокса (сам корень или его потомок).</summary>
    private bool IsOwnCollider(Collider other)
    {
        Transform owner = OwnerRoot;
        if (owner == null || other == null) return false;

        Transform t = other.transform;
        return t == owner || t.IsChildOf(owner);
    }

    private void TryDamage(Collider other)
    {
        if (!active || other == null) return;
        if (IsOwnCollider(other)) return;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null)
        {
            // Молчаливый промах — самая дорогая в отладке поломка: коллайдер есть, а получателя нет.
            if (Time.time >= nextMissWarningTime)
            {
                nextMissWarningTime = Time.time + MissWarningInterval;
                Debug.LogWarning(
                    "[CombatHitbox] '" + name + "' попал в '" + other.name +
                    "' (слой " + LayerMask.LayerToName(other.gameObject.layer) +
                    "), но на нём нет IDamageable — урона не будет.", this);
            }
            return;
        }

        if (target.IsDead) return;

        Transform targetRoot = target.DamageRoot != null ? target.DamageRoot : other.transform.root;
        Transform myRoot = OwnerRoot;
        if (myRoot == null) myRoot = transform.root;

        if (targetRoot == myRoot || other.transform.root == myRoot) return;

        // Своих не бьём: два врага рядом не ранят друг друга. Neutral получает урон от всех.
        if (team != CombatTeam.Neutral && target.Team == team) return;

        // Сквозь стены урона нет: между владельцем и точкой удара не должно быть геометрии.
        if (requireLineOfSight && !HasLineOfSight(myRoot, other)) return;

        int id = targetRoot.GetInstanceID();
        if (hitTargets.Contains(id)) return;
        hitTargets.Add(id);

        float appliedDamage = CurrentDamage;
        Vector3 point = other.ClosestPoint(transform.position);
        DamageInfo info = new DamageInfo(appliedDamage, myRoot.gameObject, point);

        ICombatBlockable blockable = other.GetComponentInParent<ICombatBlockable>();
        if (blockable != null && blockable.IsBlocking && blockable.CanBlockFrom(myRoot.position))
        {
            blockable.OnBlocked(info);
            CombatEvents.Report(new CombatImpact
            {
                Point = point,
                Damage = 0f,
                Attacker = myRoot.gameObject,
                Victim = targetRoot,
                Kind = ImpactKind.Blocked
            });
            return;
        }

        target.TakeDamage(info);

        // Смерть отложена до конца кадра, поэтому «убил ли» спрашиваем до отчёта:
        // иначе killEffect и killShake не срабатывали бы никогда.
        bool killed = target.IsDead || target.WillDie;

        // Шина фидбека: хит-стоп, VFX и цифры урона подписаны снаружи.
        CombatEvents.Report(new CombatImpact
        {
            Point = point,
            Damage = appliedDamage,
            Attacker = myRoot.gameObject,
            Victim = targetRoot,
            Kind = killed ? ImpactKind.Killed : ImpactKind.Hit,
            IsCritical = info.IsCritical
        });
    }

    /// <summary>
    /// Проверка препятствия: луч от владельца к цели не должен упираться в геометрию.
    /// Попадание в саму цель или в другого получателя урона препятствием не считается.
    /// </summary>
    private bool HasLineOfSight(Transform myRoot, Collider target)
    {
        Vector3 origin = myRoot.position + Vector3.up * 1.2f;
        Vector3 destination = target.bounds.center;

        Vector3 direction = destination - origin;
        float distance = direction.magnitude;
        if (distance <= 0.05f) return true;

        int mask = obstacleLayers.value & ~ownerLayers.value;
        if (mask == 0) return true;

        if (!Physics.Raycast(origin, direction / distance, out RaycastHit hit, distance, mask,
                QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        return hit.collider.GetComponentInParent<IDamageable>() != null;
    }

    private Vector3 GetWorldHalfExtents()
    {
        BoxCollider box = hitboxCollider as BoxCollider;
        Vector3 size = box != null ? box.size : fallbackBoxSize;
        Vector3 scale = transform.lossyScale;
        return new Vector3(
            Mathf.Abs(size.x * scale.x) * 0.5f,
            Mathf.Abs(size.y * scale.y) * 0.5f,
            Mathf.Abs(size.z * scale.z) * 0.5f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = active ? gizmoColor : new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        BoxCollider box = GetComponent<BoxCollider>();
        Vector3 size = box != null ? box.size : fallbackBoxSize;
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}
