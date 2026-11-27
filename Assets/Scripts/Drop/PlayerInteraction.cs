using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// Проверки клика перед атакой: UI, панель лута и взаимодействие.
/// Урон здесь больше не наносится — единственный источник удара это
/// <see cref="CombatHitbox"/> оружия, включённый окном анимации.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Параметры атаки")]
    [Tooltip("Дистанция прицеливания: дальше неё игрок не поворачивается к цели.")]
    [SerializeField] private float attackRange = 2.5f;
    [SerializeField] private float attackCooldown = 0.7f;

    [Header("Ссылки")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform attackOrigin;

    [Header("Блокировка атаки")]
    [Tooltip("Слои, попадание в которые отменяет атаку (Interactable, UI и т.п.)")]
    [SerializeField] private LayerMask blockAttackLayers;

    private PlayerController playerController;
    private float nextAttackTime;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();

        // Камера из инспектора может быть портретной (targetTexture):
        // для прицела и кликов нужна именно игровая.
        if (playerCamera != null && playerCamera.targetTexture != null) playerCamera = null;
        if (playerCamera == null) playerCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (playerController != null)
            playerController.AttackStarted += OnAttackStarted;
    }

    private void OnDisable()
    {
        if (playerController != null)
            playerController.AttackStarted -= OnAttackStarted;
    }

    public bool CanStartAttack(bool comboWindowOpen)
    {
        if (playerController == null || IsAttackBlocked())
            return false;

        if (playerController.IsAttacking)
            return comboWindowOpen;

        return Time.time >= nextAttackTime;
    }

    private void OnAttackStarted()
    {
        if (playerController == null) return;

        Vector3 aimPoint = GetAimPoint();
        playerController.FaceTowards(aimPoint, 0.45f);
        nextAttackTime = Time.time + attackCooldown;
    }

    private bool IsAttackBlocked()
    {
        // 0) Клик по луту и его панели не должен запускать атаку:
        //    проверка идёт до лучей, потому что панель — обычный UI без коллайдера.
        if (PointerGuard.IsBlockingAttack) return true;

        // 1) Стандартный UI (Canvas Screen Space Overlay) — коллайдеров нет,
        //    ловим через EventSystem.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return true;

        // 2) Контейнер лута под курсором. Наведение обновляет флаг в Update,
        //    а клик приходит из input-колбэка раньше — проверяем лучом ещё раз.
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera != null && Mouse.current != null)
        {
            Ray lootRay = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            int lootMask = ~(1 << gameObject.layer);

            if (Physics.Raycast(lootRay, out RaycastHit lootHit, 100f, lootMask,
                    QueryTriggerInteraction.Collide)
                && lootHit.collider.GetComponentInParent<WorldLootContainer>() != null)
            {
                return true;
            }
        }

        // 3) UI/Interactable, у которых есть коллайдеры и они лежат на нужном слое.
        if (blockAttackLayers.value == 0) return false;
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null || Mouse.current == null) return false;

        Ray ray = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        int mask = ~(1 << gameObject.layer); // игнорируем себя

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, mask, QueryTriggerInteraction.Ignore))
        {
            // Блокируем только если ПЕРВОЕ попадание — на «блокирующем» слое.
            // Так объект за стеной не отменит атаку по стене.
            return (blockAttackLayers.value & (1 << hit.collider.gameObject.layer)) != 0;
        }

        return false;
    }

    private Vector3 GetAimPoint()
    {
        if (playerCamera == null) playerCamera = PlayerController.ResolveViewCamera();
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

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = attackOrigin != null ? attackOrigin.position : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, attackRange);
    }
}