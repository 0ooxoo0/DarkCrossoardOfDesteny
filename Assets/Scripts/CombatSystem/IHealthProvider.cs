using System;

/// <summary>
/// Здоровье как контракт: значения, состояние смерти и события жизненного цикла.
/// Контроллеры зависят от этого интерфейса, а не от конкретного <see cref="PlayerHealth"/>,
/// поэтому здоровье можно подменить в тестах и расширять без правки потребителей.
/// </summary>
public interface IHealthProvider
{
    /// <summary>Текущее здоровье, округлённое для UI.</summary>
    int Current { get; }

    /// <summary>Максимум здоровья с учётом всех множителей.</summary>
    int Max { get; }

    /// <summary>Доля здоровья от 0 до 1.</summary>
    float Normalized { get; }

    /// <summary>Мёртв ли владелец. Сбрасывается только респавном.</summary>
    bool IsDead { get; }

    /// <summary>Владелец погиб. Срабатывает ровно один раз на смерть.</summary>
    event Action OnDied;

    /// <summary>Владелец вернулся в строй после смерти.</summary>
    event Action OnRespawned;
}
