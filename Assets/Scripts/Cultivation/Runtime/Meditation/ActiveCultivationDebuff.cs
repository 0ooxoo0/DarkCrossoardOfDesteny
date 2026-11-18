using UnityEngine;

/// <summary>
/// Активный дебаф культивации: стадия, её номер и остаток времени.
/// </summary>
public class ActiveCultivationDebuff
{
    public CultivationFailureStage Stage { get; }
    public int Severity { get; }
    public float Remaining { get; private set; }

    public bool IsExpired => Remaining <= 0f;
    public CultivationVfxKind Vfx => Stage != null ? Stage.Vfx : CultivationVfxKind.None;

    public ActiveCultivationDebuff(CultivationFailureStage stage, int severity)
        : this(stage, severity, stage != null ? stage.DurationSeconds : 0f)
    {
    }

    /// <summary>Восстановление из сохранения: остаток времени задаётся явно.</summary>
    public ActiveCultivationDebuff(CultivationFailureStage stage, int severity, float remaining)
    {
        Stage = stage;
        Severity = severity;
        Remaining = Mathf.Max(0f, remaining);
    }

    public void Tick(float deltaTime)
    {
        if (Remaining <= 0f) return;
        Remaining = Mathf.Max(0f, Remaining - Mathf.Max(0f, deltaTime));
    }
}
