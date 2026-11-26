using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Наведение мышью на контейнер лута: подсвечивает его контуром и показывает
/// экранную панель с содержимым. Заодно сообщает <see cref="PointerGuard"/>,
/// что клик сейчас не должен превращаться в атаку.
/// </summary>
public class LootPointerController : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LootPanelUI panel;
    [SerializeField] private InventoryPanelUI inventoryPanel;
    [SerializeField] private LootPickupController pickupController;

    [Header("Луч")]
    [Tooltip("Слои, по которым ищем контейнер под курсором.")]
    [SerializeField] private LayerMask rayMask = ~0;
    [SerializeField] private float maxDistance = 100f;

    [Header("Скрытие")]
    [Tooltip("Задержка перед скрытием панели, когда курсор ушёл с контейнера.")]
    [SerializeField] private float hideDelay = 0.2f;

    [Tooltip("Сколько секунд не открывать панель у контейнера после подхода.")]
    [SerializeField] private float reopenDelay = 1.5f;

    [Header("Подсветка")]
    [Tooltip("Смещение точки панели над центром контейнера.")]
    [SerializeField] private float panelHeightOffset = 0.5f;

    private WorldLootContainer current;
    private OutlineTarget currentOutline;
    private float hideTimer;

    [Header("Клавиатурный подбор")]
    [Tooltip("Пока клавиатура ведёт подбор, луч мыши молчит: панель держит выбор.")]
    [SerializeField] private LootKeyboardController keyboardController;

    private void Update()
    {
        // Клавиатура ведёт подбор — мышиный луч не вмешивается.
        if (keyboardController != null && keyboardController.IsKeyboardActive)
        {
            PointerGuard.IsBlockingAttack = keyboardController.Selected != null;
            return;
        }

        // Пока открыт инвентарь, луч не нужен: он только мешал бы панели.
        if (inventoryPanel != null && inventoryPanel.IsOpen)
        {
            HideCurrent();
            PointerGuard.IsBlockingAttack = false;
            return;
        }

        // Пока персонаж идёт к луту, панель закрыта — иначе она перекрывает путь.
        if (pickupController != null && pickupController.IsApproaching)
        {
            HideCurrent();
            PointerGuard.IsBlockingAttack = false;
            return;
        }

        if (playerCamera == null || Mouse.current == null)
        {
            HideCurrent();
            PointerGuard.IsBlockingAttack = false;
            return;
        }

        Ray ray = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, maxDistance, rayMask,
            QueryTriggerInteraction.Collide);

        WorldLootContainer container = hasHit
            ? hit.collider.GetComponentInParent<WorldLootContainer>()
            : null;

        if (container != null)
        {
            // Сразу после подхода панель не переоткрываем: игрок только что
            // нажал «Забрать», и всплывающий список поверх движения мешает.
            if (IsSuppressed(container))
            {
                HideCurrent();
                PointerGuard.IsBlockingAttack = false;
                return;
            }

            Show(container);
            return;
        }

        // Курсор над самой панелью лута: EventSystem видит UI, луч — нет.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            hideTimer = 0f;
            PointerGuard.IsBlockingAttack = true;
            return;
        }

        PointerGuard.IsBlockingAttack = false;
        hideTimer += Time.deltaTime;

        if (hideTimer >= hideDelay) HideCurrent();
    }

    private void OnDisable()
    {
        HideCurrent();
        PointerGuard.IsBlockingAttack = false;
    }

    private bool IsSuppressed(WorldLootContainer container)
    {
        if (pickupController == null) return false;
        if (pickupController.LastInteractedContainer != container) return false;

        return Time.time - pickupController.LastInteractionTime < reopenDelay;
    }

    private void Show(WorldLootContainer container)
    {
        hideTimer = 0f;
        PointerGuard.IsBlockingAttack = true;

        bool changed = current != container;
        if (changed) SetOutline(container);
        current = container;

        if (currentOutline != null) currentOutline.Show(true);

        if (panel == null) return;

        Vector3 worldPoint = container.transform.position + Vector3.up * panelHeightOffset;
        Vector3 screenPoint = playerCamera.WorldToScreenPoint(worldPoint);

        // За спиной камеры панель показывать некуда.
        if (screenPoint.z <= 0f) return;

        panel.Show(container, screenPoint);
    }

    private void SetOutline(WorldLootContainer container)
    {
        if (currentOutline != null) currentOutline.Show(false);

        currentOutline = container != null ? container.GetComponent<OutlineTarget>() : null;
    }

    private void HideCurrent()
    {
        current = null;

        if (currentOutline != null)
        {
            currentOutline.Show(false);
            currentOutline = null;
        }

        if (panel != null) panel.Hide();
    }
}
