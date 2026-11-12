using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Ввод культивации: новый Input System (как в проекте), с откатом на legacy Input.
/// Escape не используется — его занимает UiManager.
/// </summary>
public static class CultivationInput
{
    public static bool Pressed(Key key, KeyCode legacy)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
            return keyboard[key].wasPressedThisFrame;

        try
        {
            return Input.GetKeyDown(legacy);
        }
        catch (System.InvalidOperationException)
        {
            return false;
        }
    }
}
