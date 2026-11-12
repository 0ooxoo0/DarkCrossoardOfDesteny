using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Результат попытки прорыва.</summary>
public enum BreakthroughResult
{
    Success,
    Failed,
    NotReady
}

/// <summary>
/// Культивация: реалмы, ступени, опыт Ци, Прозрение и бонусы.
/// Не трогает HP игрока — бонусы отдаёт событиями, применяет владелец HP (Промт B).
/// </summary>
[DisallowMultipleComponent]
public class CultivationSystem : MonoBehaviour
{
    [Header("Реалмы (строго по возрастанию)")]
    [SerializeField] private List<CultivationRealm> realms = new List<CultivationRealm>();
    [SerializeField] private int startRealmIndex = 0;
    [SerializeField, Min(1)] private int startStage = 1;

    [Header("Опыт Ци")]
    [SerializeField, Min(1)] private int baseExperienceQiToNextStage = 100;
    [SerializeField, Min(1f)] private float experienceQiGrowthPerStage = 1.45f;

    [Header("Прозрение")]
    [SerializeField, Min(0)] private int comprehension;

    [Header("Прорыв")]
    [SerializeField, Range(0f, 1f)] private float baseBreakthroughChance = 0.35f;
    [SerializeField, Min(0f)] private float chancePerComprehension = 0.01f;
    [SerializeField, Range(0f, 1f)] private float maxBreakthroughChance = 0.95f;
    [SerializeField, Range(0f, 1f)] private float experienceLossOnFail = 0f;
    [Tooltip("Бонус к скорости изучения техник за единицу Прозрения (пассивный, отдаётся событием).")]
    [SerializeField, Min(0f)] private float techniqueLearningBonusPerComprehension = 0.01f;

    private int realmIndex;
    private int stage = 1;
    private int experienceQi;
    private bool initialized;

    /// <summary>
    /// Провайдер материалов (строковые теги инвентаря). Культивация не лезет в инвентарь —
    /// Промт B подписывает сюда свою проверку.
    /// </summary>
    public Func<bool> HasBreakthroughMaterials;

    public event Action<CultivationRealm> OnRealmChanged;
    public event Action<CultivationRealm, int> OnStageChanged;
    public event Action<int, int> OnExperienceQiChanged;
    public event Action<BreakthroughResult> OnBreakthroughAttempted;
    /// <summary>(реалм, в котором был совершён успешный прорыв) — для списания материалов.</summary>
    public event Action<CultivationRealm> OnBreakthroughSucceeded;
    public event Action<int> OnComprehensionChanged;
    /// <summary>Множитель скорости изучения техник — для AbilitySystem (Промт B).</summary>
    public event Action<float> OnTechniqueLearningChanged;
    /// <summary>(qiBonus, hpBonus, regenBonus) — суммарные бонусы реалма для владельца HP.</summary>
    public event Action<int, int, float> OnRealmBonusesChanged;

    public IReadOnlyList<CultivationRealm> Realms => realms;
    public int CurrentRealmIndex { get { EnsureInitialized(); return realmIndex; } }
    public CultivationRealm CurrentRealm { get { EnsureInitialized(); return GetRealm(realmIndex); } }
    public int CurrentStage { get { EnsureInitialized(); return stage; } }
    public int StagesInCurrentRealm { get { var realm = CurrentRealm; return realm != null ? realm.StagesCount : 1; } }
    public int ExperienceQi { get { EnsureInitialized(); return experienceQi; } }
    public int Comprehension { get { return comprehension; } }
    public int RealmsCount => realms != null ? realms.Count : 0;
    public bool IsAtMaxStage { get { EnsureInitialized(); return realmIndex >= RealmsCount - 1 && stage >= StagesInCurrentRealm; } }

    public float ExperienceQiToNextStage
    {
        get
        {
            EnsureInitialized();
            int globalStage = 0;
            for (int i = 0; i < realmIndex && i < RealmsCount; i++)
            {
                if (realms[i] != null) globalStage += realms[i].StagesCount;
            }
            globalStage += Mathf.Max(0, stage - 1);
            return Mathf.Max(1f, baseExperienceQiToNextStage * Mathf.Pow(experienceQiGrowthPerStage, globalStage));
        }
    }

    public int ExperienceQiToNextStageInt => Mathf.Max(1, Mathf.RoundToInt(ExperienceQiToNextStage));

    public float ExperienceNormalized
    {
        get
        {
            float cap = ExperienceQiToNextStage;
            return cap <= 0f ? 0f : Mathf.Clamp01(experienceQi / cap);
        }
    }

    public float TechniqueLearningSpeedMultiplier => 1f + comprehension * techniqueLearningBonusPerComprehension;

    public int TotalQiBonus => Mathf.RoundToInt(SumBonus(realm => realm.QiBonusPerStage));
    public int TotalHpBonus => Mathf.RoundToInt(SumBonus(realm => realm.HpBonusPerStage));
    public float TotalRegenBonus => SumBonus(realm => realm.RegenBonusPerStage);

    public float BaseBreakthroughChance => baseBreakthroughChance;
    public float ChancePerComprehension => chancePerComprehension;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;
        realmIndex = Mathf.Clamp(startRealmIndex, 0, Mathf.Max(0, RealmsCount - 1));
        stage = Mathf.Clamp(startStage, 1, Mathf.Max(1, GetRealm(realmIndex) != null ? GetRealm(realmIndex).StagesCount : 1));
        experienceQi = 0;
    }

    /// <summary>Перенастройка из bootstrap'а / сохранения.</summary>
    public void Initialize(List<CultivationRealm> newRealms, int newRealmIndex, int newStage,
        int newBaseExperience, float newGrowth, float newBaseChance)
    {
        realms = newRealms ?? new List<CultivationRealm>();
        baseExperienceQiToNextStage = Mathf.Max(1, newBaseExperience);
        experienceQiGrowthPerStage = Mathf.Max(1f, newGrowth);
        baseBreakthroughChance = Mathf.Clamp01(newBaseChance);

        realmIndex = Mathf.Clamp(newRealmIndex, 0, Mathf.Max(0, RealmsCount - 1));
        var realm = GetRealm(realmIndex);
        stage = Mathf.Clamp(newStage, 1, realm != null ? realm.StagesCount : 1);
        experienceQi = 0;
        initialized = true;

        RaiseAllState();
    }

    public CultivationRealm GetRealm(int index)
    {
        if (realms == null || index < 0 || index >= realms.Count) return null;
        return realms[index];
    }

    public void AddExperienceQi(int amount)
    {
        EnsureInitialized();
        if (amount <= 0) return;

        int cap = ExperienceQiToNextStageInt;
        int before = experienceQi;
        experienceQi = Mathf.Clamp(experienceQi + amount, 0, cap);
        if (experienceQi != before)
            OnExperienceQiChanged?.Invoke(experienceQi, cap);
    }

    public void SetExperienceQi(int value)
    {
        EnsureInitialized();
        experienceQi = Mathf.Clamp(value, 0, ExperienceQiToNextStageInt);
        OnExperienceQiChanged?.Invoke(experienceQi, ExperienceQiToNextStageInt);
    }

    public void AddComprehension(int amount)
    {
        if (amount == 0) return;
        comprehension = Mathf.Max(0, comprehension + amount);
        RaiseComprehension();
    }

    public void SetComprehension(int value)
    {
        int clamped = Mathf.Max(0, value);
        if (clamped == comprehension) return;
        comprehension = clamped;
        RaiseComprehension();
    }

    public bool CanAttemptBreakthrough(out string reason)
    {
        EnsureInitialized();
        var realm = CurrentRealm;

        if (realm == null)
        {
            reason = "Реалмы не настроены";
            return false;
        }

        if (IsAtMaxStage)
        {
            reason = "Достигнут предел культивации";
            return false;
        }

        int cap = ExperienceQiToNextStageInt;
        if (experienceQi < cap)
        {
            reason = "Недостаточно опыта Ци: " + experienceQi + " / " + cap;
            return false;
        }

        if (comprehension < realm.RequiredComprehension)
        {
            reason = "Недостаточно Прозрения: нужно " + realm.RequiredComprehension;
            return false;
        }

        if (HasBreakthroughMaterials != null && !HasBreakthroughMaterials())
        {
            reason = "Не хватает материалов для прорыва";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool CanAttemptBreakthrough() => CanAttemptBreakthrough(out _);

    /// <summary>Шанс успеха: база реалма (или системы) + бонус Прозрения, ограничен maxBreakthroughChance.</summary>
    public float ComputeSuccessChance()
    {
        EnsureInitialized();
        var realm = CurrentRealm;
        float chance = realm != null && realm.BaseBreakthroughChance >= 0f
            ? realm.BaseBreakthroughChance
            : baseBreakthroughChance;
        chance += comprehension * chancePerComprehension;
        return Mathf.Clamp(chance, 0f, maxBreakthroughChance);
    }

    /// <summary>Попытка прорыва: проверка готовности + бросок шанса + смена ступени/реалма.</summary>
    public BreakthroughResult TryBreakthrough()
    {
        EnsureInitialized();

        if (!CanAttemptBreakthrough(out _))
        {
            OnBreakthroughAttempted?.Invoke(BreakthroughResult.NotReady);
            return BreakthroughResult.NotReady;
        }

        BreakthroughResult result;
        if (UnityEngine.Random.value <= ComputeSuccessChance())
        {
            CultivationRealm attemptedRealm = CurrentRealm;
            AdvanceStage();
            result = BreakthroughResult.Success;
            OnBreakthroughSucceeded?.Invoke(attemptedRealm);
        }
        else
        {
            if (experienceLossOnFail > 0f)
            {
                int before = experienceQi;
                experienceQi = Mathf.Max(0, Mathf.RoundToInt(experienceQi * (1f - experienceLossOnFail)));
                if (experienceQi != before)
                    OnExperienceQiChanged?.Invoke(experienceQi, ExperienceQiToNextStageInt);
            }
            result = BreakthroughResult.Failed;
        }

        OnBreakthroughAttempted?.Invoke(result);
        return result;
    }

    private void AdvanceStage()
    {
        var oldRealm = CurrentRealm;
        bool realmChanged = false;

        if (stage < StagesInCurrentRealm)
        {
            stage++;
        }
        else if (realmIndex + 1 < RealmsCount)
        {
            realmIndex++;
            stage = 1;
            realmChanged = true;
        }

        experienceQi = 0;

        var newRealm = CurrentRealm;
        if (realmChanged)
            OnRealmChanged?.Invoke(newRealm);

        OnStageChanged?.Invoke(newRealm, stage);
        OnExperienceQiChanged?.Invoke(experienceQi, ExperienceQiToNextStageInt);
        RaiseBonuses();
    }

    /// <summary>Восстановление из сохранения (без броска шанса).</summary>
    public void RestoreState(int newRealmIndex, int newStage, int experience, int newComprehension)
    {
        initialized = true;
        realmIndex = Mathf.Clamp(newRealmIndex, 0, Mathf.Max(0, RealmsCount - 1));
        var realm = GetRealm(realmIndex);
        stage = Mathf.Clamp(newStage, 1, realm != null ? realm.StagesCount : 1);
        experienceQi = Mathf.Clamp(experience, 0, ExperienceQiToNextStageInt);
        comprehension = Mathf.Max(0, newComprehension);
        RaiseAllState();
    }

    public string GetProgressLabel()
    {
        var realm = CurrentRealm;
        if (realm == null) return "—";
        return realm.RealmName + " " + CurrentStage + "/" + StagesInCurrentRealm;
    }

    private void RaiseAllState()
    {
        var realm = CurrentRealm;
        OnRealmChanged?.Invoke(realm);
        OnStageChanged?.Invoke(realm, CurrentStage);
        OnExperienceQiChanged?.Invoke(experienceQi, ExperienceQiToNextStageInt);
        RaiseComprehension();
        RaiseBonuses();
    }

    private void RaiseComprehension()
    {
        OnComprehensionChanged?.Invoke(comprehension);
        OnTechniqueLearningChanged?.Invoke(TechniqueLearningSpeedMultiplier);
    }

    private void RaiseBonuses()
    {
        OnRealmBonusesChanged?.Invoke(TotalQiBonus, TotalHpBonus, TotalRegenBonus);
    }

    private float SumBonus(Func<CultivationRealm, float> selector)
    {
        EnsureInitialized();
        float sum = 0f;
        for (int i = 0; i < realmIndex && i < RealmsCount; i++)
        {
            var realm = realms[i];
            if (realm != null) sum += selector(realm) * realm.StagesCount;
        }
        var current = GetRealm(realmIndex);
        if (current != null) sum += selector(current) * Mathf.Max(0, stage - 1);
        return sum;
    }
}
