using UnityEngine;

/// <summary>Тип техники. Применение — зона AbilitySystem (Промт B), здесь только данные.</summary>
public enum TechniqueKind
{
    Active,
    Passive,
    Aura,
    Block
}

/// <summary>Как техника доставляет эффект. Выбирается в ассете, не в коде.</summary>
public enum DeliveryType
{
    /// <summary>Мгновенная зона урона в точке прицела.</summary>
    Impact,
    /// <summary>Видимый снаряд летит в точку прицела и взрывается там.</summary>
    Projectile,
    /// <summary>Луч от кастера к точке прицела с тиками урона.</summary>
    Beam,
    /// <summary>Эффект едет на игроке и бьёт вокруг него.</summary>
    Aura,
    /// <summary>Эффект на игроке без урона (щит, бафф).</summary>
    Self
}

/// <summary>
/// Данные техники: требования, стоимость, КД, VFX и клип замаха.
/// Никакой логики применения — это не зона культивации.
/// </summary>
[CreateAssetMenu(fileName = "Technique_New", menuName = "Cultivation/Technique", order = 2)]
public class TechniqueData : ScriptableObject
{
    [Header("Идентификация")]
    [SerializeField] private string techniqueId = "new_technique";
    [SerializeField] private string displayName = "Новая техника";
    [SerializeField] private string displayNameChinese = "";
    [SerializeField] private Sprite icon;
    [SerializeField, TextArea(2, 5)] private string description = "";

    [Header("Принадлежность")]
    [SerializeField] private DaoType daoType = DaoType.Sword;
    [SerializeField] private TechniqueKind kind = TechniqueKind.Active;
    [SerializeField, Min(1)] private int rank = 1;

    [Header("Требования")]
    [SerializeField, Min(0)] private int requiredDaoLevel = 1;
    [SerializeField, Min(0)] private int requiredRealmIndex = 0;

    [Header("Применение (использует AbilitySystem)")]
    [SerializeField, Min(0)] private int qiCost = 10;
    [SerializeField, Min(0f)] private float cooldown = 5f;
    [Tooltip("Базовая сила техники (урон/лечение). Используется исполнителем.")]
    [SerializeField, Min(0)] private int power = 25;
    [Tooltip("Клип замаха. Культивация не трогает Animator — поле только для AbilitySystem.")]
    [SerializeField] private AnimationClip castAnimationClip;
    [Tooltip("Префаб VFX (плейсхолдер: All In 1 Vfx Toolkit назначается в инспекторе).")]
    [SerializeField] private GameObject vfxPrefab;

    [Header("Прицел")]
    [Tooltip("Максимальная дальность каста, м. Точка прицела не дальше этого расстояния.")]
    [SerializeField, Min(1f)] private float range = 16f;
    [Tooltip("Радиус зоны урона в точке прицела, м.")]
    [SerializeField, Min(0.1f)] private float radius = 2f;

    [Header("Доставка")]
    [Tooltip("Impact — зона в точке прицела, Projectile — снаряд, Beam — луч, Aura/Self — эффект на игроке.")]
    [SerializeField] private DeliveryType delivery = DeliveryType.Impact;
    [Tooltip("Скорость снаряда, м/с. 0 — мгновенно.")]
    [SerializeField, Min(0f)] private float speed = 18f;
    [Tooltip("Время жизни снаряда/эффекта, с. 0 — до попадания.")]
    [SerializeField, Min(0f)] private float lifetime = 3f;
    [Tooltip("Префаб вспышки попадания. Пусто — только vfxPrefab.")]
    [SerializeField] private GameObject impactPrefab;
    [Tooltip("Префаб вспышки каста у руки. Пусто — без вспышки.")]
    [SerializeField] private GameObject muzzlePrefab;
    [Tooltip("Снаряд доворачивает на ближайшую цель по слоям урона.")]
    [SerializeField] private bool homing;

    [Header("Древо (очки Дао)")]
    [Tooltip("Стоимость изучения в очках Дао. 0 — узел стартовый и бесплатный.")]
    [SerializeField, Min(0)] private int unlockCost = 1;
    [Tooltip("ID техник-предпосылок в этом же Дао: без них узел не открыть.")]
    [SerializeField] private string[] prerequisiteIds = new string[0];
    [Tooltip("Стартовый узел ветки: доступен без предпосылок.")]
    [SerializeField] private bool starter;

    [Header("Пассивный скилл (Kind = Passive)")]
    [SerializeField] private PassiveEffectKind passiveEffect = PassiveEffectKind.MaxHealthPercent;
    [Tooltip("Прибавка в долях: 0.1 = +10%. 0 — пассив без эффекта.")]
    [SerializeField, Min(0f)] private float passiveValue;

    public string TechniqueId => techniqueId;
    public string DisplayName => displayName;
    public string DisplayNameChinese => displayNameChinese;
    public Sprite Icon => icon;
    public string Description => description;
    public DaoType DaoType => daoType;
    public TechniqueKind Kind => kind;
    public int Rank => Mathf.Max(1, rank);
    public int RequiredDaoLevel => Mathf.Max(0, requiredDaoLevel);
    public int RequiredRealmIndex => Mathf.Max(0, requiredRealmIndex);
    public int QiCost => Mathf.Max(0, qiCost);
    public float Cooldown => Mathf.Max(0f, cooldown);
    public int Power => Mathf.Max(0, power);
    public AnimationClip CastAnimationClip => castAnimationClip;
    public GameObject VfxPrefab => vfxPrefab;
    public float Range => Mathf.Max(1f, range);
    public float Radius => Mathf.Max(0.1f, radius);
    public DeliveryType Delivery => delivery;
    public float Speed => Mathf.Max(0f, speed);
    public float Lifetime => Mathf.Max(0f, lifetime);
    public GameObject ImpactPrefab => impactPrefab;
    public GameObject MuzzlePrefab => muzzlePrefab;
    public bool Homing => homing;
    public PassiveEffectKind PassiveEffect => passiveEffect;
    public float PassiveValue => Mathf.Max(0f, passiveValue);
    public int UnlockCost => Mathf.Max(0, unlockCost);
    public string[] PrerequisiteIds => prerequisiteIds ?? new string[0];
    public bool Starter => starter || unlockCost <= 0;

    /// <summary>Стоимость и предпосылки узла дерева — задаёт импортёр или сетап.</summary>
    public void SetTree(int cost, string[] prerequisites, bool isStarter)
    {
        unlockCost = Mathf.Max(0, cost);
        prerequisiteIds = prerequisites ?? new string[0];
        starter = isStarter;
    }

    public void Setup(string id, string name, string nameChinese, string desc, DaoType type, TechniqueKind techniqueKind,
        int techniqueRank, int daoLevel, int realmIndex, int cost, float cd, int techniquePower,
        Sprite techniqueIcon, AnimationClip castClip, GameObject vfx)
    {
        techniqueId = id;
        displayName = name;
        displayNameChinese = nameChinese;
        description = desc;
        daoType = type;
        kind = techniqueKind;
        rank = Mathf.Max(1, techniqueRank);
        requiredDaoLevel = Mathf.Max(0, daoLevel);
        requiredRealmIndex = Mathf.Max(0, realmIndex);
        qiCost = Mathf.Max(0, cost);
        cooldown = Mathf.Max(0f, cd);
        power = Mathf.Max(0, techniquePower);
        icon = techniqueIcon;
        castAnimationClip = castClip;
        vfxPrefab = vfx;
    }

    public void SetPassive(PassiveEffectKind effect, float value)
    {
        passiveEffect = effect;
        passiveValue = Mathf.Max(0f, value);
    }

    /// <summary>Дальность и радиус зоны каста — задаёт сетап ассетов или импортёр дерева.</summary>
    public void SetAim(float aimRange, float aimRadius)
    {
        range = Mathf.Max(1f, aimRange);
        radius = Mathf.Max(0.1f, aimRadius);
    }

    /// <summary>Тип доставки и полёт — задаёт сетап ассетов или импортёр дерева.</summary>
    public void SetDelivery(DeliveryType type, float travelSpeed, float life, GameObject impact, GameObject muzzle)
    {
        delivery = type;
        speed = Mathf.Max(0f, travelSpeed);
        lifetime = Mathf.Max(0f, life);
        impactPrefab = impact;
        muzzlePrefab = muzzle;
    }
    public string GetKindName()
    {
        switch (kind)
        {
            case TechniqueKind.Active: return "Активная";
            case TechniqueKind.Passive: return "Пассивная";
            case TechniqueKind.Aura: return "Аура";
            case TechniqueKind.Block: return "Блок";
            default: return kind.ToString();
        }
    }
}
