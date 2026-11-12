using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Путь Дао: цвет ауры, максимум уровня и список техник. Только данные.
/// </summary>
[CreateAssetMenu(fileName = "Dao_New", menuName = "Cultivation/Dao Path", order = 1)]
public class DaoPath : ScriptableObject
{
    [Header("Идентификация")]
    [SerializeField] private DaoType daoType = DaoType.Sword;
    [SerializeField] private string displayName = "Путь Меча";
    [SerializeField] private string displayNameChinese = "剑道";
    [SerializeField] private Sprite icon;
    [SerializeField] private Color color = new Color(0.79f, 0.82f, 0.85f, 1f);
    [SerializeField, TextArea(2, 5)] private string description = "";

    [Header("Развитие")]
    [SerializeField, Min(1)] private int maxLevel = 10;

    [Header("Техники (только данные, применение — AbilitySystem)")]
    [SerializeField] private List<TechniqueData> techniques = new List<TechniqueData>();

    public DaoType DaoType => daoType;
    public string DisplayName => displayName;
    public string DisplayNameChinese => displayNameChinese;
    public Sprite Icon => icon;
    public Color Color => color;
    public string Description => description;
    public int MaxLevel => Mathf.Max(1, maxLevel);
    public IReadOnlyList<TechniqueData> Techniques => techniques;

    public void Setup(DaoType type, string name, string nameChinese, string desc, Color pathColor,
        int levelCap, Sprite pathIcon, List<TechniqueData> pathTechniques)
    {
        daoType = type;
        displayName = name;
        displayNameChinese = nameChinese;
        description = desc;
        color = pathColor;
        maxLevel = Mathf.Max(1, levelCap);
        icon = pathIcon;
        techniques = pathTechniques != null ? new List<TechniqueData>(pathTechniques) : new List<TechniqueData>();
    }

    public void SetColor(Color value) => color = value;
}
