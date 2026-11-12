using UnityEngine;

/// <summary>
/// Триггер-зона медитации: пока игрок медитирует внутри — множитель скорости (по умолчанию ×2).
/// Просто компонент, никаких алтарей и правок сцены.
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class MeditationZone : MonoBehaviour
{
    [SerializeField, Min(1f)] private float bonusMultiplier = 2f;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool filterByTag = false;

    public float BonusMultiplier => Mathf.Max(1f, bonusMultiplier);

    private void Awake()
    {
        var trigger = GetComponent<Collider>();
        if (!trigger.isTrigger)
            trigger.isTrigger = true;

        // У игрока NavMeshAgent и нет Rigidbody — без кинематического тела на зоне
        // OnTriggerEnter/Exit не сработают.
        if (GetComponent<Rigidbody>() == null)
        {
            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    public void SetMultiplier(float value) => bonusMultiplier = Mathf.Max(1f, value);

    private void OnTriggerEnter(Collider other)
    {
        var controller = FindController(other);
        if (controller != null)
            controller.RegisterZone(this);
    }

    private void OnTriggerExit(Collider other)
    {
        var controller = FindController(other);
        if (controller != null)
            controller.UnregisterZone(this);
    }

    private MeditationController FindController(Collider other)
    {
        if (other == null) return null;
        if (filterByTag && !other.CompareTag(playerTag)) return null;
        return other.GetComponentInParent<MeditationController>();
    }
}
