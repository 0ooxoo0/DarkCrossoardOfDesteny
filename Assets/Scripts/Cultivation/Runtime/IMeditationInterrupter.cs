using System;
using UnityEngine;

/// <summary>
/// Хук прерывания медитации. Реализуется на игроке (или на его источнике урона).
/// PlayerController.cs не тронут — Промт B вызывает NotifyInterrupted() из своего урона.
/// </summary>
public interface IMeditationInterrupter
{
    event Action OnInterrupted;
}

/// <summary>
/// Готовый источник прерывания: bootstrap вешает его на игрока,
/// Промт B вызывает NotifyInterrupted() при получении урона.
/// </summary>
[DisallowMultipleComponent]
public class MeditationInterruptSource : MonoBehaviour, IMeditationInterrupter
{
    public event Action OnInterrupted;

    public void NotifyInterrupted()
    {
        OnInterrupted?.Invoke();
    }

    /// <summary>Удобная точка входа для внешних систем урона: MeditationInterruptSource.NotifyPlayerDamaged(player).</summary>
    public static void NotifyPlayerDamaged(GameObject player)
    {
        if (player == null) return;

        var sources = player.GetComponentsInChildren<MeditationInterruptSource>(true);
        for (int i = 0; i < sources.Length; i++)
            sources[i].NotifyInterrupted();
    }
}
