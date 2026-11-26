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

    [Header("Debug")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.15f, 0.1f, 0.75f);
    [SerializeField] private Vector3 fallbackBoxSize = new Vector3(0.5f, 0.5f, 1.4f);

    private Collider hitboxCollider;
    private bool active;
    private readonly HashSet<int> hitTargets = new HashSet<int>();
    private static readonly Collider[] OverlapBuffer = new Collider[32];

    public bool IsActive => active;
    public float Damage => damage;
    public CombatTeam Team => team;
    public Transform OwnerRoot => ownerRoot != null ? ownerRoot : transform.root;

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider>();
        if (hitboxCollider != null) hitboxCollider.isTrigger = true;
        if (ownerRoot == null) ownerRoot = transform.root;
    }

    public void Configure(float newDamage, CombatTeam newTeam, Transform newOwner)
    {
        damage = Mathf.Max(0f, newDamage);
        team = newTeam;
        ownerRoot = newOwner != null ? newOwner : transform.root;
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
            if (OverlapBuffer[i] != null)
                TryDamage(OverlapBuffer[i]);
        }
    }

    private void OnTriggerEnter(Collider other) => TryDamage(other);
    private void OnTriggerStay(Collider other) => TryDamage(other);

    private void TryDamage(Collider other)
    {
        if (!active || other == null) return;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null || target.IsDead) return;

        Transform targetRoot = target.DamageRoot != null ? target.DamageRoot : other.transform.root;
        Transform myRoot = OwnerRoot;
        if (myRoot == null) myRoot = transform.root;

        if (targetRoot == myRoot || other.transform.root == myRoot) return;

        int id = targetRoot.GetInstanceID();
        if (hitTargets.Contains(id)) return;
        hitTargets.Add(id);

        Vector3 point = other.ClosestPoint(transform.position);
        DamageInfo info = new DamageInfo(damage, myRoot.gameObject, point);

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

        // Шина фидбека: хит-стоп, VFX и цифры урона подписаны снаружи.
        CombatEvents.Report(new CombatImpact
        {
            Point = point,
            Damage = damage,
            Attacker = myRoot.gameObject,
            Victim = targetRoot,
            Kind = target.IsDead ? ImpactKind.Killed : ImpactKind.Hit,
            IsCritical = info.IsCritical
        });
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
