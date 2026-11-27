using UnityEngine;

/// <summary>
/// One difficulty step. Multipliers are applied by <see cref="EnemyController"/> to its base stats.
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

    [Header("AI")]
    [Range(1, 5)] public int intelligence = 2;
}
