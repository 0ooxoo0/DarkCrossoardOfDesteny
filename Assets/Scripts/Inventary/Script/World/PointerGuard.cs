using UnityEngine;

/// <summary>
/// Общий флаг «сейчас клик принадлежит интерфейсу и луту, а не атаке».
/// Статический, потому что наведение и атака живут в разных компонентах
/// и не должны тащить ссылки друг на друга.
/// </summary>
public static class PointerGuard
{
    /// <summary>Истина, если наведение на лут или его панель должно отменить атаку.</summary>
    public static bool IsBlockingAttack { get; set; }

    /// <summary>
    /// Статический флаг переживает загрузку сцены: без сброса после перезапуска
    /// игрок остался бы без атаки. Вызывается автоматически при старте плеера.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        IsBlockingAttack = false;
    }
}
