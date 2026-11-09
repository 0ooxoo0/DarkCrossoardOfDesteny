using UnityEngine;

/// <summary>
/// Приёмник AnimationEvent из клипов атаки. Вторая проверка флага
/// useAnimationEvents: если оружие управляет хитбоксом по normalizedTime,
/// события не должны включать хитбокс.
/// </summary>
public class AnimationEventRelay : MonoBehaviour
{
    [SerializeField] private WeaponManager weaponManager;

    public void EnableHitbox()
    {
        if (weaponManager != null && weaponManager.UseAnimationEvents)
            weaponManager.EnableHitbox();
    }

    public void DisableHitbox()
    {
        if (weaponManager != null && weaponManager.UseAnimationEvents)
            weaponManager.DisableHitbox();
    }
}
