using UnityEngine;

/// <summary>
/// Числа алхимии: шансы, штрафы, пороги качества и скорость обучения.
/// Один ассет на игру, значения меняются без правки кода.
/// </summary>
[CreateAssetMenu(fileName = "AlchemyConfig", menuName = "Cultivation/Alchemy Config")]
public class AlchemyConfig : ScriptableObject
{
    [Header("Шанс успеха")]
    [SerializeField, Min(0f)] private float chancePerRank = 0.05f;
    [SerializeField, Min(0f)] private float chancePerComprehension = 0.01f;
    [Tooltip("Бонус за работу у печи.")]
    [SerializeField, Min(0f)] private float stationBonus = 0.15f;
    [Tooltip("Штраф за походный крафт.")]
    [SerializeField, Min(0f)] private float portablePenalty = 0.25f;
    [SerializeField, Range(0f, 1f)] private float minChance = 0.05f;
    [SerializeField, Range(0f, 1f)] private float maxChance = 0.95f;

    [Header("Последствия")]
    [Tooltip("Доля каждого ингредиента, которая тратится при провале.")]
    [SerializeField, Range(0f, 1f)] private float ingredientLossOnFail = 0.5f;
    [SerializeField, Min(0)] private int masteryOnFail = 1;

    [Header("Качество")]
    [SerializeField, Min(0f)] private float veinsMargin = 0.15f;
    [SerializeField, Min(0f)] private float cloudMargin = 0.30f;
    [SerializeField, Min(0f)] private float veinsQualityBonus = 0.05f;
    [SerializeField, Min(0f)] private float cloudQualityBonus = 0.10f;
    [Tooltip("Штраф к качеству при походном крафте.")]
    [SerializeField, Min(0f)] private float portableQualityPenalty = 0.10f;

    [Header("Мастерство")]
    [SerializeField, Min(1)] private int masteryPerSubRank = 100;

    public float ChancePerRank => Mathf.Max(0f, chancePerRank);
    public float ChancePerComprehension => Mathf.Max(0f, chancePerComprehension);
    public float StationBonus => Mathf.Max(0f, stationBonus);
    public float PortablePenalty => Mathf.Max(0f, portablePenalty);
    public float MinChance => Mathf.Clamp01(minChance);
    public float MaxChance => Mathf.Clamp(maxChance, MinChance, 1f);
    public float IngredientLossOnFail => Mathf.Clamp01(ingredientLossOnFail);
    public int MasteryOnFail => Mathf.Max(0, masteryOnFail);
    public float VeinsMargin => Mathf.Max(0f, veinsMargin);
    public float CloudMargin => Mathf.Max(VeinsMargin, cloudMargin);
    public float VeinsQualityBonus => Mathf.Max(0f, veinsQualityBonus);
    public float CloudQualityBonus => Mathf.Max(0f, cloudQualityBonus);
    public float PortableQualityPenalty => Mathf.Max(0f, portableQualityPenalty);
    public int MasteryPerSubRank => Mathf.Max(1, masteryPerSubRank);
}
