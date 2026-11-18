using System;
using UnityEngine;

/// <summary>
/// Навык алхимика: грейд, подранг и мастерство. Повышает подранг по порогу,
/// затем грейд; выше «Открывающего Небеса» не растёт. Числа — в <see cref="AlchemyConfig"/>.
/// </summary>
[DisallowMultipleComponent]
public class AlchemySkill : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private AlchemyConfig config;

    [Header("Состояние")]
    [SerializeField] private AlchemyGradeName gradeName = AlchemyGradeName.Common;
    [SerializeField] private AlchemySubRank subRank = AlchemySubRank.Low;
    [SerializeField, Min(0)] private int mastery;

    /// <summary>Любое изменение грейда, подранга или мастерства.</summary>
    public event Action OnChanged;

    /// <summary>Текущий грейд алхимика.</summary>
    public AlchemyGrade Grade => new AlchemyGrade(gradeName, subRank);

    public int Mastery => Mathf.Max(0, mastery);

    /// <summary>Сколько мастерства нужно до следующей ступени.</summary>
    public int MasteryForNextRank => config != null ? config.MasteryPerSubRank : 100;

    public AlchemyConfig Config => config;

    /// <summary>Передаёт конфиг из bootstrap/сетапа.</summary>
    public void Initialize(AlchemyConfig alchemyConfig)
    {
        config = alchemyConfig;
    }

    /// <summary>
    /// Добавляет мастерство и повышает ступени, пока хватает порога.
    /// На максимуме мастерство не растёт.
    /// </summary>
    public void AddMastery(int amount)
    {
        if (amount <= 0 || Grade.IsMax) return;

        mastery = Mathf.Max(0, mastery + amount);

        bool changed = false;
        while (!Grade.IsMax && mastery >= MasteryForNextRank)
        {
            mastery -= MasteryForNextRank;
            AlchemyGrade next = Grade.Next();
            gradeName = next.Name;
            subRank = next.SubRank;
            changed = true;
        }

        // На максимуме остаток не копится: иначе порог выглядел бы недостижимым.
        if (Grade.IsMax) mastery = 0;

        if (changed) OnChanged?.Invoke();
    }

    /// <summary>Восстанавливает состояние из сохранения.</summary>
    public void Restore(AlchemyGrade grade, int savedMastery)
    {
        gradeName = grade.Name;
        subRank = grade.SubRank;
        mastery = Mathf.Max(0, savedMastery);

        if (grade.IsMax) mastery = 0;
        OnChanged?.Invoke();
    }

    /// <summary>Сброс к обычному низкому грейду (новая игра).</summary>
    public void ResetSkill()
    {
        gradeName = AlchemyGradeName.Common;
        subRank = AlchemySubRank.Low;
        mastery = 0;
        OnChanged?.Invoke();
    }
}
