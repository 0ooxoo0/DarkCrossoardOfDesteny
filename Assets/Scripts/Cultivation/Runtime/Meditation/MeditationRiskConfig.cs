using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Настройки риска медитации: радиус угрозы, окна затухания и три стадии
/// последствий. Один ассет на всю игру, значения меняются без правки кода.
/// </summary>
[CreateAssetMenu(fileName = "MeditationRiskConfig", menuName = "Cultivation/Meditation Risk")]
public class MeditationRiskConfig : ScriptableObject
{
    [Header("Угроза")]
    [SerializeField, Min(0f)] private float threatRadius = 8f;
    [SerializeField, Min(0.05f)] private float threatScanInterval = 0.25f;

    [Header("Затухание стадий")]
    [Tooltip("Через сколько секунд без прерываний стадия снижается на одну.")]
    [SerializeField, Min(0f)] private float escalationDecaySeconds = 180f;
    [Tooltip("Через сколько секунд без прерываний счётчик стадий сбрасывается полностью.")]
    [SerializeField, Min(0f)] private float escalationResetSeconds = 600f;

    [Header("Стадии (по возрастанию)")]
    [SerializeField] private List<CultivationFailureStage> stages = CreateDefaultStages();

    [Header("Ручное прерывание")]
    [Tooltip("Индекс стадии для ручного выхода без безопасной зоны (0 — первая).")]
    [SerializeField, Min(0)] private int manualPenaltyStageIndex;
    [SerializeField] private bool manualPenaltyOnlyUnsafe = true;

    public float ThreatRadius => Mathf.Max(0f, threatRadius);
    public float ThreatScanInterval => Mathf.Max(0.05f, threatScanInterval);
    public float EscalationDecaySeconds => Mathf.Max(0f, escalationDecaySeconds);
    public float EscalationResetSeconds => Mathf.Max(0f, escalationResetSeconds);
    public IReadOnlyList<CultivationFailureStage> Stages => stages;
    public int ManualPenaltyStageIndex => Mathf.Max(0, manualPenaltyStageIndex);
    public bool ManualPenaltyOnlyUnsafe => manualPenaltyOnlyUnsafe;

    /// <summary>Стадия по номеру 1..N; значения вне диапазона клампятся.</summary>
    public CultivationFailureStage GetStage(int severity)
    {
        if (stages == null || stages.Count == 0) return null;

        int index = Mathf.Clamp(severity - 1, 0, stages.Count - 1);
        return stages[index];
    }

    private static List<CultivationFailureStage> CreateDefaultStages()
    {
        return new List<CultivationFailureStage>
        {
            new CultivationFailureStage("Срыв Ци", 30f, 0.75f, 0.9f, 1f, 0.9f,
                0.15f, 0f, 5f, CultivationVfxKind.QiSurge),
            new CultivationFailureStage("Отклонение Ци", 90f, 0.4f, 0.75f, 1f, 0.8f,
                0.35f, 0.1f, 20f, CultivationVfxKind.QiDeviation),
            new CultivationFailureStage("Обрыв культивации", 240f, 0f, 0.6f, 1f, 0.65f,
                0.6f, 0.25f, 60f, CultivationVfxKind.Severing)
        };
    }
}
