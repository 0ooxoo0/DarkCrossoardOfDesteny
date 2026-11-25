using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// Подбор лута с подходом. Если контейнер далеко, персонаж сам идёт к нему,
/// а игрок видит анимацию ходьбы; WASD, атака, таймаут или исчезновение контейнера
/// отменяют подход.
/// </summary>
[DefaultExecutionOrder(1000)]
public class LootPickupController : MonoBehaviour
{
    private const float MoveCancelThreshold = 0.01f;
    private const float ProgressEpsilon = 0.001f;

    [Header("Ссылки")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [Header("Анимация")]
    [Tooltip("Параметр скорости в аниматоре. Пусто — аниматор не трогаем.")]
    [SerializeField] private string speedParameter = "Speed";

    [Header("Подход")]
    [Tooltip("С какого расстояния предмет забирается сразу.")]
    [SerializeField] private float pickupRange = 2.5f;

    [Tooltip("Максимальное время подхода, секунды.")]
    [SerializeField] private float approachTimeout = 8f;

    [Tooltip("Сколько секунд без движения считать застреванием.")]
    [SerializeField] private float noProgressTimeout = 2f;

    /// <summary>Сообщение игроку: почему подход отменён или почему не удалось забрать.</summary>
    public event Action<string> OnStatus;

    /// <summary>Персонаж сейчас идёт к контейнеру.</summary>
    public bool IsApproaching => hasTarget;

    /// <summary>Последний контейнер, к которому шли или который забрали.</summary>
    public WorldLootContainer LastInteractedContainer { get; private set; }

    /// <summary>Время последнего завершения подхода (для подавления повторного показа панели).</summary>
    public float LastInteractionTime { get; private set; } = float.NegativeInfinity;

    private WorldLootContainer targetContainer;
    private ItemData targetItem;
    private int targetQuantity;
    private bool targetTakeAll;
    private bool hasTarget;

    private PlayerControls controls;
    private PlayerController playerController;

    private float approachTimer;
    private float stuckTimer;
    private Vector3 lastPosition;

    private bool hasSpeedParameter;

    private void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        playerController = GetComponent<PlayerController>();
        hasSpeedParameter = HasAnimatorParameter(speedParameter);

        // Свой экземпляр карты ввода нужен, чтобы отличать движение игрока
        // от нашего собственного: PlayerController не даёт это состояние наружу.
        controls = new PlayerControls();
    }

    private void Update()
    {
        if (!hasTarget) return;

        // Контейнер мог быть уничтожен подбором из панели или сменой сцены.
        if (targetContainer == null)
        {
            CancelApproach("Контейнер исчез.");
            return;
        }

        if (PlayerMoved())
        {
            CancelApproach("Подход отменён.");
            return;
        }

        if (playerController != null && playerController.IsAttacking)
        {
            // Атака важнее подбора: стоим и ждём, пока игрок закончит.
            StopMovement();
            return;
        }

        float distance = HorizontalDistance(targetContainer.transform.position);
        if (distance <= pickupRange)
        {
            // Решение принимаем до FinishApproach: он очищает цель.
            WorldLootContainer container = targetContainer;
            ItemData item = targetItem;
            int quantity = targetQuantity;
            bool takeAll = targetTakeAll;

            RememberInteraction(container);
            FinishApproach();

            bool success = takeAll
                ? container.TryTakeAll(inventory)
                : container.TryTake(item, quantity, inventory);

            if (!success) OnStatus?.Invoke("Не удалось забрать предмет.");
            return;
        }

        MoveTowards(distance);
    }

    /// <summary>
    /// Забирает конкретную запись. Вблизи — сразу, издалека — запускает подход.
    /// </summary>
    public LootTakeResult RequestTake(WorldLootContainer container, LootEntry entry)
    {
        if (container == null || container.IsEmpty || !entry.IsValid) return LootTakeResult.Failed;
        if (inventory == null) return LootTakeResult.Failed;

        if (inventory.GetFreeSpace(entry.item) <= 0) return LootTakeResult.NoSpace;

        if (WithinPickupRange(container))
        {
            return container.TryTake(entry.item, entry.quantity, inventory)
                ? LootTakeResult.Taken
                : LootTakeResult.Failed;
        }

        StartApproach(container, entry.item, entry.quantity, false);
        return LootTakeResult.Walking;
    }

    /// <summary>
    /// Забирает всё, что влезает. Вблизи — сразу, издалека — запускает подход.
    /// </summary>
    public LootTakeResult RequestTakeAll(WorldLootContainer container)
    {
        if (container == null || container.IsEmpty) return LootTakeResult.Failed;
        if (inventory == null) return LootTakeResult.Failed;
        if (!HasSpaceForAnything(container)) return LootTakeResult.NoSpace;

        if (WithinPickupRange(container))
        {
            return container.TryTakeAll(inventory) ? LootTakeResult.Taken : LootTakeResult.Failed;
        }

        StartApproach(container, null, 0, true);
        return LootTakeResult.Walking;
    }

    /// <summary>Прерывает подход и сбрасывает движение. Причина уходит в <see cref="OnStatus"/>.</summary>
    public void CancelApproach(string reason = null)
    {
        if (hasTarget) RememberInteraction(targetContainer);
        FinishApproach();

        if (!string.IsNullOrEmpty(reason)) OnStatus?.Invoke(reason);
    }

    private void RememberInteraction(WorldLootContainer container)
    {
        LastInteractedContainer = container;
        LastInteractionTime = Time.time;
    }

    private void StartApproach(WorldLootContainer container, ItemData item, int quantity, bool takeAll)
    {
        // Новый запрос всегда заменяет предыдущий: два одновременных подхода
        // тянули бы персонажа в разные стороны.
        if (targetContainer != null) CancelApproach(null);

        targetContainer = container;
        targetItem = item;
        targetQuantity = quantity;
        targetTakeAll = takeAll;
        hasTarget = true;

        approachTimer = 0f;
        stuckTimer = 0f;
        lastPosition = transform.position;

        controls.Player.Enable();
    }

    private void FinishApproach()
    {
        targetContainer = null;
        targetItem = null;
        targetQuantity = 0;
        targetTakeAll = false;
        hasTarget = false;

        approachTimer = 0f;
        stuckTimer = 0f;

        StopMovement();
        controls.Player.Disable();
    }

    private void MoveTowards(float distance)
    {
        Vector3 to = targetContainer.transform.position - transform.position;
        to.y = 0f;

        if (hasSpeedParameter) animator.SetFloat(speedParameter, 1f);

        Vector3 direction = to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.velocity = direction * agent.speed;
        }
        else
        {
            transform.position += direction * (agent != null ? agent.speed : 0f) * Time.deltaTime;
        }

        Quaternion look = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 10f * Time.deltaTime);

        UpdateTimeouts();
    }

    private void UpdateTimeouts()
    {
        approachTimer += Time.deltaTime;

        if ((transform.position - lastPosition).sqrMagnitude > ProgressEpsilon * ProgressEpsilon)
        {
            stuckTimer = 0f;
            lastPosition = transform.position;
        }
        else
        {
            stuckTimer += Time.deltaTime;
        }

        if (stuckTimer >= noProgressTimeout)
        {
            CancelApproach("Не удаётся подойти к предмету.");
            return;
        }

        if (approachTimer >= approachTimeout)
        {
            CancelApproach("Слишком далеко.");
        }
    }

    private bool PlayerMoved()
    {
        if (controls == null) return false;
        return controls.Player.Move.ReadValue<Vector2>().sqrMagnitude > MoveCancelThreshold;
    }

    private bool HasSpaceForAnything(WorldLootContainer container)
    {
        for (int i = 0; i < container.Entries.Count; i++)
        {
            LootEntry entry = container.Entries[i];
            if (entry.IsValid && inventory.GetFreeSpace(entry.item) > 0) return true;
        }

        return false;
    }

    private bool WithinPickupRange(WorldLootContainer container)
    {
        return HorizontalDistance(container.transform.position) <= pickupRange;
    }

    private float HorizontalDistance(Vector3 point)
    {
        Vector3 delta = point - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    private void StopMovement()
    {
        if (agent != null && agent.isOnNavMesh) agent.velocity = Vector3.zero;
        if (hasSpeedParameter) animator.SetFloat(speedParameter, 0f);
    }

    private bool HasAnimatorParameter(string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName)) return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName) return true;
        }

        return false;
    }

    private void OnDisable()
    {
        CancelApproach(null);
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }
}
