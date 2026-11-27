using UnityEngine;

public enum CombatTeam
{
    Neutral,
    Player,
    Enemy
}

/// <summary>
/// Всё, что может получать боевой урон: игрок, враг, ресурс или животное.
/// </summary>
public interface IDamageable
{
    bool IsDead { get; }

    /// <summary>
    /// Смерть уже запланирована, но доводится в конце кадра. Нужна хитбоксу,
    /// чтобы отличить добивание от обычного попадания: <see cref="IsDead"/>
    /// внутри физического колбэка ещё false.
    /// </summary>
    bool WillDie { get; }

    /// <summary>Команда цели. Хитбокс не бьёт своих.</summary>
    CombatTeam Team { get; }

    Transform DamageRoot { get; }

    void TakeDamage(DamageInfo info);
}

/// <summary>
/// Blocking target. CombatHitbox asks this before applying damage.
/// </summary>
public interface ICombatBlockable
{
    bool IsBlocking { get; }
    bool CanBlockFrom(Vector3 attackerPosition);
    void OnBlocked(DamageInfo info);
}

/// <summary>
/// Receiver for editable attack/combo windows driven by CombatAnimationWindow (StateMachineBehaviour).
/// </summary>
public interface ICombatAnimationReceiver
{
    void OnAttackWindowOpen();
    void OnAttackWindowClose();
    void OnComboWindowOpen();
    void OnComboWindowClose();
}
