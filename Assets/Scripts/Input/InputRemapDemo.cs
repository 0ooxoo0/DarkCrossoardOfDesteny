using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Демонстрация использования InputRemapManager.
/// Повесьте компонент на любой GameObject в сцене и запустите игру.
///
/// Показывает:
///  • чтение GetKeyDown для трёх действий;
///  • запуск ребинда из кода (F1/F2/F3);
///  • сброс к значениям по умолчанию (F4);
///  • текущие привязки в маленьком оверлее.
/// </summary>
public sealed class InputRemapDemo : MonoBehaviour
{
    [Header("Действия для демонстрации")]
    [SerializeField] private string attackAction = "Attack";
    [SerializeField] private string jumpAction = "Jump";
    [SerializeField] private string interactAction = "Interact";

    private InputRemapManager manager;

    private void Start()
    {
        manager = InputRemapManager.EnsureExists();

        Debug.Log("[InputRemapDemo] Атака: " + manager.GetBindingDisplayName(attackAction, InputBindingSlot.Primary));
        Debug.Log("[InputRemapDemo] Прыжок: " + manager.GetBindingDisplayName(jumpAction, InputBindingSlot.Primary));
        Debug.Log("[InputRemapDemo] Взаимодействие: " + manager.GetBindingDisplayName(interactAction, InputBindingSlot.Primary));
    }

    private void Update()
    {
        if (manager == null) return;

        if (manager.GetKeyDown(attackAction)) Debug.Log("[InputRemapDemo] Атака");
        if (manager.GetKeyDown(jumpAction)) Debug.Log("[InputRemapDemo] Прыжок");
        if (manager.GetKeyDown(interactAction)) Debug.Log("[InputRemapDemo] Взаимодействие");

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f1Key.wasPressedThisFrame)
            manager.StartRebind(attackAction, InputBindingSlot.Primary);

        if (keyboard.f2Key.wasPressedThisFrame)
            manager.StartRebind(jumpAction, InputBindingSlot.Primary);

        if (keyboard.f3Key.wasPressedThisFrame)
            manager.StartRebind(interactAction, InputBindingSlot.Secondary);

        if (keyboard.f4Key.wasPressedThisFrame)
            manager.ResetToDefaults();
    }

    private void OnGUI()
    {
        if (manager == null) return;

        GUILayout.BeginArea(new Rect(10f, 10f, 380f, 250f), GUI.skin.box);
        GUILayout.Label("Input Remap Demo");
        GUILayout.Label("Атака: " + manager.GetBindingDisplayName(attackAction, InputBindingSlot.Primary));
        GUILayout.Label("Прыжок: " + manager.GetBindingDisplayName(jumpAction, InputBindingSlot.Primary));
        GUILayout.Label("Взаимодействие (доп.): " + manager.GetBindingDisplayName(interactAction, InputBindingSlot.Secondary));
        GUILayout.Space(6f);

        if (manager.IsRebindingActive)
        {
            GUILayout.Label("Нажмите клавишу… Esc — отмена");
        }
        else
        {
            if (GUILayout.Button("Переназначить атаку (F1)"))
                manager.StartRebind(attackAction, InputBindingSlot.Primary);

            if (GUILayout.Button("Переназначить прыжок (F2)"))
                manager.StartRebind(jumpAction, InputBindingSlot.Primary);

            if (GUILayout.Button("Сбросить всё (F4)"))
                manager.ResetToDefaults();
        }

        GUILayout.EndArea();
    }
}
