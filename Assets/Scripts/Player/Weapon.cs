using UnityEngine;

public class Weapon : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float attackCooldown = 0.5f; // минимальный интервал между нанесением урона одному объекту

    private bool canDealDamage = true;
    private float lastAttackTime;

    // Этот метод вызывается из анимации (Animation Event) для активации коллайдера
    public void EnableCollider()
    {
        canDealDamage = true;
        GetComponent<Collider>().enabled = true;
    }

    public void DisableCollider()
    {
        GetComponent<Collider>().enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!canDealDamage) return;

        // Проверяем, есть ли на объекте интерфейс IInteractable
        IInteractable interactable = other.GetComponent<IInteractable>();
        if (interactable != null)
        {
            // Проверяем кулдаун для предотвращения многократного урона за один взмах
            if (Time.time - lastAttackTime < attackCooldown) return;

            lastAttackTime = Time.time;
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            interactable.TakeDamage(damage, hitPoint);

            // Вывод в консоль
            Debug.Log($"<color=cyan>Игрок ударил {other.name} | Тип: {interactable.GetResourceType()} | Урон: {damage}</color>");

            // Дополнительно можно вызвать эффект удара, звук и т.д.
        }
    }

    // Если нужно, чтобы урон наносился при каждом кадре нахождения в триггере, можно использовать OnTriggerStay
    // Но лучше ограничить кулдауном
}