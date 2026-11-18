using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Рецепт пилюли: что нужно, какие требования и какой предмет получается.
/// Уровень и реалм проверяет <c>AlchemyService</c>, рецепт только хранит числа.
/// </summary>
[CreateAssetMenu(fileName = "AlchemyRecipe", menuName = "Cultivation/Alchemy Recipe")]
public class AlchemyRecipe : ScriptableObject
{
    [Header("Описание")]
    [SerializeField] private string recipeId;
    [SerializeField] private string displayName;
    [TextArea(2, 5)]
    [SerializeField] private string description;

    [Header("Результат")]
    [SerializeField] private ItemData output;
    [Tooltip("Вариант с жилками. Пусто — поиск по суффиксу имени ассета.")]
    [SerializeField] private ItemData veinsOutput;
    [Tooltip("Облачный вариант. Пусто — поиск по суффиксу имени ассета.")]
    [SerializeField] private ItemData cloudOutput;
    [SerializeField, Min(1)] private int outputCount = 1;

    [Header("Ингредиенты")]
    [SerializeField] private List<AlchemyIngredient> ingredients = new List<AlchemyIngredient>();

    [Header("Требования")]
    [SerializeField] private AlchemyGrade requiredGrade = AlchemyGrade.CommonLow;
    [SerializeField, Min(0)] private int requiredDaoLevel;
    [SerializeField, Min(0)] private int requiredRealmIndex;

    [Header("Крафт")]
    [Range(0f, 1f)]
    [SerializeField] private float baseSuccessChance = 0.5f;
    [Tooltip("Можно ли варить походным способом из Esc-меню.")]
    [SerializeField] private bool portableAllowed;
    [SerializeField, Min(0f)] private float craftSeconds = 2f;
    [SerializeField, Min(0)] private int masteryReward = 5;

    public string RecipeId => recipeId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public string Description => description;
    public ItemData Output => output;
    public ItemData VeinsOutput => veinsOutput;
    public ItemData CloudOutput => cloudOutput;
    public int OutputCount => Mathf.Max(1, outputCount);
    public IReadOnlyList<AlchemyIngredient> Ingredients => ingredients;
    public AlchemyGrade RequiredGrade => requiredGrade;
    public int RequiredDaoLevel => Mathf.Max(0, requiredDaoLevel);
    public int RequiredRealmIndex => Mathf.Max(0, requiredRealmIndex);
    public float BaseSuccessChance => Mathf.Clamp01(baseSuccessChance);
    public bool PortableAllowed => portableAllowed;
    public float CraftSeconds => Mathf.Max(0f, craftSeconds);
    public int MasteryReward => Mathf.Max(0, masteryReward);
}
