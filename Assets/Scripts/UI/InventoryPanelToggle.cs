using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Переключатель панели инвентаря по клавише I.
/// Висит на всегда активном корне Canvas и прячет только дочернюю панель,
/// поэтому сам не выключается вместе с ней. Другие системы не трогает.
/// </summary>
[DisallowMultipleComponent]
public class InventoryPanelToggle : MonoBehaviour
{
    [Header("Панель")]
    [Tooltip("Объект, который показывается/прячется. Пусто — первый дочерний объект.")]
    [SerializeField] private GameObject panel;

    [Header("Клавиша")]
    [SerializeField] private Key toggleKey = Key.I;
    [SerializeField] private KeyCode legacyToggleKey = KeyCode.I;

    public bool IsOpen { get; private set; }
    public GameObject Panel => panel;

    private void Awake()
    {
        if (panel != null) return;
        panel = transform.childCount > 0 ? transform.GetChild(0).gameObject : gameObject;
    }

    private void Start()
    {
        SetOpen(false);
    }

    private void Update()
    {
        if (!Pressed()) return;
        SetOpen(!IsOpen);
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool value)
    {
        IsOpen = value;
        if (panel != null) panel.SetActive(value);
    }

    private bool Pressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null) return keyboard[toggleKey].wasPressedThisFrame;

        try
        {
            return Input.GetKeyDown(legacyToggleKey);
        }
        catch (System.InvalidOperationException)
        {
            return false;
        }
    }
}
