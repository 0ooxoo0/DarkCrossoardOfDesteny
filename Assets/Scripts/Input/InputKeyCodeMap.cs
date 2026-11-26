using UnityEngine;

/// <summary>
/// Мост из устаревшего KeyCode в пути нового Input System.
/// Нужен только для удобного GetConflicts(KeyCode); вся внутренняя логика
/// работает с каноническими путями контролов.
/// </summary>
public static class InputKeyCodeMap
{
    public static bool TryGetControlPath(KeyCode keyCode, out string path)
    {
        switch (keyCode)
        {
            case KeyCode.Mouse0: path = "<Mouse>/leftButton"; return true;
            case KeyCode.Mouse1: path = "<Mouse>/rightButton"; return true;
            case KeyCode.Mouse2: path = "<Mouse>/middleButton"; return true;
            case KeyCode.Mouse3: path = "<Mouse>/forwardButton"; return true;
            case KeyCode.Mouse4: path = "<Mouse>/backButton"; return true;

            case KeyCode.Space: path = "<Keyboard>/space"; return true;
            case KeyCode.Return: path = "<Keyboard>/enter"; return true;
            case KeyCode.KeypadEnter: path = "<Keyboard>/numpadEnter"; return true;
            case KeyCode.Escape: path = "<Keyboard>/escape"; return true;
            case KeyCode.Tab: path = "<Keyboard>/tab"; return true;
            case KeyCode.Backspace: path = "<Keyboard>/backspace"; return true;
            case KeyCode.Delete: path = "<Keyboard>/delete"; return true;
            case KeyCode.Insert: path = "<Keyboard>/insert"; return true;
            case KeyCode.Home: path = "<Keyboard>/home"; return true;
            case KeyCode.End: path = "<Keyboard>/end"; return true;
            case KeyCode.PageUp: path = "<Keyboard>/pageUp"; return true;
            case KeyCode.PageDown: path = "<Keyboard>/pageDown"; return true;
            case KeyCode.UpArrow: path = "<Keyboard>/upArrow"; return true;
            case KeyCode.DownArrow: path = "<Keyboard>/downArrow"; return true;
            case KeyCode.LeftArrow: path = "<Keyboard>/leftArrow"; return true;
            case KeyCode.RightArrow: path = "<Keyboard>/rightArrow"; return true;
            case KeyCode.LeftShift: path = "<Keyboard>/leftShift"; return true;
            case KeyCode.RightShift: path = "<Keyboard>/rightShift"; return true;
            case KeyCode.LeftControl: path = "<Keyboard>/leftCtrl"; return true;
            case KeyCode.RightControl: path = "<Keyboard>/rightCtrl"; return true;
            case KeyCode.LeftAlt: path = "<Keyboard>/leftAlt"; return true;
            case KeyCode.RightAlt: path = "<Keyboard>/rightAlt"; return true;
            case KeyCode.LeftCommand: path = "<Keyboard>/leftMeta"; return true;
            case KeyCode.RightCommand: path = "<Keyboard>/rightMeta"; return true;
            case KeyCode.CapsLock: path = "<Keyboard>/capsLock"; return true;
            case KeyCode.BackQuote: path = "<Keyboard>/backquote"; return true;
            case KeyCode.Minus: path = "<Keyboard>/minus"; return true;
            case KeyCode.Equals: path = "<Keyboard>/equals"; return true;
            case KeyCode.LeftBracket: path = "<Keyboard>/leftBracket"; return true;
            case KeyCode.RightBracket: path = "<Keyboard>/rightBracket"; return true;
            case KeyCode.Backslash: path = "<Keyboard>/backslash"; return true;
            case KeyCode.Semicolon: path = "<Keyboard>/semicolon"; return true;
            case KeyCode.Quote: path = "<Keyboard>/quote"; return true;
            case KeyCode.Comma: path = "<Keyboard>/comma"; return true;
            case KeyCode.Period: path = "<Keyboard>/period"; return true;
            case KeyCode.Slash: path = "<Keyboard>/slash"; return true;
        }

        if (keyCode >= KeyCode.Keypad0 && keyCode <= KeyCode.Keypad9)
        {
            path = "<Keyboard>/numpad" + ((int)keyCode - (int)KeyCode.Keypad0);
            return true;
        }

        if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
        {
            path = "<Keyboard>/" + (char)('a' + ((int)keyCode - (int)KeyCode.A));
            return true;
        }

        if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
        {
            path = "<Keyboard>/" + ((int)keyCode - (int)KeyCode.Alpha0);
            return true;
        }

        if (keyCode >= KeyCode.F1 && keyCode <= KeyCode.F12)
        {
            path = "<Keyboard>/f" + ((int)keyCode - (int)KeyCode.F1 + 1);
            return true;
        }

        path = null;
        return false;
    }
}
