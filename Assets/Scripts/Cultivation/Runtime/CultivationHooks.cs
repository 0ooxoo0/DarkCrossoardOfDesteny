using UnityEngine;

/// <summary>
/// Внешние хуки культивации для Промта B: боссы, редкие события, награды Прозрения и урон.
/// Никакие чужие скрипты не модифицируются — только публичные точки входа.
/// </summary>
public static class CultivationHooks
{
    /// <summary>Награда Прозрения (боссы, редкие события).</summary>
    public static event System.Action<int> ComprehensionGained;
    public static event System.Action<string> BossDefeated;

    public static void NotifyBossDefeated(string bossId, int comprehensionReward = 3, int experienceQiReward = 150)
    {
        var cultivation = FindCultivation();
        if (cultivation != null)
        {
            if (comprehensionReward > 0) cultivation.AddComprehension(comprehensionReward);
            if (experienceQiReward > 0) cultivation.AddExperienceQi(experienceQiReward);
        }

        BossDefeated?.Invoke(bossId);
        if (comprehensionReward > 0) ComprehensionGained?.Invoke(comprehensionReward);
    }

    public static void GrantComprehension(int amount)
    {
        if (amount <= 0) return;

        var cultivation = FindCultivation();
        if (cultivation != null) cultivation.AddComprehension(amount);
        ComprehensionGained?.Invoke(amount);
    }

    public static void GrantExperienceQi(int amount)
    {
        if (amount <= 0) return;
        FindCultivation()?.AddExperienceQi(amount);
    }

    /// <summary>Вызвать из системы урона: прерывает медитацию игрока.</summary>
    public static void NotifyPlayerDamaged(GameObject player)
    {
        if (player == null) return;
        MeditationInterruptSource.NotifyPlayerDamaged(player);
    }

    public static CultivationSystem FindCultivation()
    {
        if (CultivationBootstrap.Instance != null && CultivationBootstrap.Instance.Cultivation != null)
            return CultivationBootstrap.Instance.Cultivation;

        return Object.FindFirstObjectByType<CultivationSystem>();
    }
}
