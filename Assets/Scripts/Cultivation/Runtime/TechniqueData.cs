using UnityEngine;

/// <summary>Тип техники. Применение — зона AbilitySystem (Промт B), здесь только данные.</summary>
public enum TechniqueKind
{
    Active,
    Passive,
    Aura,
    Block
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
