using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Активные пассивы школ: собирает выученные пассивные техники с выполненными
/// требованиями и раздаёт множители. Пересчёт идёт по событиям, не в Update.
/// </summary>
[DisallowMultipleComponent]
public class PassiveSkillService : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private DaoSystem dao;
    [SerializeField] private CultivationSystem cultivation;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private QiComponent qi;
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private AlchemyService alchemy;

    private readonly List<PassiveModifier> active = new List<PassiveModifier>();
    private readonly Dictionary<PassiveEffectKind, float> multipliers =
        new Dictionary<PassiveEffectKind, float>();

    /// <summary>Множители изменились: компоненты и UI могут обновиться.</summary>
    public event Action OnModifiersChanged;

    public IReadOnlyList<PassiveModifier> ActivePassives => active;

    /// <summary>Произведение «1 + величина» для вида эффекта. Нет пассивов — 1.</summary>
    public float GetMultiplier(PassiveEffectKind kind)
    {
        return multipliers.TryGetValue(kind, out float value) ? value : 1f;
    }

    /// <summary>Передаёт ссылки из bootstrap/сетапа.</summary>
    public void Initialize(DaoSystem daoSystem, CultivationSystem cultivationSystem, PlayerHealth playerHealth,
        QiComponent qiComponent, AbilitySystem abilitySystemComponent, AlchemyService alchemyService)
    {
        Unsubscribe();

        dao = daoSystem;
        cultivation = cultivationSystem;
        health = playerHealth;
        qi = qiComponent;
        abilitySystem = abilitySystemComponent;
        weaponManager = abilitySystemComponent != null
            ? abilitySystemComponent.GetComponent<WeaponManager>()
            : null;
        alchemy = alchemyService;

        Subscribe();
        Recalculate();
    }

    private void OnEnable()
    {
        Subscribe();
        Recalculate();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (dao != null)
        {
            dao.OnTechniqueLearned -= HandleTechniqueLearned;
            dao.OnDaoLevelChanged -= HandleDaoLevelChanged;
            dao.OnDaoUnlocked -= HandleDaoUnlocked;
            dao.OnTechniqueLearned += HandleTechniqueLearned;
            dao.OnDaoLevelChanged += HandleDaoLevelChanged;
            dao.OnDaoUnlocked += HandleDaoUnlocked;
        }

        if (cultivation != null)
        {
            cultivation.OnRealmChanged -= HandleRealmChanged;
            cultivation.OnStageChanged -= HandleStageChanged;
            cultivation.OnRealmChanged += HandleRealmChanged;
            cultivation.OnStageChanged += HandleStageChanged;
        }
    }

    private void Unsubscribe()
    {
        if (dao != null)
        {
            dao.OnTechniqueLearned -= HandleTechniqueLearned;
            dao.OnDaoLevelChanged -= HandleDaoLevelChanged;
            dao.OnDaoUnlocked -= HandleDaoUnlocked;
        }

        if (cultivation != null)
        {
            cultivation.OnRealmChanged -= HandleRealmChanged;
            cultivation.OnStageChanged -= HandleStageChanged;
        }
    }

    private void HandleTechniqueLearned(TechniqueData technique) => Recalculate();
    private void HandleDaoLevelChanged(DaoType type, int level) => Recalculate();
    private void HandleDaoUnlocked(DaoType type) => Recalculate();
    private void HandleRealmChanged(CultivationRealm realm) => Recalculate();
    private void HandleStageChanged(CultivationRealm realm, int stage) => Recalculate();

    /// <summary>Собирает активные пассивы и раздаёт множители системам.</summary>
    public void Recalculate()
    {
        active.Clear();
        multipliers.Clear();

        if (dao != null && cultivation != null)
        {
            // Пассив активен по прогрессу: требования выполнены, Дао открыт, эффект задан.
            // «Выучен» (свиток) — только обход требований, а не обязательное условие.
            List<TechniqueData> all = dao.GetAllTechniques();
            for (int i = 0; i < all.Count; i++)
            {
                TryAdd(all[i], false);
            }

            // Выученные пассивы обходят требования уровня/реалма.
            List<TechniqueData> learned = dao.GetLearnedTechniques();
            for (int i = 0; i < learned.Count; i++)
            {
                TryAdd(learned[i], true);
            }
        }

        ApplyToSystems();
        OnModifiersChanged?.Invoke();
    }

    /// <summary>Активен ли пассив сейчас: открытый Дао, эффект и выполненные требования.</summary>
    public bool IsPassiveActive(TechniqueData technique)
    {
        if (technique == null || technique.Kind != TechniqueKind.Passive) return false;
        if (technique.PassiveValue <= 0f) return false;
        if (dao == null || !dao.IsDaoUnlocked(technique.DaoType)) return false;

        if (dao.HasLearnedTechnique(technique)) return true;
        return MeetsRequirements(technique);
    }

    /// <summary>Причина неактивности пассива для UI. Пусто — пассив активен.</summary>
    public string GetInactiveReason(TechniqueData technique)
    {
        if (technique == null || technique.Kind != TechniqueKind.Passive) return string.Empty;
        if (technique.PassiveValue <= 0f) return "Нет эффекта";
        if (dao == null) return "Нет DaoSystem";
        if (!dao.IsDaoUnlocked(technique.DaoType))
            return "Нужен Дао " + DaoTypeInfo.GetDisplayName(technique.DaoType);
        if (dao.HasLearnedTechnique(technique) || MeetsRequirements(technique)) return string.Empty;

        int daoLevel = dao.GetLevel(technique.DaoType);
        if (daoLevel < technique.RequiredDaoLevel)
            return "Нужен Дао " + technique.RequiredDaoLevel + " ур. (сейчас " + daoLevel + ")";

        int realmIndex = cultivation != null ? cultivation.CurrentRealmIndex : 0;
        if (realmIndex < technique.RequiredRealmIndex)
            return "Нужен реалм " + (technique.RequiredRealmIndex + 1);

        return "Недоступно";
    }

    private bool MeetsRequirements(TechniqueData technique)
    {
        int realmIndex = cultivation != null ? cultivation.CurrentRealmIndex : 0;
        if (realmIndex < technique.RequiredRealmIndex) return false;

        int daoLevel = dao != null ? dao.GetLevel(technique.DaoType) : 0;
        return daoLevel >= technique.RequiredDaoLevel;
    }

    private void TryAdd(TechniqueData technique, bool learnedOverride)
    {
        if (technique == null || technique.Kind != TechniqueKind.Passive) return;
        if (technique.PassiveValue <= 0f) return;
        if (dao == null || !dao.IsDaoUnlocked(technique.DaoType)) return;
        if (!learnedOverride && !MeetsRequirements(technique)) return;

        for (int i = 0; i < active.Count; i++)
        {
            if (active[i].Source == technique) return;
        }

        active.Add(new PassiveModifier(technique.PassiveEffect, technique.PassiveValue, technique));

        float current = GetMultiplier(technique.PassiveEffect);
        multipliers[technique.PassiveEffect] = current * (1f + technique.PassiveValue);
    }

    private void ApplyToSystems()
    {
        if (health != null)
        {
            health.PassiveMaxHealthMultiplier = GetMultiplier(PassiveEffectKind.MaxHealthPercent);

            // «−10% урона» хранится как +0.1 к множителю: 1.1 → 0.9 входящего урона.
            health.PassiveIncomingDamageMultiplier =
                Mathf.Clamp(2f - GetMultiplier(PassiveEffectKind.DamageReductionPercent), 0.1f, 1f);
        }

        if (qi != null)
        {
            qi.MaxQiMultiplier = GetMultiplier(PassiveEffectKind.MaxQiPercent);
            qi.RegenMultiplier = GetMultiplier(PassiveEffectKind.QiRegenPercent);
        }

        if (cultivation != null)
            cultivation.ComprehensionGainMultiplier = GetMultiplier(PassiveEffectKind.ComprehensionGainPercent);

        if (abilitySystem != null)
            abilitySystem.DamageMultiplier = GetMultiplier(PassiveEffectKind.SpellPowerPercent);

        if (weaponManager != null)
            weaponManager.PlayerDamageMultiplier = GetMultiplier(PassiveEffectKind.MeleeDamagePercent);

        if (alchemy != null)
            alchemy.SetPassiveSuccessBonus(GetMultiplier(PassiveEffectKind.AlchemySuccessPercent) - 1f);
    }
}
