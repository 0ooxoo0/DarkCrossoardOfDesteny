using UnityEngine;

/// <summary>
/// Считает последствия прерванной медитации: выбирает стадию, отнимает опыт
/// и накладывает дебаф. Отдельный компонент, чтобы <see cref="MeditationController"/>
/// не разрастался правилами наказаний.
/// </summary>
[DisallowMultipleComponent]
public class MeditationConsequenceService : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private CultivationDebuffController debuffs;
    [SerializeField] private CultivationSystem cultivation;
    [SerializeField] private MeditationRiskConfig config;

    /// <summary>Передаёт ссылки из bootstrap.</summary>
    public void Initialize(MeditationRiskConfig riskConfig, CultivationDebuffController debuffController,
        CultivationSystem cultivationSystem)
    {
        config = riskConfig;
        debuffs = debuffController;
        cultivation = cultivationSystem;
    }

    /// <summary>
    /// Применяет последствия прерывания. Смерть и ручной выход в безопасной зоне
    /// ничего не стоят; остальные случаи эскалируют стадию.
    /// </summary>
    public void Apply(MeditationInterruptionReason reason, MeditationSession session, bool wasSafe)
    {
        if (reason == MeditationInterruptionReason.Death) return;
        if (config == null) return;

        int severity = ResolveSeverity(reason, wasSafe);
        if (severity <= 0) return;

        CultivationFailureStage stage = config.GetStage(severity);
        if (stage == null) return;

        int lost = LoseExperience(session, stage);

        if (debuffs != null)
            debuffs.Apply(stage, severity);

        Debug.Log($"[Meditation] {reason}: {stage.DisplayName} (стадия {severity}), потеряно {lost} опыта.");
    }

    private int ResolveSeverity(MeditationInterruptionReason reason, bool wasSafe)
    {
        int max = config.Stages != null && config.Stages.Count > 0 ? config.Stages.Count : 1;

        if (reason == MeditationInterruptionReason.Manual)
        {
            if (wasSafe && config.ManualPenaltyOnlyUnsafe) return 0;
            return Mathf.Clamp(config.ManualPenaltyStageIndex + 1, 1, max);
        }

        int current = debuffs != null ? debuffs.Severity : 0;
        return Mathf.Clamp(current + 1, 1, max);
    }

    private int LoseExperience(MeditationSession session, CultivationFailureStage stage)
    {
        if (cultivation == null || session == null) return 0;

        int current = cultivation.ExperienceQi;
        if (current <= 0) return 0;

        int fromSession = Mathf.CeilToInt(session.AccumulatedExperience * stage.SessionExperienceLossPercent);
        int fromStage = Mathf.CeilToInt(current * stage.StageExperienceLossPercent);
        int total = Mathf.Clamp(fromSession + fromStage, 0, current);
        if (total <= 0) return 0;

        // LoseExperiencePercent работает долей, поэтому переводим абсолютную потерю в долю.
        return cultivation.LoseExperiencePercent((float)total / current);
    }
}
