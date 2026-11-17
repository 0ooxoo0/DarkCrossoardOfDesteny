using UnityEngine;

/// <summary>
/// Снимок одной медитации: сколько успел накопить игрок до прерывания.
/// Обычный класс, чтобы не тащить в сцену лишний компонент.
/// </summary>
public class MeditationSession
{
    public float StartedAt { get; private set; }
    public bool IsActive { get; private set; }
    public bool WasSafe { get; set; }
    public int AccumulatedExperience { get; private set; }
    public int AccumulatedDaoPoints { get; private set; }
    public int AccumulatedComprehension { get; private set; }

    /// <summary>Начинает новую сессию и обнуляет накопления.</summary>
    public void Start(bool safe)
    {
        StartedAt = Time.time;
        IsActive = true;
        WasSafe = safe;
        AccumulatedExperience = 0;
        AccumulatedDaoPoints = 0;
        AccumulatedComprehension = 0;
    }

    /// <summary>Сбрасывает сессию без начисления последствий.</summary>
    public void Reset()
    {
        StartedAt = 0f;
        IsActive = false;
        WasSafe = false;
        AccumulatedExperience = 0;
        AccumulatedDaoPoints = 0;
        AccumulatedComprehension = 0;
    }

    public void AddExperience(int amount)
    {
        if (amount > 0) AccumulatedExperience += amount;
    }

    public void AddDaoPoints(int amount)
    {
        if (amount > 0) AccumulatedDaoPoints += amount;
    }

    public void AddComprehension(int amount)
    {
        if (amount > 0) AccumulatedComprehension += amount;
    }
}
