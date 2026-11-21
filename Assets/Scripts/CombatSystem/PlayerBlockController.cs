using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player block. Uses the existing Input System devices (no second input manager).
/// Hold Shift or Right Mouse Button. A blocked hit deals no damage and plays BlockHit.
/// </summary>
[DisallowMultipleComponent]
public class PlayerBlockController : MonoBehaviour, ICombatBlockable
{
    [Header("Input")]
    [SerializeField] private Key blockKey = Key.LeftShift;
    [SerializeField] private bool alsoRightMouse = true;

    [Header("Block")]
    [Tooltip("Total block cone in degrees. 120 = 60 degrees to each side.")]
    [SerializeField, Range(30f, 360f)] private float blockAngle = 120f;
    [SerializeField, Min(0f)] private float blockHitCooldown = 0.25f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerHealth playerHealth;

    private bool blocking;
    private float lastBlockHitTime = -10f;

    public bool IsBlocking => blocking;
    public float BlockAngle => blockAngle;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
    }

    private void OnDisable()
    {
        SetBlocking(false);
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            SetBlocking(false);
            return;
        }

        if (playerController != null && playerController.IsAttacking)
        {
            SetBlocking(false);
            return;
        }

        bool wantBlock = false;
        if (Keyboard.current != null && Keyboard.current[blockKey].isPressed)
            wantBlock = true;
        if (alsoRightMouse && Mouse.current != null && Mouse.current.rightButton.isPressed)
            wantBlock = true;

        SetBlocking(wantBlock);
    }

    private void SetBlocking(bool value)
    {
        if (blocking == value) return;

        blocking = value;
        if (animator != null) animator.SetBool("IsBlocking", value);

        if (playerController == null) return;
        if (value) playerController.StopMovement();
        else playerController.ResumeMovement();
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

        if (animator != null) animator.SetTrigger("BlockHit");
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
