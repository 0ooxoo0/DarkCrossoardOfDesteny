using UnityEngine;

public enum CombatTeam
{
    Neutral,
    Player,
    Enemy
}

/// <summary>
/// Everything that can receive combat damage. Implemented by PlayerHealth and EnemyHealth.
/// </summary>
public interface IDamageable
{
    bool IsDead { get; }
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
