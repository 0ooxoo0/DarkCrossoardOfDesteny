using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    public PlayerControls Controls { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;

        Controls = new PlayerControls();
        Controls.Enable();
    }

    private void OnDestroy()
    {
        Controls?.Dispose();
    }
}