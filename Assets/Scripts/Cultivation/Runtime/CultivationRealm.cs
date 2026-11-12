using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Один реалм культивации (например «Очищение Ци»). Только данные, без логики.
/// </summary>
[CreateAssetMenu(fileName = "Realm_New", menuName = "Cultivation/Cultivation Realm", order = 0)]
public class CultivationRealm : ScriptableObject
{
    [Header("Идентификация")]
    [SerializeField] private string realmName = "Новый реалм";
    [Tooltip("Необязательное иероглифическое название (нужен CJK-шрифт в UI).")]
    [SerializeField] private string realmNameChinese = "";
    [SerializeField, TextArea(2, 5)] private string description = "";
    [SerializeField] private int realmIndex = 0;

    [Header("Ступени")]
    [SerializeField, Min(1)] private int stagesCount = 3;

    [Header("Бонусы за каждую пройденную ступень")]
    [SerializeField] private float qiBonusPerStage = 10f;
    [SerializeField] private float hpBonusPerStage = 5f;
    [SerializeField] private float regenBonusPerStage = 0.5f;

    [Header("Требования прорыва")]
    [SerializeField, Min(0)] private int requiredComprehension = 0;
    [Tooltip("Строковые теги материалов — без ссылок на ItemData, чтобы не пересекаться с инвентарём.")]
    [SerializeField] private List<string> requiredMaterials = new List<string>();

    [Header("Прорыв")]
    [Tooltip("Базовый шанс успеха для этого реалма. Отрицательное значение = взять из CultivationSystem.")]
    [SerializeField, Range(-1f, 1f)] private float baseBreakthroughChance = -1f;

    public string RealmName => realmName;
    public string RealmNameChinese => realmNameChinese;
    public string Description => description;
    public int RealmIndex => realmIndex;
    public int StagesCount => Mathf.Max(1, stagesCount);
    public float QiBonusPerStage => qiBonusPerStage;
    public float HpBonusPerStage => hpBonusPerStage;
    public float RegenBonusPerStage => regenBonusPerStage;
    public int RequiredComprehension => Mathf.Max(0, requiredComprehension);
    public IReadOnlyList<string> RequiredMaterials => requiredMaterials;
    public float BaseBreakthroughChance => baseBreakthroughChance;

    /// <summary>Используется setup-скриптом и runtime-заглушкой конфига.</summary>
    public void Setup(string name, string nameChinese, string desc, int index, int stages,
        float qiBonus, float hpBonus, float regenBonus, int comprehension,
        float chance, List<string> materials)
    {
        realmName = name;
        realmNameChinese = nameChinese;
        description = desc;
        realmIndex = index;
        stagesCount = Mathf.Max(1, stages);
        qiBonusPerStage = qiBonus;
        hpBonusPerStage = hpBonus;
        regenBonusPerStage = regenBonus;
        requiredComprehension = Mathf.Max(0, comprehension);
        baseBreakthroughChance = chance;
        requiredMaterials = materials != null ? new List<string>(materials) : new List<string>();
        ApplyName();
    }

    public void AddMaterialTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return;
        if (!requiredMaterials.Contains(tag)) requiredMaterials.Add(tag);
    }

    private void OnValidate()
    {
        stagesCount = Mathf.Max(1, stagesCount);
        requiredComprehension = Mathf.Max(0, requiredComprehension);
        ApplyName();
    }

    private void ApplyName()
    {
        if (string.IsNullOrEmpty(realmName)) realmName = name;
        if (string.IsNullOrEmpty(realmName)) realmName = "Реалм " + (realmIndex + 1);
    }
}
