using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Параметры атаки")]
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private float attackRange = 2.5f;
    [SerializeField] private float attackAngle = 150f;
    [SerializeField] private float hitDelay = 0.3f;
    [SerializeField] private float attackCooldown = 0.7f;

    [Header("Ссылки")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform attackOrigin;

    private PlayerController playerController;
    private PlayerControls controls;
    private float nextAttackTime;
    private bool isAttacking;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        if (playerCamera == null) playerCamera = Camera.main;
        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Enable();
        controls.Player.Attack.performed += OnAttack;
    }

    private void OnDisable()
    {
        controls.Player.Attack.performed -= OnAttack;
        controls.Disable();
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (Time.time < nextAttackTime || isAttacking) return;
        if (playerController == null) return;

        Vector3 aimPoint = GetAimPoint();
        playerController.FaceTowards(aimPoint, 0.45f);

        if (!playerController.PlayAttackAnimation()) return;

        isAttacking = true;
        nextAttackTime = Time.time + attackCooldown;
        StartCoroutine(AttackRoutine());
    }

    private Vector3 GetAimPoint()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) return transform.position + transform.forward * attackRange;

        if (Mouse.current != null)
        {
            Ray ray = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, ~(1 << gameObject.layer)))
            {
                return hit.point;
            }

            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (groundPlane.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }
        }

        return transform.position + transform.forward * attackRange;
    }

    private IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(hitDelay);

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 origin = attackOrigin != null
            ? attackOrigin.position
            : transform.position + forward * 0.5f;
        float halfAngleCos = Mathf.Cos(attackAngle * 0.5f * Mathf.Deg2Rad);
        Collider[] hits = Physics.OverlapSphere(origin, attackRange, ~0, QueryTriggerInteraction.Ignore);
        HashSet<IInteractable> damaged = new HashSet<IInteractable>();

        foreach (Collider hit in hits)
        {
            IInteractable interactable = hit.GetComponentInParent<IInteractable>();
            if (interactable == null || damaged.Contains(interactable)) continue;

            Vector3 toTarget = hit.transform.position - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f &&
                Vector3.Dot(forward, toTarget.normalized) < halfAngleCos)
            {
                continue;
            }

            damaged.Add(interactable);
            interactable.TakeDamage(attackDamage, hit.bounds.ClosestPoint(origin));
        }

        isAttacking = false;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = attackOrigin != null ? attackOrigin.position : transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin, attackRange);
    }
}
