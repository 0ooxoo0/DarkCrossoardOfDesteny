using System;
using UnityEngine;

/// <summary>
/// Описание одной стадии срыва культивации: как долго длится, что портит
/// и сколько опыта забирает. Числа живут в ассете, а не в коде.
/// </summary>
[Serializable]
public class CultivationFailureStage
{
    [SerializeField] private string displayName = "Срыв Ци";
    [SerializeField, Min(0f)] private float durationSeconds = 30f;
    [SerializeField, Range(0f, 1f)] private float qiRegenMultiplier = 0.75f;
    [SerializeField, Range(0f, 1f)] private float damageDealtMultiplier = 0.9f;
    [SerializeField, Range(0.5f, 2f)] private float damageTakenMultiplier = 1f;
    [SerializeField, Range(0f, 1f)] private float maxHealthMultiplier = 0.9f;
    [SerializeField, Range(0f, 1f)] private float sessionExperienceLossPercent = 0.15f;
    [SerializeField, Range(0f, 1f)] private float stageExperienceLossPercent;
    [SerializeField, Min(0f)] private float meditationBlockSeconds = 5f;
    [SerializeField] private CultivationVfxKind vfx = CultivationVfxKind.QiSurge;

    public string DisplayName => displayName;
    public float DurationSeconds => Mathf.Max(0f, durationSeconds);
    public float QiRegenMultiplier => Mathf.Clamp01(qiRegenMultiplier);
    public float DamageDealtMultiplier => Mathf.Clamp(damageDealtMultiplier, 0f, 1f);
    public float DamageTakenMultiplier => Mathf.Clamp(damageTakenMultiplier, 0.5f, 2f);
    public float MaxHealthMultiplier => Mathf.Clamp(maxHealthMultiplier, 0.1f, 1f);
    public float SessionExperienceLossPercent => Mathf.Clamp01(sessionExperienceLossPercent);
    public float StageExperienceLossPercent => Mathf.Clamp01(stageExperienceLossPercent);
    public float MeditationBlockSeconds => Mathf.Max(0f, meditationBlockSeconds);
    public CultivationVfxKind Vfx => vfx;

    public CultivationFailureStage()
    {
    }

    public CultivationFailureStage(string name, float duration, float qiRegen, float damageDealt,
        float damageTaken, float maxHealth, float sessionLoss, float stageLoss,
        float meditationBlock, CultivationVfxKind effect)
    {
        displayName = name;
        durationSeconds = duration;
        qiRegenMultiplier = qiRegen;
        damageDealtMultiplier = damageDealt;
        damageTakenMultiplier = damageTaken;
        maxHealthMultiplier = maxHealth;
        sessionExperienceLossPercent = sessionLoss;
        stageExperienceLossPercent = stageLoss;
        meditationBlockSeconds = meditationBlock;
        vfx = effect;
    }
}
