using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Декларативное описание действия. Именно здесь, а не в логике менеджера,
/// задаются идентификаторы, подписи и клавиши по умолчанию.
/// </summary>
[Serializable]
public sealed class InputActionDefinition
{
    [Tooltip("Стабильный идентификатор действия. Не меняйте после релиза — он попадает в сохранение.")]
    public string id = "";

    [Tooltip("Подпись для игрока.")]
    public string displayName = "";

    [Tooltip("Группа в списке настроек.")]
    public string category = "Общее";

    [Tooltip("Что читает действие: кнопку, ось или вектор.")]
    public InputValueType valueType = InputValueType.Button;

    [Tooltip("Можно ли переназначать действие из UI.")]
    public bool rebindable = true;

    [Tooltip("Разрешена ли вторая (дополнительная) привязка.")]
    public bool allowSecondary = true;

    [Tooltip("Можно ли использовать Shift/Ctrl/Alt в комбинации.")]
    public bool allowModifiers = true;

    [Tooltip("Можно ли очистить привязку.")]
    public bool allowUnbind = true;

    public InputChord defaultPrimary = new InputChord(string.Empty);
    public InputChord defaultSecondary = new InputChord(string.Empty);

    public InputActionDefinition() { }

    public InputActionDefinition(
        string id,
        string displayName,
        string category,
        string primary,
        string secondary = "",
        InputValueType valueType = InputValueType.Button,
        bool rebindable = true,
        bool allowSecondary = true,
        bool allowModifiers = true,
        bool allowUnbind = true)
    {
        this.id = id;
        this.displayName = displayName;
        this.category = category;
        this.valueType = valueType;
        this.rebindable = rebindable;
        this.allowSecondary = allowSecondary;
        this.allowModifiers = allowModifiers;
        this.allowUnbind = allowUnbind;
        defaultPrimary = new InputChord(primary);
        defaultSecondary = new InputChord(secondary);
    }

    public InputActionDefinition Clone()
    {
        return new InputActionDefinition
        {
            id = id,
            displayName = displayName,
            category = category,
            valueType = valueType,
            rebindable = rebindable,
            allowSecondary = allowSecondary,
            allowModifiers = allowModifiers,
            allowUnbind = allowUnbind,
            defaultPrimary = defaultPrimary.Normalized(),
            defaultSecondary = defaultSecondary.Normalized()
        };
    }

    /// <summary>Проверка описания перед регистрацией в менеджере.</summary>
    public bool IsValid(out string error)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            error = "У действия пустой id.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            error = "У действия «" + id + "» пустое displayName.";
            return false;
        }

        if (valueType != InputValueType.Button && allowSecondary && !string.IsNullOrEmpty(defaultSecondary.controlPath))
        {
            // Для осей вторая привязка не имеет смысла, но это не ошибка —
            // просто предупреждение о неочевидной конфигурации.
            Debug.LogWarning("[InputRemap] У осевого действия «" + id + "» задана вторая привязка.");
        }

        error = null;
        return true;
    }
}

/// <summary>
/// Список действий. Можно создать ассет через
/// Assets → Create → Input → Input Action Registry
/// и положить его в Resources/InputActionRegistry, чтобы менеджер подхватил его автоматически.
/// </summary>
[CreateAssetMenu(menuName = "Input/Input Action Registry", fileName = "InputActionRegistry")]
public sealed class InputActionRegistry : ScriptableObject
{
    [SerializeField] private List<InputActionDefinition> actions = new List<InputActionDefinition>();

    public IReadOnlyList<InputActionDefinition> Actions => actions;

    /// <summary>
    /// Набор по умолчанию для этого проекта. Совпадает с клавишами,
    /// которые уже используются в PlayerControls / PlayerController.
    /// </summary>
    public static InputActionRegistry CreateDefault()
    {
        var registry = CreateInstance<InputActionRegistry>();

        registry.actions = new List<InputActionDefinition>
        {
            // Бой
            new InputActionDefinition("Attack", "Атака", "Бой", "<Mouse>/leftButton", "<Keyboard>/enter"),
            new InputActionDefinition("Interact", "Взаимодействие", "Бой", "<Keyboard>/e"),
            new InputActionDefinition("NextTarget", "Следующая цель", "Бой", "<Keyboard>/2"),
            new InputActionDefinition("PrevTarget", "Предыдущая цель", "Бой", "<Keyboard>/1"),

            // Движение
            new InputActionDefinition("Jump", "Прыжок", "Движение", "<Keyboard>/space"),
            new InputActionDefinition("Sprint", "Бег", "Движение", "<Keyboard>/leftShift"),
            new InputActionDefinition("Crouch", "Присесть", "Движение", "<Keyboard>/c"),
            new InputActionDefinition("Dodge", "Уклонение", "Движение", "<Keyboard>/leftCtrl"),
            new InputActionDefinition("ClickToMove", "Идти к курсору", "Движение", "<Mouse>/rightButton"),

            // Интерфейс
            new InputActionDefinition("Inventory", "Инвентарь", "Интерфейс", "<Keyboard>/i"),

            // Аналоговые действия — только для чтения, не переназначаются из UI.
            new InputActionDefinition(
                "Zoom", "Приближение", "Камера", "<Mouse>/scroll/y",
                valueType: InputValueType.Axis, rebindable: false, allowSecondary: false),
            new InputActionDefinition(
                "Look", "Обзор", "Камера", "<Mouse>/delta",
                valueType: InputValueType.Vector2, rebindable: false, allowSecondary: false)
        };

        return registry;
    }
}
