using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Связывает данные экипировки с уже существующими объектами одежды персонажа.
/// Ничего не создаёт и не перестраивает: только включает и выключает готовые меши,
/// поэтому скиннинг и аватар остаются нетронутыми.
/// </summary>
public class CharacterEquipmentVisuals : MonoBehaviour
{
    /// <summary>
    /// Одна вещь на персонаже: слот, строковый идентификатор и объект в иерархии.
    /// Идентификатор хранится строкой, чтобы ItemData не зависел от сцены.
    /// </summary>
    [Serializable]
    public class EquipmentVisualEntry
    {
        [Tooltip("Слот экипировки, к которому относится вещь.")]
        public EquipmentSlotType slot;

        [Tooltip("Идентификатор для ItemData.equipVisualId. Обычно имя объекта.")]
        public string visualId;

        [Tooltip("Объект одежды в иерархии персонажа.")]
        public GameObject visual;
    }

    [Header("Одежда")]
    [SerializeField] private List<EquipmentVisualEntry> visuals = new List<EquipmentVisualEntry>();

    /// <summary>Все зарегистрированные вещи — для редактора и отладки.</summary>
    public IReadOnlyList<EquipmentVisualEntry> Visuals => visuals;

    private void Awake()
    {
        // Персонаж появляется в базовой одежде сцены, но система экипировки
        // стартует с пустыми слотами: показываем ровно то, что надето.
        for (int i = 0; i < visuals.Count; i++)
        {
            if (visuals[i]?.visual != null) visuals[i].visual.SetActive(false);
        }
    }

    /// <summary>
    /// Уникальные идентификаторы вещей для слота в порядке регистрации.
    /// Пустые записи пропускаются.
    /// </summary>
    public List<string> GetVisualIds(EquipmentSlotType slot)
    {
        List<string> result = new List<string>();

        for (int i = 0; i < visuals.Count; i++)
        {
            EquipmentVisualEntry entry = visuals[i];
            if (entry?.visual == null || entry.slot != slot) continue;
            if (string.IsNullOrEmpty(entry.visualId)) continue;
            if (result.Contains(entry.visualId)) continue;

            result.Add(entry.visualId);
        }

        return result;
    }

    /// <summary>
    /// Включает вещь с указанным идентификатором и выключает остальные вещи слота.
    /// Пустой идентификатор означает «слот пуст».
    /// </summary>
    public void Apply(EquipmentSlotType slot, string visualId)
    {
        bool hasVisual = !string.IsNullOrEmpty(visualId);

        for (int i = 0; i < visuals.Count; i++)
        {
            EquipmentVisualEntry entry = visuals[i];
            if (entry?.visual == null || entry.slot != slot) continue;

            bool shouldShow = hasVisual && entry.visualId == visualId;
            if (entry.visual.activeSelf != shouldShow) entry.visual.SetActive(shouldShow);
        }
    }
}
