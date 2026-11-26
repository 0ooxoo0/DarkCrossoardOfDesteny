using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>
/// Человекочитаемые подписи для путей Input System.
/// Русские названия заданы для самых частых клавиш, остальное
/// берётся из InputControlPath.ToHumanReadableString (W, Space, Button South...).
/// </summary>
public static class InputDisplayNames
{
    private static readonly Dictionary<string, string> RussianNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "<Keyboard>/space", "Пробел" },
            { "<Keyboard>/enter", "Enter" },
            { "<Keyboard>/numpadEnter", "Enter (Num)" },
            { "<Keyboard>/escape", "Esc" },
            { "<Keyboard>/tab", "Tab" },
            { "<Keyboard>/backspace", "Backspace" },
            { "<Keyboard>/delete", "Delete" },
            { "<Keyboard>/insert", "Insert" },
            { "<Keyboard>/home", "Home" },
            { "<Keyboard>/end", "End" },
            { "<Keyboard>/pageUp", "Page Up" },
            { "<Keyboard>/pageDown", "Page Down" },
            { "<Keyboard>/upArrow", "↑" },
            { "<Keyboard>/downArrow", "↓" },
            { "<Keyboard>/leftArrow", "←" },
            { "<Keyboard>/rightArrow", "→" },
            { "<Keyboard>/leftShift", "Shift" },
            { "<Keyboard>/rightShift", "Shift" },
            { "<Keyboard>/leftCtrl", "Ctrl" },
            { "<Keyboard>/rightCtrl", "Ctrl" },
            { "<Keyboard>/leftAlt", "Alt" },
            { "<Keyboard>/rightAlt", "Alt" },
            { "<Keyboard>/capsLock", "Caps Lock" },
            { "<Keyboard>/backquote", "`" },
            { "<Keyboard>/minus", "-" },
            { "<Keyboard>/equals", "=" },
            { "<Keyboard>/leftBracket", "[" },
            { "<Keyboard>/rightBracket", "]" },
            { "<Keyboard>/backslash", "\\" },
            { "<Keyboard>/semicolon", ";" },
            { "<Keyboard>/quote", "'" },
            { "<Keyboard>/comma", "," },
            { "<Keyboard>/period", "." },
            { "<Keyboard>/slash", "/" },

            { "<Mouse>/leftButton", "ЛКМ" },
            { "<Mouse>/rightButton", "ПКМ" },
            { "<Mouse>/middleButton", "СКМ" },
            { "<Mouse>/forwardButton", "Мышь 4" },
            { "<Mouse>/backButton", "Мышь 5" },

            { "<Gamepad>/buttonSouth", "A / ×" },
            { "<Gamepad>/buttonEast", "B / ○" },
            { "<Gamepad>/buttonWest", "X / □" },
            { "<Gamepad>/buttonNorth", "Y / △" },
            { "<Gamepad>/leftShoulder", "LB / L1" },
            { "<Gamepad>/rightShoulder", "RB / R1" },
            { "<Gamepad>/leftTrigger", "LT / L2" },
            { "<Gamepad>/rightTrigger", "RT / R2" },
            { "<Gamepad>/leftStickPress", "L3" },
            { "<Gamepad>/rightStickPress", "R3" },
            { "<Gamepad>/start", "Start" },
            { "<Gamepad>/select", "Select" },
            { "<Gamepad>/dpad/up", "D-Pad ↑" },
            { "<Gamepad>/dpad/down", "D-Pad ↓" },
            { "<Gamepad>/dpad/left", "D-Pad ←" },
            { "<Gamepad>/dpad/right", "D-Pad →" }
        };

    public static string Get(InputChord chord)
    {
        if (chord.IsEmpty) return "—";

        string keyName = GetKeyName(chord.controlPath);
        if (chord.modifier == InputModifier.None) return keyName;
        return ModifierName(chord.modifier) + "+" + keyName;
    }

    public static string GetKeyName(string controlPath)
    {
        if (string.IsNullOrEmpty(controlPath)) return "—";
        if (RussianNames.TryGetValue(controlPath, out string name)) return name;

        try
        {
            return InputControlPath.ToHumanReadableString(
                controlPath,
                InputControlPath.HumanReadableStringOptions.OmitDevice |
                InputControlPath.HumanReadableStringOptions.UseShortNames);
        }
        catch (Exception)
        {
            // Битый или незнакомый путь не должен ронять UI.
            return controlPath;
        }
    }

    public static string ModifierName(InputModifier modifier)
    {
        switch (modifier)
        {
            case InputModifier.Shift: return "Shift";
            case InputModifier.Ctrl: return "Ctrl";
            case InputModifier.Alt: return "Alt";
            default: return string.Empty;
        }
    }
}
