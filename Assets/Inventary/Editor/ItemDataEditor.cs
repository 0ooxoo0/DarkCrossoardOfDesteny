using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Инспектор <see cref="ItemData"/>: для брони показывает выпадающий список вещей,
/// зарегистрированных на персонаже в сцене. Избавляет от ручного ввода имён
/// объектов и опечаток в <see cref="ItemData.equipVisualId"/>.
/// </summary>
[CustomEditor(typeof(ItemData))]
public class ItemDataEditor : Editor
{
    private const string NoVisualLabel = "— без визуала —";
    private const string VisualFieldName = "equipVisualId";
    private const string ManualHint =
        "В открытой сцене нет CharacterEquipmentVisuals — введите идентификатор вручную " +
        "или откройте сцену с персонажем.";

    private CharacterEquipmentVisuals cachedVisuals;

    /// <summary>Стандартные поля плюс выбор визуала для брони.</summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        ItemData item = (ItemData)target;
        DrawFields(item);

        if (item.type == ItemType.Armor) DrawVisualField(item);

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Рисует все поля ассета. Для брони поле визуала пропускается: вместо него
    /// будет выпадающий список.
    /// </summary>
    private void DrawFields(ItemData item)
    {
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (item.type == ItemType.Armor && property.name == VisualFieldName) continue;

            EditorGUILayout.PropertyField(property, true);
        }
    }

    private void DrawVisualField(ItemData item)
    {
        CharacterEquipmentVisuals visuals = ResolveVisuals();

        if (visuals == null)
        {
            EditorGUILayout.HelpBox(ManualHint, MessageType.Info);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(VisualFieldName),
                new GUIContent("Визуал на персонаже"));
            return;
        }

        List<string> ids = visuals.GetVisualIds(item.EquipmentSlot);

        // Текущее значение может отсутствовать в сцене — показываем его,
        // чтобы выбор в инспекторе не стирал данные молча.
        string current = item.equipVisualId;
        if (!string.IsNullOrEmpty(current) && !ids.Contains(current)) ids.Add(current);

        List<string> options = new List<string> { NoVisualLabel };
        options.AddRange(ids);

        int currentIndex = string.IsNullOrEmpty(current) ? 0 : options.IndexOf(current);
        if (currentIndex < 0) currentIndex = 0;

        int selected = EditorGUILayout.Popup("Визуал на персонаже", currentIndex, options.ToArray());
        if (selected < 0) return;

        string newValue = selected == 0 ? string.Empty : options[selected];
        SerializedProperty property = serializedObject.FindProperty(VisualFieldName);
        if (property.stringValue != newValue) property.stringValue = newValue;
    }

    private CharacterEquipmentVisuals ResolveVisuals()
    {
        if (cachedVisuals != null) return cachedVisuals;

        CharacterEquipmentVisuals[] found = Object.FindObjectsByType<CharacterEquipmentVisuals>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (found != null && found.Length > 0) cachedVisuals = found[0];
        return cachedVisuals;
    }
}
