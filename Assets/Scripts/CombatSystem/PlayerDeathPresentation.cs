using UnityEngine;

/// <summary>
/// Внешний вид игрока при смерти и респавне: Animator-флаги и сброс боевого хитбокса.
/// Отдельный подписчик на <see cref="IHealthProvider"/>, чтобы реакция на смерть
/// расширялась добавлением компонента, а не правкой контроллеров движения и блока.
/// </summary>
[DisallowMultipleComponent]
public class PlayerDeathPresentation : MonoBehaviour
{
    private const string DeadParam = "IsDead";
    private const string HitTrigger = "Hit";

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private WeaponManager weaponManager;
    [Tooltip("Источник события смерти; ссылка обязательна.")]
    [SerializeField] private PlayerHealth playerHealth;

    private bool initialized;

    /// <summary>
    /// Идемпотентно подписывает презентацию на смерть и респавн. В EditMode Unity
    /// не вызывает <c>OnEnable</c>, поэтому тесты входят тем же путём, что и игра.
    /// </summary>
    internal void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        playerHealth.OnDied += HandleDied;
        playerHealth.OnRespawned += HandleRespawned;
    }

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void OnDisable()
    {
        playerHealth.OnDied -= HandleDied;
        playerHealth.OnRespawned -= HandleRespawned;
    }

    private void HandleDied()
    {
        if (animator != null) animator.SetBool(DeadParam, true);
        if (weaponManager != null) weaponManager.SetHitboxActive(false, true);
    }

    private void HandleRespawned()
    {
        if (animator == null) return;

        animator.SetBool(DeadParam, false);
        animator.ResetTrigger(HitTrigger);
    }
}
