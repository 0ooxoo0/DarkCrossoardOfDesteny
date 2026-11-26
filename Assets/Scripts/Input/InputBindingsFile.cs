using System;
using System.Collections.Generic;

/// <summary>Одна запись сохранения: действие и две его привязки.</summary>
[Serializable]
public sealed class InputBindingEntry
{
    public string action;
    public InputChord primary;
    public InputChord secondary;
}

/// <summary>
/// Корневой объект JSON-файла привязок.
/// JsonUtility не умеет словари, поэтому используется список —
/// зато файл читаемый и стабильно версионируется.
/// </summary>
[Serializable]
public sealed class InputBindingsFile
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public string savedAtUtc;
    public List<InputBindingEntry> bindings = new List<InputBindingEntry>();
}
