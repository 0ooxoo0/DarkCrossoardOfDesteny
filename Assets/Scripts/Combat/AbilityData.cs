using System.Collections.Generic;
using UnityEngine;

public enum AbilityType
{
    Melee,
    Magic
}

/// <summary>Как способность доставляет эффект. Дублирует DeliveryType из TechniqueData для способностей оружия.</summary>
public enum AbilityDelivery
{
    Impact,
    Projectile,
    Beam,
    Aura,
    Self
}

[CreateAssetMenu(menuName = "Combat/Ability Data", fileName = "NewAbility")]
public class AbilityData : ScriptableObject
{
    [Header("Identity")]
    public string abilityId;
    public string displayName;
    public Sprite icon;
    public KeyCode hotkey = KeyCode.Alpha1;

    [Header("Behaviour")]
    public AbilityType type;
    public float cooldown = 1f;
    public GameObject vfxPrefab;
    public AnimationClip castAnimation;
    public WeaponType requiredWeapon = WeaponType.Any;

    [Header("Cultivation Requirements")]
    [Tooltip("Если включено, способность требует активный Дао и его уровень.")]
    [SerializeField] private bool useRequiredDao;
    [SerializeField] private DaoType requiredDaoType = DaoType.Sword;
    [Tooltip("0 — без требования к уровню Дао.")]
    [Min(0)] public int requiredDaoLevel;
    [Tooltip("0 — без требования к реалму; иначе сравнивается CurrentRealmIndex.")]
    [Min(0)] public int requiredRealmIndex;
    [Tooltip("0 — способность бесплатна.")]
    [Min(0)] public int qiCost;

    /// <summary>null — способность не привязана к Дао. В инспекторе задаётся парой useRequiredDao + requiredDaoType.</summary>
    public DaoType? requiredDao
    {
        get => useRequiredDao ? requiredDaoType : (DaoType?)null;
        set
        {
            useRequiredDao = value.HasValue;
            if (value.HasValue) requiredDaoType = value.Value;
        }
    }

    [Header("Damage")]
    [Tooltip("Сила техники (урон). 0 — используется урон оружия.")]
    [Min(0)] public int power;
    [Tooltip("Сейчас используется только первый тег (по умолчанию DammageObject). Список оставлен для расширения.")]
    public List<string> damageTags = new List<string> { "DammageObject" };
    public LayerMask damageLayers = ~0;

    [Header("Magic")]
    public float magicRange = 2.5f;
    public float magicRadius = 2f;
    public float magicSpawnDistance = 1.5f;
    public float vfxDelay = 0.2f;
    public float vfxLifetime = 2f;
    public float magicHitboxLifetime = 0.2f;

    [Header("Доставка")]
    [Tooltip("Impact — зона в точке прицела, Projectile — снаряд, Beam — луч, Aura/Self — эффект на игроке.")]
    public AbilityDelivery delivery = AbilityDelivery.Impact;
    [Tooltip("Скорость снаряда, м/с. 0 — мгновенно.")]
    [Min(0f)] public float speed = 18f;
    [Tooltip("Время жизни снаряда/эффекта, с. 0 — до попадания.")]
    [Min(0f)] public float lifetime = 3f;
    [Tooltip("Префаб вспышки попадания.")]
    public GameObject impactPrefab;
    [Tooltip("Префаб вспышки каста у руки.")]
    public GameObject muzzlePrefab;
    [Tooltip("Снаряд доворачивает на ближайшую цель по слоям урона.")]
    public bool homing;

    // Первый непустой тег списка. Поведение сейчас прежнее: только DammageObject.
    public string PrimaryDamageTag
    {
        get
        {
            if (damageTags != null)
            {
                for (int i = 0; i < damageTags.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(damageTags[i]))
                        return damageTags[i];
                }
            }

            return "DammageObject";
        }
    }
}
