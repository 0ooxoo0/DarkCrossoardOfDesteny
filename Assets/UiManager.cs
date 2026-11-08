using UnityEngine;
using UnityEngine.InputSystem;

public class UiManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject uiPanel;

    [Header("Игрок (можно оставить пустым)")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInteraction playerInteraction;

    public bool IsOpen { get; private set; }

    private void Start()
    {
        // Синхронизируем состояние на старте: UI закрыт, курсор спрятан
        CloseUI();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            Toggle();
    }

    private void OnDestroy()
    {
        // Чтобы не оставить игру на паузе при перезагрузке сцены
        Time.timeScale = 1f;
    }

    public void Toggle()
    {
        if (IsOpen) CloseUI();
        else OpenUI();
    }

    public void OpenUI()
    {
        if (IsOpen) return;
        IsOpen = true;

        if (uiPanel != null) uiPanel.SetActive(true);

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playerController != null) playerController.enabled = false;
        if (playerInteraction != null) playerInteraction.enabled = false;
    }

    public void CloseUI()
    {
        IsOpen = false;

        if (uiPanel != null) uiPanel.SetActive(false);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerController != null) playerController.enabled = true;
        if (playerInteraction != null) playerInteraction.enabled = true;
    }
}