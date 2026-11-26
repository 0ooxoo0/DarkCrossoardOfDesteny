using System;
using UnityEngine;

/// <summary>
/// Слот привязки внутри действия.
/// Основная и дополнительная клавиши — как в большинстве игр.
/// </summary>
public enum InputBindingSlot
{
    Primary = 0,
    Secondary = 1
}

/// <summary>Тип значения, которое читается из действия.</summary>
public enum InputValueType
{
    /// <summary>Кнопка: GetKey / GetKeyDown / GetKeyUp.</summary>
    Button = 0,

    /// <summary>Ось: GetAxis (мышь, триггеры, колёсико).</summary>
    Axis = 1,

    /// <summary>Двумерный вектор: GetVector2 (стик, delta мыши).</summary>
    Vector2 = 2
}

/// <summary>
/// Модификатор, который нужно удерживать вместе с клавишей.
/// Поддерживается один модификатор на привязку — этого достаточно
/// для игровых действий и не усложняет поиск конфликтов.
/// </summary>
public enum InputModifier
{
    None = 0,
    Shift = 1,
    Ctrl = 2,
    Alt = 3
}

/// <summary>
/// Одна привязка: путь контрола Input System + необязательный модификатор.
/// Пример: "<Keyboard>/w" + Shift = Shift+W.
/// </summary>
[Serializable]
public struct InputChord : IEquatable<InputChord>
{
    /// <summary>Канонический путь контрола, например "<Keyboard>/space".</summary>
    public string controlPath;

    /// <summary>Модификатор, удерживаемый вместе с клавишей.</summary>
    public InputModifier modifier;

    public InputChord(string controlPath, InputModifier modifier = InputModifier.None)
    {
        this.controlPath = controlPath;
        this.modifier = modifier;
    }

    /// <summary>Пустая привязка (действие без клавиши).</summary>
    public bool IsEmpty => string.IsNullOrEmpty(controlPath);

    /// <summary>
    /// Приводит путь к каноническому виду "<Устройство>/контрол".
    /// Понимает как "<Keyboard>/w", так и "/Keyboard/w" (формат InputControl.path).
    /// </summary>
    public InputChord Normalized()
    {
        string path = controlPath == null ? string.Empty : controlPath.Trim();

        // "/Keyboard/w" -> "<Keyboard>/w"
        if (path.Length > 1 && path[0] == '/')
        {
            int slash = path.IndexOf('/', 1);
            if (slash > 0)
            {
                string layout = path.Substring(1, slash - 1);
                path = "<" + layout + ">" + path.Substring(slash);
            }
        }

        return new InputChord(path, path.Length == 0 ? InputModifier.None : modifier);
    }

    public bool Equals(InputChord other)
    {
        return string.Equals(controlPath ?? string.Empty, other.controlPath ?? string.Empty,
                   StringComparison.OrdinalIgnoreCase)
               && modifier == other.modifier;
    }

    public override bool Equals(object obj) => obj is InputChord other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = controlPath == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(controlPath);
            return (hash * 397) ^ (int)modifier;
        }
    }

    public static bool operator ==(InputChord left, InputChord right) => left.Equals(right);
    public static bool operator !=(InputChord left, InputChord right) => !left.Equals(right);

    public override string ToString()
    {
        if (IsEmpty) return "—";
        return modifier == InputModifier.None ? controlPath : modifier + "+" + controlPath;
    }
}
