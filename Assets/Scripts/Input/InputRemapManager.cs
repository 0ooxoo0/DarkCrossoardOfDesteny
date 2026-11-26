using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;

/// <summary>
/// Центральный сервис переназначения управления.
///
/// Ответственности:
///  • хранит действие → две привязки (primary / secondary);
///  • строит и перестраивает InputAction по текущим привязкам;
///  • ловит следующее нажатие для ребинда (клавиатура, мышь, геймпад);
///  • ищет конфликты и разбиндивает старые действия;
///  • сохраняет/загружает JSON с fallback на значения по умолчанию;
///  • отдаёт игре готовые GetKey / GetKeyDown / GetKeyUp / GetAxis.
///
/// Опрос в Update не аллоцирует: все InputAction и словари созданы заранее.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class InputRemapManager : MonoBehaviour
{
    public const string DefaultFileName = "input_bindings.json";
    private const float RebindIgnoreSeconds = 0.25f;

    /// <summary>Что делать, если клавиша уже занята другим действием.</summary>
    public enum ConflictPolicy
    {
        /// <summary>Снять клавишу со старого действия и показать уведомление.</summary>
        ReplaceExisting = 0,

        /// <summary>Не назначать, показать ошибку и остаться в ожидании.</summary>
        Block = 1
    }

    /// <summary>Описание конфликта для UI.</summary>
    public readonly struct BindingConflict
    {
        public readonly string ActionId;
        public readonly string DisplayName;
        public readonly InputBindingSlot Slot;
        public readonly InputChord Chord;

        public BindingConflict(string actionId, string displayName, InputBindingSlot slot, InputChord chord)
        {
            ActionId = actionId;
            DisplayName = displayName;
            Slot = slot;
            Chord = chord;
        }
    }

    private sealed class RuntimeAction
    {
        public InputActionDefinition Definition;
        public InputAction Action;
        public InputChord Primary;
        public InputChord Secondary;
    }

    private struct BindingRef
    {
        public string ActionId;
        public InputBindingSlot Slot;
    }

    // ------------------------------------------------------------------
    // Статический доступ
    // ------------------------------------------------------------------

    private static InputRemapManager instance;

    public static InputRemapManager Instance => instance;

    /// <summary>Идёт ли ожидание нажатия (Esc в этот момент принадлежит ребинду).</summary>
    public static bool IsRebinding => instance != null && instance.rebinding;

    /// <summary>
    /// Кадр, в котором Esc уже обработан как отмена ребинда.
    /// Нужен, чтобы GameMenuController не закрыл паузу тем же нажатием.
    /// </summary>
    public static int EscapeConsumedFrame { get; private set; } = -1;

    public static bool EscapeConsumedThisFrame => EscapeConsumedFrame == Time.frameCount;

    /// <summary>Позволяет подменить реестр действий (тесты, моды, другой проект).</summary>
    public static InputActionRegistry RegistryOverride { get; set; }

    /// <summary>Создаёт менеджер при необходимости. Вызывается бутстрапом и UI.</summary>
    public static InputRemapManager EnsureExists()
    {
        if (instance != null) return instance;

        var found = FindFirstObjectByType<InputRemapManager>(FindObjectsInactive.Include);
        if (found != null)
        {
            instance = found;
            return found;
        }

        var go = new GameObject("[Input Remap]");
        DontDestroyOnLoad(go);
        return go.AddComponent<InputRemapManager>();
    }

    // ------------------------------------------------------------------
    // Состояние
    // ------------------------------------------------------------------

    [Header("Конфигурация (необязательно)")]
    [SerializeField] private InputActionRegistry registryAsset;
    [SerializeField] private ConflictPolicy conflictPolicy = ConflictPolicy.ReplaceExisting;
    [SerializeField] private string fileName = DefaultFileName;

    private readonly List<InputActionDefinition> definitions = new List<InputActionDefinition>();
    private readonly Dictionary<string, RuntimeAction> actions =
        new Dictionary<string, RuntimeAction>(StringComparer.Ordinal);

    private readonly Dictionary<InputChord, List<BindingRef>> chordIndex =
        new Dictionary<InputChord, List<BindingRef>>();

    private readonly List<BindingConflict> conflictBuffer = new List<BindingConflict>(8);

    private bool rebinding;
    private string rebindActionId;
    private InputBindingSlot rebindSlot;
    private float rebindIgnoreUntil;
    private ButtonControl pendingModifierControl;
    private InputModifier pendingModifier;
    private int suppressPollingFrame = -1;

    // ------------------------------------------------------------------
    // Публичные события (для UI)
    // ------------------------------------------------------------------

    public event Action<string, InputBindingSlot> BindingChanged;
    public event Action BindingsReset;
    public event Action<string, InputBindingSlot> RebindStarted;
    public event Action<string, InputBindingSlot, string> RebindCompleted;
    public event Action RebindCanceled;
    public event Action<string, InputBindingSlot, string> RebindConflict;
    public event Action<string> RebindFailed;

    // ------------------------------------------------------------------
    // Свойства
    // ------------------------------------------------------------------

    public IReadOnlyList<InputActionDefinition> Actions => definitions;

    public ConflictPolicy Policy
    {
        get => conflictPolicy;
        set => conflictPolicy = value;
    }

    public string FileName
    {
        get => fileName;
        set => fileName = string.IsNullOrWhiteSpace(value) ? DefaultFileName : value;
    }

    public string SavePath => Path.Combine(Application.persistentDataPath, fileName);

    public bool IsRebindingActive => rebinding;
    public string RebindActionId => rebindActionId;
    public InputBindingSlot RebindSlot => rebindSlot;
    public bool HasPendingModifier => rebinding && pendingModifierControl != null;
    public InputModifier PendingModifier => pendingModifier;

    // ------------------------------------------------------------------
    // Жизненный цикл
    // ------------------------------------------------------------------

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        BuildRegistry();
        LoadFromDiskInternal();
        SanitizeBindings();
        RebuildAllActions();
        RebuildIndex();
    }

    private void OnDestroy()
    {
        CancelRebind();

        foreach (var runtimeAction in actions.Values)
            runtimeAction.Action?.Dispose();

        actions.Clear();
        definitions.Clear();
        chordIndex.Clear();

        if (instance == this) instance = null;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Потеря фокуса — нажатие всё равно не должно «прилипнуть» к ребинду.
        if (!hasFocus) CancelRebind();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) CancelRebind();
    }

    // ------------------------------------------------------------------
    // Инициализация
    // ------------------------------------------------------------------

    private void BuildRegistry()
    {
        definitions.Clear();
        actions.Clear();

        InputActionRegistry source = RegistryOverride != null ? RegistryOverride : registryAsset;
        if (source == null) source = Resources.Load<InputActionRegistry>("InputActionRegistry");
        if (source == null) source = InputActionRegistry.CreateDefault();

        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < source.Actions.Count; i++)
        {
            InputActionDefinition definition = source.Actions[i];
            if (definition == null) continue;

            if (!definition.IsValid(out string error))
            {
                Debug.LogWarning("[InputRemap] Пропущено действие: " + error);
                continue;
            }

            if (!seenIds.Add(definition.id))
            {
                Debug.LogWarning("[InputRemap] Дублирующийся id действия: " + definition.id);
                continue;
            }

            InputActionDefinition clone = definition.Clone();
            definitions.Add(clone);

            actions.Add(clone.id, new RuntimeAction
            {
                Definition = clone,
                Primary = clone.defaultPrimary,
                Secondary = clone.defaultSecondary
            });
        }
    }

    private void RebuildAllActions()
    {
        for (int i = 0; i < definitions.Count; i++)
            RebuildAction(actions[definitions[i].id]);
    }

    private void RebuildAction(RuntimeAction runtimeAction)
    {
        runtimeAction.Action?.Dispose();

        InputActionType type = runtimeAction.Definition.valueType == InputValueType.Button
            ? InputActionType.Button
            : InputActionType.Value;

        var action = new InputAction(runtimeAction.Definition.id, type);
        AddChord(action, runtimeAction.Primary);

        if (runtimeAction.Definition.allowSecondary)
            AddChord(action, runtimeAction.Secondary);

        action.Enable();
        runtimeAction.Action = action;
    }

    private static void AddChord(InputAction action, InputChord chord)
    {
        if (chord.IsEmpty) return;

        // Модификаторы имеют смысл только для клавиатуры.
        bool keyboardControl = chord.controlPath.StartsWith("<Keyboard>/", StringComparison.Ordinal);
        if (chord.modifier == InputModifier.None || !keyboardControl)
        {
            action.AddBinding(chord.controlPath);
            return;
        }

        string[] modifierPaths = GetModifierPaths(chord.modifier);
        for (int i = 0; i < modifierPaths.Length; i++)
        {
            action.AddCompositeBinding("OneModifier")
                .With("Modifier", modifierPaths[i])
                .With("Binding", chord.controlPath);
        }
    }

    private static string[] GetModifierPaths(InputModifier modifier)
    {
        switch (modifier)
        {
            case InputModifier.Shift: return new[] { "<Keyboard>/leftShift", "<Keyboard>/rightShift" };
            case InputModifier.Ctrl: return new[] { "<Keyboard>/leftCtrl", "<Keyboard>/rightCtrl" };
            case InputModifier.Alt: return new[] { "<Keyboard>/leftAlt", "<Keyboard>/rightAlt" };
            default: return Array.Empty<string>();
        }
    }

    private void RebuildIndex()
    {
        chordIndex.Clear();

        for (int i = 0; i < definitions.Count; i++)
        {
            RuntimeAction runtimeAction = actions[definitions[i].id];
            AddToIndex(runtimeAction.Primary, definitions[i].id, InputBindingSlot.Primary);
            AddToIndex(runtimeAction.Secondary, definitions[i].id, InputBindingSlot.Secondary);
        }
    }

    private void AddToIndex(InputChord chord, string actionId, InputBindingSlot slot)
    {
        if (chord.IsEmpty) return;

        if (!chordIndex.TryGetValue(chord, out List<BindingRef> refs))
        {
            refs = new List<BindingRef>(2);
            chordIndex.Add(chord, refs);
        }

        refs.Add(new BindingRef { ActionId = actionId, Slot = slot });
    }

    // ------------------------------------------------------------------
    // Опрос в игре (без аллокаций)
    // ------------------------------------------------------------------

    public bool GetKey(string actionId)
    {
        if (suppressPollingFrame == Time.frameCount) return false;
        RuntimeAction runtimeAction = GetRuntime(actionId);
        return runtimeAction != null && runtimeAction.Action != null && runtimeAction.Action.IsPressed();
    }

    public bool GetKeyDown(string actionId)
    {
        if (suppressPollingFrame == Time.frameCount) return false;
        RuntimeAction runtimeAction = GetRuntime(actionId);
        return runtimeAction != null && runtimeAction.Action != null && runtimeAction.Action.WasPressedThisFrame();
    }

    public bool GetKeyUp(string actionId)
    {
        if (suppressPollingFrame == Time.frameCount) return false;
        RuntimeAction runtimeAction = GetRuntime(actionId);
        return runtimeAction != null && runtimeAction.Action != null && runtimeAction.Action.WasReleasedThisFrame();
    }

    /// <summary>
    /// Аналоговое значение действия. Для Vector2-действий возвращает длину вектора,
    /// чтобы метод можно было использовать как «силу» стика.
    /// </summary>
    public float GetAxis(string actionId)
    {
        if (suppressPollingFrame == Time.frameCount) return 0f;
        RuntimeAction runtimeAction = GetRuntime(actionId);
        if (runtimeAction == null || runtimeAction.Action == null) return 0f;

        if (runtimeAction.Definition.valueType == InputValueType.Vector2)
            return runtimeAction.Action.ReadValue<Vector2>().magnitude;

        return runtimeAction.Action.ReadValue<float>();
    }

    public Vector2 GetVector2(string actionId)
    {
        if (suppressPollingFrame == Time.frameCount) return Vector2.zero;
        RuntimeAction runtimeAction = GetRuntime(actionId);
        if (runtimeAction == null || runtimeAction.Action == null) return Vector2.zero;

        if (runtimeAction.Definition.valueType == InputValueType.Vector2)
            return runtimeAction.Action.ReadValue<Vector2>();

        float value = runtimeAction.Action.ReadValue<float>();
        return new Vector2(value, 0f);
    }

    // ------------------------------------------------------------------
    // Чтение привязок
    // ------------------------------------------------------------------

    public InputActionDefinition GetDefinition(string actionId)
    {
        if (string.IsNullOrEmpty(actionId)) return null;
        return actions.TryGetValue(actionId, out RuntimeAction runtimeAction)
            ? runtimeAction.Definition
            : null;
    }

    public InputChord GetBinding(string actionId, InputBindingSlot slot)
    {
        RuntimeAction runtimeAction = GetRuntime(actionId);
        if (runtimeAction == null) return default;
        return slot == InputBindingSlot.Primary ? runtimeAction.Primary : runtimeAction.Secondary;
    }

    public string GetBindingDisplayName(string actionId, InputBindingSlot slot)
    {
        return InputDisplayNames.Get(GetBinding(actionId, slot));
    }

    // ------------------------------------------------------------------
    // Конфликты
    // ------------------------------------------------------------------

    /// <summary>Все привязки, которые совпадают с данным аккордом (с учётом модификатора).</summary>
    public List<BindingConflict> GetConflicts(InputChord chord)
    {
        var result = new List<BindingConflict>(4);
        chord = chord.Normalized();
        if (chord.IsEmpty) return result;

        if (chordIndex.TryGetValue(chord, out List<BindingRef> refs))
            AppendConflicts(refs, chord, result);

        return result;
    }

    /// <summary>Все привязки, использующие данный путь контрола (модификаторы игнорируются).</summary>
    public List<BindingConflict> GetConflicts(string controlPath)
    {
        var result = new List<BindingConflict>(4);
        if (string.IsNullOrEmpty(controlPath)) return result;

        string normalized = new InputChord(controlPath).Normalized().controlPath;
        foreach (KeyValuePair<InputChord, List<BindingRef>> pair in chordIndex)
        {
            if (string.Equals(pair.Key.controlPath, normalized, StringComparison.OrdinalIgnoreCase))
                AppendConflicts(pair.Value, pair.Key, result);
        }

        return result;
    }

    /// <summary>Удобная перегрузка для старого кода, который ещё мыслит KeyCode.</summary>
    public List<BindingConflict> GetConflicts(KeyCode keyCode)
    {
        if (!InputKeyCodeMap.TryGetControlPath(keyCode, out string path))
            return new List<BindingConflict>(0);

        return GetConflicts(path);
    }

    private void AppendConflicts(List<BindingRef> refs, InputChord chord, List<BindingConflict> result)
    {
        for (int i = 0; i < refs.Count; i++)
        {
            BindingRef bindingRef = refs[i];
            RuntimeAction runtimeAction = GetRuntime(bindingRef.ActionId);
            if (runtimeAction == null) continue;

            result.Add(new BindingConflict(
                bindingRef.ActionId,
                runtimeAction.Definition.displayName,
                bindingRef.Slot,
                chord));
        }
    }

    // ------------------------------------------------------------------
    // Ребинд
    // ------------------------------------------------------------------

    /// <summary>Переводит систему в режим ожидания следующего нажатия.</summary>
    public bool StartRebind(string actionId, InputBindingSlot slot = InputBindingSlot.Primary)
    {
        RuntimeAction runtimeAction = GetRuntime(actionId);
        if (runtimeAction == null)
        {
            RebindFailed?.Invoke("Действие «" + actionId + "» не найдено.");
            return false;
        }

        if (!runtimeAction.Definition.rebindable)
        {
            RebindFailed?.Invoke("Действие «" + runtimeAction.Definition.displayName + "» нельзя переназначить.");
            return false;
        }

        if (slot == InputBindingSlot.Secondary && !runtimeAction.Definition.allowSecondary)
        {
            RebindFailed?.Invoke("У действия «" + runtimeAction.Definition.displayName + "» нет второй привязки.");
            return false;
        }

        CancelRebind();

        rebinding = true;
        rebindActionId = actionId;
        rebindSlot = slot;
        rebindIgnoreUntil = Time.unscaledTime + RebindIgnoreSeconds;
        pendingModifierControl = null;
        pendingModifier = InputModifier.None;

        RebindStarted?.Invoke(actionId, slot);
        return true;
    }

    /// <summary>Отмена без изменения привязки.</summary>
    public void CancelRebind()
    {
        if (!rebinding) return;

        rebinding = false;
        rebindActionId = null;
        pendingModifierControl = null;
        pendingModifier = InputModifier.None;

        RebindCanceled?.Invoke();
    }

    private void CancelRebindByEscape()
    {
        if (!rebinding) return;

        // GameMenuController проверяет этот флаг и не закрывает паузу тем же Esc.
        EscapeConsumedFrame = Time.frameCount;
        CancelRebind();
    }

    private void Update()
    {
        if (!rebinding) return;
        if (Time.unscaledTime < rebindIgnoreUntil) return;

        // Модификатор нажат и отпущен без второй клавиши — назначаем сам модификатор.
        // Это позволяет, например, повесить «Бег» на один Shift.
        if (pendingModifierControl != null)
        {
            if (pendingModifierControl.device == null || !pendingModifierControl.device.added)
            {
                // Устройство отключили прямо во время ребинда.
                pendingModifierControl = null;
                pendingModifier = InputModifier.None;
            }
            else if (!pendingModifierControl.isPressed)
            {
                ButtonControl control = pendingModifierControl;
                pendingModifierControl = null;
                CompleteRebind(new InputChord(GetCanonicalPath(control), InputModifier.None));
                return;
            }
        }

        ReadOnlyArray<InputDevice> devices = InputSystem.devices;
        for (int deviceIndex = 0; deviceIndex < devices.Count; deviceIndex++)
        {
            ReadOnlyArray<InputControl> controls = devices[deviceIndex].allControls;
            for (int controlIndex = 0; controlIndex < controls.Count; controlIndex++)
            {
                if (controls[controlIndex] is not ButtonControl button || !button.wasPressedThisFrame)
                    continue;

                // anyKey и подобные синтетические контролы не должны попадать в привязки.
                if (button.synthetic) continue;

                if (button.device is Keyboard keyboard)
                {
                    if (button == keyboard.escapeKey)
                    {
                        CancelRebindByEscape();
                        return;
                    }

                    if (TryGetModifier(button, out InputModifier modifier))
                    {
                        pendingModifierControl = button;
                        pendingModifier = modifier;
                        continue;
                    }
                }

                InputModifier activeModifier = button.device is Keyboard
                    ? GetActiveModifier()
                    : InputModifier.None;

                pendingModifierControl = null;
                CompleteRebind(new InputChord(GetCanonicalPath(button), activeModifier));
                return;
            }
        }
    }

    private void CompleteRebind(InputChord chord)
    {
        string actionId = rebindActionId;
        InputBindingSlot slot = rebindSlot;

        List<BindingConflict> conflicts = GetConflicts(chord);
        bool assigned = TryAssign(actionId, slot, chord);

        if (!assigned)
        {
            // Политика Block: остаёмся в ожидании, игрок выберет другую клавишу.
            RebindConflict?.Invoke(actionId, slot, BuildConflictMessage(chord, conflicts));
            return;
        }

        rebinding = false;
        rebindActionId = null;
        pendingModifierControl = null;
        pendingModifier = InputModifier.None;

        // Клавиша, которой только что назначили действие, не должна
        // в этом же кадре сработать как игровое нажатие.
        suppressPollingFrame = Time.frameCount;

        RebindCompleted?.Invoke(actionId, slot, BuildCompletedMessage(conflicts, actionId));
    }

    private static string BuildConflictMessage(InputChord chord, List<BindingConflict> conflicts)
    {
        string key = InputDisplayNames.Get(chord);
        if (conflicts.Count == 0)
            return "Клавиша " + key + " уже используется.";

        string first = conflicts[0].DisplayName;
        return "Клавиша " + key + " уже используется: «" + first + "». Выберите другую.";
    }

    private string BuildCompletedMessage(List<BindingConflict> conflicts, string actionId)
    {
        for (int i = 0; i < conflicts.Count; i++)
        {
            // Конфликт с этим же действием (primary ↔ secondary) не озвучиваем.
            if (conflicts[i].ActionId == actionId) continue;
            return "Клавиша снята с действия «" + conflicts[i].DisplayName + "».";
        }

        return string.Empty;
    }

    // ------------------------------------------------------------------
    // Изменение привязок
    // ------------------------------------------------------------------

    /// <summary>
    /// Назначает привязку. При политике ReplaceExisting освобождает клавишу
    /// у старого действия. Возвращает false только если сработала политика Block
    /// или действие/слот не найдены.
    /// </summary>
    public bool TryAssign(string actionId, InputBindingSlot slot, InputChord chord)
    {
        RuntimeAction runtimeAction = GetRuntime(actionId);
        if (runtimeAction == null) return false;
        if (!runtimeAction.Definition.rebindable) return false;

        chord = chord.Normalized();

        if (slot == InputBindingSlot.Secondary && !runtimeAction.Definition.allowSecondary)
            return false;

        if (!runtimeAction.Definition.allowModifiers)
            chord.modifier = InputModifier.None;

        if (chord.IsEmpty)
        {
            Unbind(actionId, slot);
            return true;
        }

        if (!IsValidChord(chord)) return false;

        conflictBuffer.Clear();
        if (chordIndex.TryGetValue(chord, out List<BindingRef> refs))
        {
            for (int i = 0; i < refs.Count; i++)
            {
                BindingRef bindingRef = refs[i];
                if (bindingRef.ActionId == actionId && bindingRef.Slot == slot) continue;

                RuntimeAction conflicting = GetRuntime(bindingRef.ActionId);
                if (conflicting == null) continue;

                conflictBuffer.Add(new BindingConflict(
                    bindingRef.ActionId,
                    conflicting.Definition.displayName,
                    bindingRef.Slot,
                    chord));
            }
        }

        if (conflictPolicy == ConflictPolicy.Block && conflictBuffer.Count > 0)
            return false;

        // Авторазбиндинг: сначала освобождаем занятые клавиши.
        for (int i = 0; i < conflictBuffer.Count; i++)
        {
            BindingConflict conflict = conflictBuffer[i];
            ClearSlotInternal(conflict.ActionId, conflict.Slot, true);
        }

        SetSlot(runtimeAction, slot, chord);
        RebuildAction(runtimeAction);
        RebuildIndex();
        Save();

        BindingChanged?.Invoke(actionId, slot);
        return true;
    }

    /// <summary>Очищает привязку действия.</summary>
    public bool Unbind(string actionId, InputBindingSlot slot)
    {
        RuntimeAction runtimeAction = GetRuntime(actionId);
        if (runtimeAction == null) return false;
        if (!runtimeAction.Definition.allowUnbind) return false;
        if (GetBinding(actionId, slot).IsEmpty) return true;

        SetSlot(runtimeAction, slot, default);
        RebuildAction(runtimeAction);
        RebuildIndex();
        Save();

        BindingChanged?.Invoke(actionId, slot);
        return true;
    }

    /// <summary>Сбрасывает одну привязку к значению из реестра.</summary>
    public void ResetAction(string actionId)
    {
        RuntimeAction runtimeAction = GetRuntime(actionId);
        if (runtimeAction == null) return;

        runtimeAction.Primary = runtimeAction.Definition.defaultPrimary;
        runtimeAction.Secondary = runtimeAction.Definition.defaultSecondary;
        RebuildAction(runtimeAction);
        RebuildIndex();
        Save();

        BindingChanged?.Invoke(actionId, InputBindingSlot.Primary);
        BindingChanged?.Invoke(actionId, InputBindingSlot.Secondary);
    }

    /// <summary>Сбрасывает всё управление к значениям по умолчанию.</summary>
    public void ResetToDefaults()
    {
        CancelRebind();

        for (int i = 0; i < definitions.Count; i++)
        {
            RuntimeAction runtimeAction = actions[definitions[i].id];
            runtimeAction.Primary = definitions[i].defaultPrimary;
            runtimeAction.Secondary = definitions[i].defaultSecondary;
            RebuildAction(runtimeAction);
        }

        RebuildIndex();
        Save();
        BindingsReset?.Invoke();
    }

    private void ClearSlotInternal(string actionId, InputBindingSlot slot, bool notify)
    {
        RuntimeAction runtimeAction = GetRuntime(actionId);
        if (runtimeAction == null) return;

        SetSlot(runtimeAction, slot, default);
        RebuildAction(runtimeAction);

        if (notify) BindingChanged?.Invoke(actionId, slot);
    }

    private static void SetSlot(RuntimeAction runtimeAction, InputBindingSlot slot, InputChord chord)
    {
        if (slot == InputBindingSlot.Primary) runtimeAction.Primary = chord;
        else runtimeAction.Secondary = chord;
    }

    private RuntimeAction GetRuntime(string actionId)
    {
        if (string.IsNullOrEmpty(actionId)) return null;
        return actions.TryGetValue(actionId, out RuntimeAction runtimeAction) ? runtimeAction : null;
    }

    // ------------------------------------------------------------------
    // Сохранение / загрузка
    // ------------------------------------------------------------------

    public void Save()
    {
        var file = new InputBindingsFile
        {
            version = InputBindingsFile.CurrentVersion,
            savedAtUtc = DateTime.UtcNow.ToString("o"),
            bindings = new List<InputBindingEntry>(definitions.Count)
        };

        for (int i = 0; i < definitions.Count; i++)
        {
            RuntimeAction runtimeAction = actions[definitions[i].id];
            file.bindings.Add(new InputBindingEntry
            {
                action = definitions[i].id,
                primary = runtimeAction.Primary,
                secondary = runtimeAction.Secondary
            });
        }

        string json = JsonUtility.ToJson(file, true);
        string path = SavePath;

        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temp = path + ".tmp";
            File.WriteAllText(temp, json);

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temp, path, path + ".bak");
                }
                catch (Exception)
                {
                    // На некоторых платформах File.Replace недоступен.
                    File.Copy(temp, path, true);
                    File.Delete(temp);
                }
            }
            else
            {
                File.Move(temp, path);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError("[InputRemap] Не удалось сохранить привязки: " + exception);
        }
    }

    /// <summary>Повторно читает файл (например, после ручного редактирования).</summary>
    public void ReloadFromDisk()
    {
        CancelRebind();

        for (int i = 0; i < definitions.Count; i++)
        {
            RuntimeAction runtimeAction = actions[definitions[i].id];
            runtimeAction.Primary = definitions[i].defaultPrimary;
            runtimeAction.Secondary = definitions[i].defaultSecondary;
        }

        LoadFromDiskInternal();
        SanitizeBindings();
        RebuildAllActions();
        RebuildIndex();
        BindingsReset?.Invoke();
    }

    private void LoadFromDiskInternal()
    {
        string path = SavePath;

        if (TryRead(path, out InputBindingsFile file))
        {
            ApplyLoadedBindings(file);
            return;
        }

        if (TryRead(path + ".bak", out file))
        {
            Debug.LogWarning("[InputRemap] Основной файл привязок повреждён, использован .bak.");
            ApplyLoadedBindings(file);
            return;
        }

        Debug.Log("[InputRemap] Файл привязок не найден — используются значения по умолчанию.");
    }

    private static bool TryRead(string path, out InputBindingsFile file)
    {
        file = null;

        try
        {
            if (!File.Exists(path)) return false;

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return false;

            file = JsonUtility.FromJson<InputBindingsFile>(json);
            if (file == null || file.version <= 0 || file.bindings == null || file.bindings.Count == 0)
                return false;

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[InputRemap] Ошибка чтения " + path + ": " + exception.Message);
            file = null;
            return false;
        }
    }

    private void ApplyLoadedBindings(InputBindingsFile file)
    {
        for (int i = 0; i < file.bindings.Count; i++)
        {
            InputBindingEntry entry = file.bindings[i];
            if (entry == null || string.IsNullOrEmpty(entry.action)) continue;
            if (!actions.TryGetValue(entry.action, out RuntimeAction runtimeAction)) continue;

            runtimeAction.Primary = IsValidChord(entry.primary)
                ? entry.primary.Normalized()
                : runtimeAction.Definition.defaultPrimary;

            runtimeAction.Secondary = IsValidChord(entry.secondary)
                ? entry.secondary.Normalized()
                : runtimeAction.Definition.defaultSecondary;
        }
    }

    private static bool IsValidChord(InputChord chord)
    {
        if (chord.IsEmpty) return true;

        string path = chord.controlPath.Trim();
        if (!path.StartsWith("<", StringComparison.Ordinal)) return false;
        if (path.IndexOf(">/", StringComparison.Ordinal) < 0) return false;
        if (path.IndexOf(' ') >= 0) return false;

        return true;
    }

    /// <summary>
    /// Защита от битого файла: одна клавиша не может быть назначена
    /// двум действиям, а primary и secondary не могут совпадать.
    /// </summary>
    private void SanitizeBindings()
    {
        var used = new HashSet<InputChord>();

        for (int i = 0; i < definitions.Count; i++)
        {
            RuntimeAction runtimeAction = actions[definitions[i].id];

            if (!runtimeAction.Primary.IsEmpty && !used.Add(runtimeAction.Primary))
                runtimeAction.Primary = default;

            if (!runtimeAction.Secondary.IsEmpty)
            {
                if (runtimeAction.Secondary == runtimeAction.Primary || !used.Add(runtimeAction.Secondary))
                    runtimeAction.Secondary = default;
            }
        }
    }

    // ------------------------------------------------------------------
    // Утилиты
    // ------------------------------------------------------------------

    /// <summary>Превращает InputControl.path ("/Keyboard/w") в "<Keyboard>/w".</summary>
    public static string GetCanonicalPath(InputControl control)
    {
        if (control == null) return string.Empty;

        InputDevice device = control.device;
        if (device == null) return control.path;

        string devicePath = device.path;
        string controlPath = control.path;

        if (!string.IsNullOrEmpty(devicePath) &&
            controlPath.StartsWith(devicePath, StringComparison.Ordinal))
        {
            return "<" + device.layout + ">" + controlPath.Substring(devicePath.Length);
        }

        return controlPath;
    }

    private static bool TryGetModifier(ButtonControl button, out InputModifier modifier)
    {
        switch (button.name)
        {
            case "leftShift":
            case "rightShift":
                modifier = InputModifier.Shift;
                return true;
            case "leftCtrl":
            case "rightCtrl":
                modifier = InputModifier.Ctrl;
                return true;
            case "leftAlt":
            case "rightAlt":
                modifier = InputModifier.Alt;
                return true;
            default:
                modifier = InputModifier.None;
                return false;
        }
    }

    private InputModifier GetActiveModifier()
    {
        if (pendingModifierControl != null && pendingModifierControl.isPressed)
            return pendingModifier;

        return CurrentHeldModifier();
    }

    private static InputModifier CurrentHeldModifier()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return InputModifier.None;

        if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
            return InputModifier.Shift;
        if (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)
            return InputModifier.Ctrl;
        if (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed)
            return InputModifier.Alt;

        return InputModifier.None;
    }
}
