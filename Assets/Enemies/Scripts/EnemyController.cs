using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Universal enemy: swaps the model by visual id and applies a difficulty preset.
/// Visuals and difficulties live in ScriptableObject catalogues, so one prefab covers every enemy.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class EnemyController : MonoBehaviour
{
    public const string VisualRootName = "VisualRoot";

    [Header("Catalogues")]
    public EnemyVisualData[] availableVisuals;
    public DifficultyConfig[] availableDifficulties;

    [Header("Selection (inspector or code)")]
    public int currentVisualId;
    [Range(0, 3)] public int currentDifficultyIndex;

    [Header("Base stats (difficulty multipliers apply to these)")]
    [SerializeField] private float baseHealth = 120f;
    [SerializeField] private float baseDamage = 12f;
    [SerializeField] private float baseMoveSpeed = 3.5f;
    [SerializeField] private float baseAttackCooldown = 2f;

    [Header("Runtime references (filled by SetVisual)")]
    public Transform visualRoot;
    public GameObject currentVisual;
    public GameObject currentWeapon;
    public Animator currentAnimator;
    public EnemyHealth health;
    public EnemyCombatAI ai;
    public NavMeshAgent agent;

    public EnemyVisualData CurrentVisualData { get; private set; }
    public DifficultyConfig CurrentDifficulty { get; private set; }

    public float BaseHealth => baseHealth;
    public float BaseDamage => baseDamage;
    public float BaseMoveSpeed => baseMoveSpeed;

    private void Awake()
    {
        CacheComponents();
        SetDifficulty(currentDifficultyIndex);
        SetVisual(currentVisualId);
    }

    private void CacheComponents()
    {
        if (health == null) health = GetComponent<EnemyHealth>();
        if (ai == null) ai = GetComponent<EnemyCombatAI>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
    }

    public EnemyVisualData FindVisual(int id)
    {
        if (availableVisuals == null) return null;
        for (int i = 0; i < availableVisuals.Length; i++)
            if (availableVisuals[i] != null && availableVisuals[i].id == id) return availableVisuals[i];
        return null;
    }

    /// <summary>Rebuilds the model from the catalogue entry with this id.</summary>
    public void SetVisual(int id)
    {
        EnemyVisualData data = FindVisual(id);
        if (data == null || data.characterPrefab == null)
        {
            Debug.LogWarning("[EnemyController] No usable visual with id " + id + " on '" + name + "'", this);
            return;
        }

        currentVisualId = id;
        CurrentVisualData = data;

        Transform root = EnsureVisualRoot();
        ClearChildren(root);

        currentVisual = Instantiate(data.characterPrefab, root);
        currentVisual.name = data.characterPrefab.name;
        currentVisual.transform.localPosition = Vector3.zero;
        currentVisual.transform.localRotation = Quaternion.identity;

        currentAnimator = currentVisual.GetComponentInChildren<Animator>();
        if (currentAnimator != null)
        {
            // Движение делает NavMeshAgent. Root motion от Humanoid-клипов тянет модель
            // обратно и дерётся с агентом за позицию — рывки и застревания.
            currentAnimator.applyRootMotion = false;

            if (data.animatorController != null)
            {
                currentAnimator.runtimeAnimatorController = data.animatorController;
            }
            else if (currentAnimator.runtimeAnimatorController == null)
            {
                // Молчаливый враг без анимаций неотличим от сломанного боя: падаем громко.
                Debug.LogError(
                    "[EnemyController] У визуала '" + data.name + "' (id " + data.id +
                    ", " + data.displayName + ") не задан animatorController, и у модели его тоже нет — " +
                    "враг останется без анимаций. Заполни поле в Assets/Enemies/Data/" + data.name + ".asset.", this);
            }
        }

        AttachWeapon(data, root);
        DisableVisualColliders();
        RebindDependents();
    }

    /// <summary>Applies the difficulty preset at this catalogue index to the base stats.</summary>
    public void SetDifficulty(int index)
    {
        if (availableDifficulties == null || availableDifficulties.Length == 0) return;

        index = Mathf.Clamp(index, 0, availableDifficulties.Length - 1);
        DifficultyConfig config = availableDifficulties[index];
        if (config == null) return;

        currentDifficultyIndex = index;
        CurrentDifficulty = config;
        CacheComponents();

        // Здоровье масштабируем сами: EnemyCombatAI его не трогает.
        float healthValue = baseHealth * config.healthMultiplier;
        SetField(health, "maxHealth", healthValue);
        SetField(health, "currentHealth", healthValue);

        // Боевые поля пишем БАЗОВЫМИ: множители ступени применяет EnemyCombatAI.ApplyDifficulty.
        // Иначе урон и скорость умножались бы дважды — здесь и в пресете.
        SetField(ai, "attackDamage", baseDamage);
        SetField(ai, "moveSpeed", baseMoveSpeed);
        SetField(ai, "attackCooldown", baseAttackCooldown);

        if (ai != null) ai.ApplyDifficulty(config);
        else if (agent != null) agent.speed = baseMoveSpeed * config.speedMultiplier;
    }

    [ContextMenu("Apply Visual + Difficulty")]
    private void ApplySelection()
    {
        SetDifficulty(currentDifficultyIndex);
        SetVisual(currentVisualId);
    }

    private Transform EnsureVisualRoot()
    {
        if (visualRoot != null) return visualRoot;

        Transform existing = transform.Find(VisualRootName);
        if (existing != null)
        {
            visualRoot = existing;
            return visualRoot;
        }

        GameObject created = new GameObject(VisualRootName);
        created.transform.SetParent(transform, false);
        visualRoot = created.transform;
        return visualRoot;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                // Destroy() is deferred; unparent first so the old model leaves VisualRoot at once.
                child.transform.SetParent(null, false);
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private void AttachWeapon(EnemyVisualData data, Transform root)
    {
        currentWeapon = null;
        if (data.weaponPrefab == null) return;

        Transform bone = FindBone(currentVisual.transform, data.weaponBoneName);
        Transform parent = bone != null ? bone : root;

        currentWeapon = Instantiate(data.weaponPrefab, parent);
        currentWeapon.name = data.weaponPrefab.name;
        currentWeapon.transform.localPosition = data.weaponLocalPosition;
        currentWeapon.transform.localRotation = Quaternion.Euler(data.weaponLocalEuler);
        currentWeapon.transform.localScale = data.weaponLocalScale;
    }

    private static Transform FindBone(Transform root, string boneName)
    {
        if (root == null || string.IsNullOrEmpty(boneName)) return null;

        Transform partial = null;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == root) continue;
            if (t.name == boneName) return t;
            if (partial == null && t.name.IndexOf(boneName, System.StringComparison.OrdinalIgnoreCase) >= 0) partial = t;
        }
        return partial;
    }

    /// <summary>Model and weapon colliders stay off so the root's CombatHitbox owns damage.</summary>
    private void DisableVisualColliders()
    {
        if (currentVisual != null)
            foreach (Collider c in currentVisual.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        if (currentWeapon != null)
            foreach (Collider c in currentWeapon.GetComponentsInChildren<Collider>(true)) c.enabled = false;
    }

    private void RebindDependents()
    {
        SetField(health, "animator", currentAnimator);
        SetField(ai, "animator", currentAnimator);
    }

    // ponytail: reflection because EnemyHealth/EnemyCombatAI keep their stats private with no setters.
    // Upgrade path: add public setters there and drop this helper.
    private static void SetField(object target, string fieldName, object value)
    {
        if (target == null) return;

        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null) return;

        field.SetValue(target, value);
    }
}
