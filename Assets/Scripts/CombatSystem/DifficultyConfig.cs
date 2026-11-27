using UnityEngine;

/// <summary>
/// Одна ступень сложности врага. Множители применяет <see cref="EnemyController"/>,
/// боевые шансы читает <see cref="EnemyCombatAI"/> через <c>ApplyDifficulty</c>.
/// Уровни 1..4 задаются таблицей <see cref="GetPreset"/>, а этот ассет позволяет
/// переопределить любую ступень под конкретного врага.
/// </summary>
[CreateAssetMenu(fileName = "Difficulty_", menuName = "Enemies/Difficulty Config", order = 1)]
public class DifficultyConfig : ScriptableObject
{
    [Header("Identity")]
    public string difficultyName = "Normal";

    [Header("Multipliers")]
    [Min(0.1f)] public float healthMultiplier = 1f;
    [Min(0f)] public float damageMultiplier = 1f;
    [Min(0.1f)] public float speedMultiplier = 1f;
    [Min(0.1f)] public float attackCooldownMultiplier = 1f;

    [Header("Combat behaviour")]
    [Tooltip("Шанс поднять блок, когда игрок начал атаку рядом. 0 — не блокирует никогда.")]
    [Range(0f, 1f)] public float blockChance = 0.4f;
    [Tooltip("Шанс продолжить серию вместо одиночного удара.")]
    [Range(0f, 1f)] public float comboChance = 0.15f;
    [Tooltip("Шанс контратаковать сразу после успешного блока. Кулдаун — attackCooldown.")]
    [Range(0f, 1f)] public float parryChance = 0f;
    [Tooltip("Пауза между решениями ИИ, с. Меньше — резче реакции.")]
    [Min(0f)] public float reactionDelay = 0.5f;
    [Tooltip("Сколько ударов подряд можно провести в серии.")]
    [Range(1, 3)] public int maxCombo = 2;
    [Tooltip("Сколько секунд враг держит блок.")]
    [Min(0.1f)] public float blockDuration = 0.9f;

    [Header("AI")]
    [Tooltip("Уровень сложности 1..4. 1 Слабый — бьёт редко, не защищается. " +
             "2 Обычный — блокирует, серии редки. 3 Опытный — частые удары и серии по 3. " +
             "4 Мастер — блокирует и парирует с контратакой.")]
    [Range(1, 4)] public int intelligence = 2;

    /// <summary>Боевые числа одной ступени. Значения по умолчанию — уровень 2 (Обычный).</summary>
    public struct Preset
    {
        public float damageMultiplier;
        public float speedMultiplier;
        public float cooldownMultiplier;
        public float blockChance;
        public float comboChance;
        public float parryChance;
        public float reactionDelay;
        public float blockDuration;
        public int maxCombo;
    }

    // Единственный источник чисел лестницы 1..4. EnemyCombatAI.SetIntelligence читает отсюда,
    // поэтому смена уровня в рантайме не требует перезапуска сцены и правки префаба.
    private static readonly Preset Level1 = new Preset
    {
        damageMultiplier = 0.7f, speedMultiplier = 0.8f, cooldownMultiplier = 1.4f,
        blockChance = 0f, comboChance = 0f, parryChance = 0f,
        reactionDelay = 0.8f, blockDuration = 0.6f, maxCombo = 2
    };

    private static readonly Preset Level2 = new Preset
    {
        damageMultiplier = 1f, speedMultiplier = 1f, cooldownMultiplier = 1f,
        blockChance = 0.4f, comboChance = 0.15f, parryChance = 0f,
        reactionDelay = 0.5f, blockDuration = 0.9f, maxCombo = 2
    };

    private static readonly Preset Level3 = new Preset
    {
        damageMultiplier = 1.2f, speedMultiplier = 1.15f, cooldownMultiplier = 0.7f,
        blockChance = 0.6f, comboChance = 0.55f, parryChance = 0f,
        reactionDelay = 0.25f, blockDuration = 1.1f, maxCombo = 3
    };

    private static readonly Preset Level4 = new Preset
    {
        damageMultiplier = 1.45f, speedMultiplier = 1.3f, cooldownMultiplier = 0.55f,
        blockChance = 0.8f, comboChance = 0.8f, parryChance = 0.5f,
        reactionDelay = 0.12f, blockDuration = 1.3f, maxCombo = 3
    };

    /// <summary>Пресет по уровню 1..4. Значения вне диапазона зажимаются.</summary>
    public static Preset GetPreset(int level)
    {
        switch (Mathf.Clamp(level, 1, 4))
        {
            case 1: return Level1;
            case 3: return Level3;
            case 4: return Level4;
            default: return Level2;
        }
    }

    /// <summary>Числа этого ассета в виде пресета: ассет всегда важнее таблицы уровней.</summary>
    public Preset ToPreset()
    {
        return new Preset
        {
            damageMultiplier = damageMultiplier,
            speedMultiplier = speedMultiplier,
            cooldownMultiplier = attackCooldownMultiplier,
            blockChance = blockChance,
            comboChance = comboChance,
            parryChance = parryChance,
            reactionDelay = reactionDelay,
            blockDuration = blockDuration,
            maxCombo = maxCombo
        };
    }
}
