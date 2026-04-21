using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Настройки атаки")]
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private float attackRange = 3f; // Для геймпада (сфера)
    [SerializeField] private float mouseRayDistance = 50f; // Для мыши

    [Header("Ссылки")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform attackOrigin; // Точка, откуда идёт атака (например, центр персонажа)

    private void Update()
    {
        // Обработка ввода с мыши
        if (Input.GetMouseButtonDown(0)) // ЛКМ
        {
            TryInteractWithMouse();
        }

        // Обработка ввода с геймпада (например, кнопка "A" на Xbox контроллере)
        if (Input.GetButtonDown("Fire1")) // Можно назначить "Fire1" и на кнопку геймпада
        {
            TryInteractWithGamepad();
        }
    }

    // Взаимодействие мышью: пускаем луч из камеры через курсор
    private void TryInteractWithMouse()
    {
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, mouseRayDistance))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                interactable.TakeDamage(attackDamage, hit.point);
                Debug.Log($"Атакован объект {hit.collider.name} мышью");
            }
        }
    }

    // Взаимодействие с геймпада: сфера вокруг точки атаки
    private void TryInteractWithGamepad()
    {
        Vector3 center = attackOrigin != null ? attackOrigin.position : transform.position;
        Collider[] hits = Physics.OverlapSphere(center, attackRange);

        IInteractable closest = null;
        float minDist = float.MaxValue;

        foreach (var col in hits)
        {
            IInteractable interactable = col.GetComponent<IInteractable>();
            if (interactable != null)
            {
                float dist = Vector3.Distance(center, col.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = interactable;
                }
            }
        }

        if (closest != null)
        {
            closest.TakeDamage(attackDamage, center);
            Debug.Log($"Атакован объект геймпадом");
        }
    }

    // Визуализация радиуса атаки в редакторе
    private void OnDrawGizmosSelected()
    {
        if (attackOrigin != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackOrigin.position, attackRange);
        }
    }
}