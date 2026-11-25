using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Блок игрока: удержание Shift или ПКМ. Удачный блок не пропускает урон
/// и проигрывает BlockHit. Смерть снимает блок подпиской на <see cref="IHealthProvider"/>.
/// </summary>
[DisallowMultipleComponent]
public class PlayerBlockController : MonoBehaviour, ICombatBlockable
{
    private const string BlockingParam = "IsBlocking";
    private const string BlockHitTrigger = "BlockHit";

    [Header("Input")]
    [SerializeField] private Key blockKey = Key.LeftShift;
    [SerializeField] private bool alsoRightMouse = true;

    [Header("Block")]
    [Tooltip("Total block cone in degrees. 120 = 60 degrees to each side.")]
    [SerializeField, Range(30f, 360f)] private float blockAngle = 120f;
    [SerializeField, Min(0f)] private float blockHitCooldown = 0.25f;

    [Header("References")]
    [Tooltip("Смерть снимает блок; ссылка обязательна.")]
    [SerializeField] private PlayerHealth playerHealth;
    [Tooltip("Блок останавливает и возобновляет движение; ссылка обязательна.")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Animator animator;

    private bool blocking;
    private float lastBlockHitTime = -10f;
    private bool initialized;

    public bool IsBlocking => blocking;
    public float BlockAngle => blockAngle;

    /// <summary>
    /// Идемпотентно подписывает блок на смерть. В EditMode Unity не вызывает
    /// <c>OnEnable</c>, поэтому тесты входят тем же путём, что и игра.
    /// </summary>
    internal void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        playerHealth.OnDied += HandleDied;
    }

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void OnDisable()
    {
        playerHealth.OnDied -= HandleDied;
        SetBlocking(false);
    }

    private void Update()
    {
        if (playerHealth.IsDead || playerController.IsAttacking)
        {
            SetBlocking(false);
            return;
        }

        SetBlocking(IsBlockInputHeld());
    }

    private bool IsBlockInputHeld()
    {
        if (Keyboard.current != null && Keyboard.current[blockKey].isPressed)
            return true;

        return alsoRightMouse && Mouse.current != null && Mouse.current.rightButton.isPressed;
    }

    private void SetBlocking(bool value)
    {
        if (blocking == value) return;

        blocking = value;
        if (animator != null) animator.SetBool(BlockingParam, value);

        if (value)
        {
            playerController.StopMovement();
            return;
        }

        // Мёртвый игрок остаётся обездвиженным: движение вернёт респавн.
        if (!playerHealth.IsDead) playerController.ResumeMovement();
    }

    private void HandleDied()
    {
        SetBlocking(false);
    }

    /// <summary>Включает и выключает блок. Тесты входят тем же путём, что и ввод.</summary>
    internal void SetBlockingForTests(bool value)
    {
        SetBlocking(value);
    }

    public bool CanBlockFrom(Vector3 attackerPosition)
    {
        if (!blocking) return false;

        Vector3 direction = attackerPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return true;

        return Vector3.Angle(transform.forward, direction) <= blockAngle * 0.5f;
    }

    public void OnBlocked(DamageInfo info)
    {
        if (Time.time - lastBlockHitTime < blockHitCooldown) return;
        lastBlockHitTime = Time.time;

        if (animator != null) animator.SetTrigger(BlockHitTrigger);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, 1.5f);

        Vector3 left = Quaternion.Euler(0f, -blockAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, blockAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawLine(transform.position, transform.position + left * 2f);
        Gizmos.DrawLine(transform.position, transform.position + right * 2f);
    }
}
