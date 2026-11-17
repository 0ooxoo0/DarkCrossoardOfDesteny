using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Активные дебафы культивации и их множители. Тикает сам, чтобы последствия
/// истекали независимо от того, медитирует игрок или нет.
/// </summary>
[DisallowMultipleComponent]
public class CultivationDebuffController : MonoBehaviour
{
    private const int FallbackSeverityCount = 3;

    [Header("Ссылки")]
    [SerializeField] private MeditationRiskConfig config;
    [SerializeField] private CultivationSystem cultivation;
    [SerializeField] private QiComponent qi;

    private readonly List<ActiveCultivationDebuff> active = new List<ActiveCultivationDebuff>();
    private int severity;
    private float meditationBlockRemaining;
    private float timeSinceLastInterruption;

    /// <summary>Список активных дебафов в порядке наложения.</summary>
    public IReadOnlyList<ActiveCultivationDebuff> Active => active;

    /// <summary>Текущая накопленная стадия срыва (0 — чисто).</summary>
    public int Severity => severity;

    /// <summary>Нельзя ли начать медитацию.</summary>
    public bool IsMeditationBlocked => meditationBlockRemaining > 0f || QiRegenMultiplier <= 0f;

    /// <summary>Сколько ещё действует запрет медитации.</summary>
    public float MeditationBlockRemaining => meditationBlockRemaining;

    public float QiRegenMultiplier { get; private set; } = 1f;
    public float DamageDealtMultiplier { get; private set; } = 1f;
    public float IncomingDamageMultiplier { get; private set; } = 1f;
    public float MaxHealthMultiplier { get; private set; } = 1f;

    public event Action<ActiveCultivationDebuff> OnDebuffApplied;
    public event Action<ActiveCultivationDebuff> OnDebuffExpired;
    public event Action<int> OnSeverityChanged;

    private void Awake()
    {
        Recalculate();
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    /// <summary>Передаёт ссылки из bootstrap, чтобы не искать их в сцене.</summary>
    public void Initialize(MeditationRiskConfig riskConfig, CultivationSystem cultivationSystem, QiComponent qiComponent)
    {
        config = riskConfig;
        cultivation = cultivationSystem;
        qi = qiComponent;
    }

    /// <summary>Накладывает стадию или обновляет уже активную.</summary>
    public void Apply(CultivationFailureStage stage, int newSeverity)
    {
        if (stage == null) return;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(active[i].Stage, stage))
                active.RemoveAt(i);
        }

        var debuff = new ActiveCultivationDebuff(stage, Mathf.Max(1, newSeverity));
        active.Add(debuff);

        severity = Mathf.Clamp(newSeverity, 0, MaxSeverity);
        meditationBlockRemaining = Mathf.Max(meditationBlockRemaining, stage.MeditationBlockSeconds);
        timeSinceLastInterruption = 0f;

        Recalculate();
        OnDebuffApplied?.Invoke(debuff);
        OnSeverityChanged?.Invoke(severity);
    }

    /// <summary>Тикает дебафы и окна затухания. Вызывается из Update.</summary>
    public void Tick(float deltaTime)
    {
        float dt = Mathf.Max(0f, deltaTime);
        bool changed = false;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            active[i].Tick(dt);
            if (!active[i].IsExpired) continue;

            ActiveCultivationDebuff expired = active[i];
            active.RemoveAt(i);
            OnDebuffExpired?.Invoke(expired);
            changed = true;
        }

        if (meditationBlockRemaining > 0f)
            meditationBlockRemaining = Mathf.Max(0f, meditationBlockRemaining - dt);

        if (changed)
            Recalculate();

        NotifyCleanPeriod(dt);
    }

    /// <summary>
    /// Учитывает время без прерываний: сначала снижает стадию на одну,
    /// затем сбрасывает счётчик полностью.
    /// </summary>
    public void NotifyCleanPeriod(float deltaTime)
    {
        if (severity <= 0 || config == null) return;

        timeSinceLastInterruption += Mathf.Max(0f, deltaTime);

        float reset = config.EscalationResetSeconds;
        if (reset > 0f && timeSinceLastInterruption >= reset)
        {
            severity = 0;
            timeSinceLastInterruption = 0f;
            OnSeverityChanged?.Invoke(severity);
            return;
        }

        float decay = config.EscalationDecaySeconds;
        if (decay <= 0f || timeSinceLastInterruption < decay) return;

        severity = Mathf.Max(0, severity - 1);
        timeSinceLastInterruption = 0f;
        OnSeverityChanged?.Invoke(severity);
    }

    /// <summary>Снимает все дебафы (смерть, загрузка, отладка).</summary>
    public void ClearAll()
    {
        for (int i = 0; i < active.Count; i++)
            OnDebuffExpired?.Invoke(active[i]);

        active.Clear();
        severity = 0;
        meditationBlockRemaining = 0f;
        timeSinceLastInterruption = 0f;

        Recalculate();
        OnSeverityChanged?.Invoke(0);
    }

    private int MaxSeverity
    {
        get
        {
            if (config != null && config.Stages != null && config.Stages.Count > 0)
                return config.Stages.Count;

            return FallbackSeverityCount;
        }
    }

    private void Recalculate()
    {
        float qiRegen = 1f;
        float damageDealt = 1f;
        float damageTaken = 1f;
        float maxHealth = 1f;

        for (int i = 0; i < active.Count; i++)
        {
            CultivationFailureStage stage = active[i].Stage;
            if (stage == null) continue;

            qiRegen *= stage.QiRegenMultiplier;
            damageDealt *= stage.DamageDealtMultiplier;
            damageTaken *= stage.DamageTakenMultiplier;
            maxHealth *= stage.MaxHealthMultiplier;
        }

        QiRegenMultiplier = Mathf.Clamp(qiRegen, 0f, 1f);
        DamageDealtMultiplier = Mathf.Clamp(damageDealt, 0.1f, 1f);
        IncomingDamageMultiplier = Mathf.Clamp(damageTaken, 0.1f, 2f);
        MaxHealthMultiplier = Mathf.Clamp(maxHealth, 0.1f, 1f);
    }
}
