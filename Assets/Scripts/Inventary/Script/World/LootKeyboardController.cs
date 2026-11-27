using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Подбор лута без мыши: R — забрать всё, E — забрать выбранное,
/// Up/Down — листать список. Работает только если мышь в бою не использовалась.
/// Движение не блокируется: гасятся только стрелки, WASD ходит всегда.
/// </summary>
[DefaultExecutionOrder(1100)]
[DisallowMultipleComponent]
public class LootKeyboardController : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private LootViewArea viewArea;
    [SerializeField] private LootPanelUI panel;
    [SerializeField] private LootPickupController pickupController;
    [SerializeField] private InventoryPanelUI inventoryPanel;
    [SerializeField] private PlayerController playerController;

    [Header("Клавиши")]
    [SerializeField] private Key takeAllKey = Key.R;
    [SerializeField] private Key takeOneKey = Key.E;
    [SerializeField] private Key nextKey = Key.DownArrow;
    [SerializeField] private Key previousKey = Key.UpArrow;

    [Header("Условия")]
    [Tooltip("Если мышь двигали не дальше этого времени назад — клавиатура уступает ей.")]
    [SerializeField, Min(0.1f)] private float mouseIdleSeconds = 1.5f;
    [Tooltip("Сколько секунд держится выбранный контейнер после ухода из области.")]
    [SerializeField, Min(0f)] private float selectionGrace = 0.4f;

    private readonly List<WorldLootContainer> candidates = new List<WorldLootContainer>();
    private WorldLootContainer selected;
    private int selectedIndex = -1;
    private float lastMouseActivityTime = float.NegativeInfinity;
    private float lostSelectionTime = float.NegativeInfinity;
    private Vector2 lastMousePosition;
    private bool hasMousePosition;

    /// <summary>Клавиатура сейчас ведёт подбор (мышь не трогали).</summary>
    public bool IsKeyboardActive { get; private set; }

    /// <summary>Выбранный контейнер или null.</summary>
    public WorldLootContainer Selected => selected;

    private void Awake()
    {
        if (pickupController == null) pickupController = GetComponent<LootPickupController>();
        if (playerController == null) playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        TrackMouse();

        bool blocked = pickupController != null && pickupController.IsApproaching;
        if (inventoryPanel != null && inventoryPanel.IsOpen) blocked = true;

        IsKeyboardActive = !blocked && IsMouseIdle();

        if (!IsKeyboardActive)
        {
            ReleaseSelection();
            return;
        }

        RefreshSelection();
        HandleKeys();
    }

    /// <summary>
    /// Мышь «взяла управление», если её сдвинули. Тогда панель подбора снова
    /// ведёт курсор, а клавиатура молчит — иначе они дрались бы за панель.
    /// </summary>
    private void TrackMouse()
    {
        if (Mouse.current == null) return;

        Vector2 position = Mouse.current.position.ReadValue();
        if (!hasMousePosition)
        {
            hasMousePosition = true;
            lastMousePosition = position;
            return;
        }

        if ((position - lastMousePosition).sqrMagnitude > 4f)
            lastMouseActivityTime = Time.unscaledTime;

        lastMousePosition = position;
    }

    private bool IsMouseIdle()
    {
        return Time.unscaledTime - lastMouseActivityTime >= mouseIdleSeconds;
    }

    private void RefreshSelection()
    {
        if (viewArea == null) return;

        viewArea.Refresh();
        IReadOnlyList<WorldLootContainer> visible = viewArea.Visible;

        if (visible.Count == 0)
        {
            // Пока игрок не отошёл, держим выбор: контейнер мог мигнуть из-за края области.
            if (selected != null && Time.unscaledTime - lostSelectionTime >= selectionGrace)
                ReleaseSelection();

            return;
        }

        lostSelectionTime = Time.unscaledTime;

        // Выбранный мог опустеть или исчезнуть — тогда берём ближайший.
        if (selected == null || !Contains(visible, selected))
        {
            SetSelected(visible[0]);
            selectedIndex = 0;
            return;
        }

        int index = IndexOf(visible, selected);
        selectedIndex = Mathf.Clamp(index, 0, visible.Count - 1);
    }

    private void HandleKeys()
    {
        if (Keyboard.current == null || selected == null) return;

        // WASD закрывает панель: игрок мог просто пойти дальше, не выбирая лут.
        if (MovePressed())
        {
            ReleaseSelection();
            return;
        }

        if (Keyboard.current[nextKey].wasPressedThisFrame) MoveSelection(1);
        if (Keyboard.current[previousKey].wasPressedThisFrame) MoveSelection(-1);

        if (Keyboard.current[takeAllKey].wasPressedThisFrame) TakeAll();
        if (Keyboard.current[takeOneKey].wasPressedThisFrame) TakeSelected();
    }

    private void MoveSelection(int step)
    {
        if (candidates.Count == 0) return;

        selectedIndex = (selectedIndex + step + candidates.Count) % candidates.Count;
        SetSelected(candidates[selectedIndex]);
    }

    private void TakeAll()
    {
        if (pickupController == null || selected == null) return;

        pickupController.RequestTakeAll(selected);
        lostSelectionTime = Time.unscaledTime;
    }

    private void TakeSelected()
    {
        if (pickupController == null || selected == null) return;

        // «Забрать некоторое» — одна единица первого подходящего предмета.
        IReadOnlyList<LootEntry> entries = selected.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (!entries[i].IsValid) continue;

            pickupController.RequestTake(selected, entries[i]);
            lostSelectionTime = Time.unscaledTime;
            return;
        }
    }

    private void SetSelected(WorldLootContainer container)
    {
        selected = container;
        candidates.Clear();

        if (viewArea != null && viewArea.Visible != null)
            candidates.AddRange(viewArea.Visible);

        // Движение не блокируем: гасим только стрелки, которые листают список.
        if (playerController != null) playerController.SuppressArrowMove = container != null;

        // Панель — только подсказка выбора: подбор работает и без неё.
        if (panel == null || container == null) return;

        Camera cam = viewArea != null ? viewArea.ViewCamera : null;
        if (cam == null) return;

        Vector3 screenPoint = cam.WorldToScreenPoint(container.transform.position + Vector3.up);
        if (screenPoint.z <= 0f) return;

        panel.Show(container, screenPoint);
    }

    private void ReleaseSelection()
    {
        selected = null;
        selectedIndex = -1;
        candidates.Clear();
        lostSelectionTime = float.NegativeInfinity;

        if (playerController != null) playerController.SuppressArrowMove = false;
        if (panel != null && panel.IsVisible) panel.Hide();
    }

    private static bool MovePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard.wKey.wasPressedThisFrame
            || keyboard.aKey.wasPressedThisFrame
            || keyboard.sKey.wasPressedThisFrame
            || keyboard.dKey.wasPressedThisFrame;
    }

    private static bool Contains(IReadOnlyList<WorldLootContainer> list, WorldLootContainer value)
    {
        return IndexOf(list, value) >= 0;
    }

    private static int IndexOf(IReadOnlyList<WorldLootContainer> list, WorldLootContainer value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == value) return i;
        }

        return -1;
    }
}
