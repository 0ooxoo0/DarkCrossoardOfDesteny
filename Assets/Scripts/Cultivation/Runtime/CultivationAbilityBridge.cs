using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Мост к AbilitySystem: TechniqueData → AbilityData и регистрация в слотах 1–5.
/// Чужие скрипты не меняются — используются только публичные SetGlobalAbilities/OnAbilityActivated.
/// </summary>
[DisallowMultipleComponent]
public class CultivationAbilityBridge : MonoBehaviour
{
    [SerializeField] private DaoSystem dao;
    [SerializeField] private QiComponent qi;
    [SerializeField] private AbilitySystem abilitySystem;

    private readonly List<AbilityData> runtimeAbilities = new List<AbilityData>();
    private readonly Dictionary<string, TechniqueData> techniquesById = new Dictionary<string, TechniqueData>();

    public event Action<TechniqueData> OnTechniqueRegistered;

    public int RegisteredCount => runtimeAbilities.Count;

    public void Initialize(DaoSystem daoSystem, QiComponent qiComponent, AbilitySystem system)
    {
        Unsubscribe();
        dao = daoSystem;
        qi = qiComponent;
        abilitySystem = system != null ? system : GetComponent<AbilitySystem>();
        Subscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        for (int i = 0; i < runtimeAbilities.Count; i++)
        {
            if (runtimeAbilities[i] != null)
                Destroy(runtimeAbilities[i]);
        }

        runtimeAbilities.Clear();
        techniquesById.Clear();
    }

    /// <summary>Кладёт технику в свободный слот хотбара (клавиши 1–5).</summary>
    public void RegisterTechnique(TechniqueData technique)
    {
        if (technique == null) return;

        if (technique.Kind == TechniqueKind.Passive)
        {
            Debug.Log("[Cultivation] " + technique.DisplayName + " — пассивная техника, в слот не кладётся.");
            return;
        }

        if (abilitySystem == null)
        {
            Debug.LogWarning("[Cultivation] На игроке нет AbilitySystem — технику некуда положить.");
            return;
        }

        if (techniquesById.ContainsKey(technique.TechniqueId))
        {
            OnTechniqueRegistered?.Invoke(technique);
            return;
        }

        AbilityData data = ToAbilityData(technique);
        if (data == null) return;

        techniquesById[technique.TechniqueId] = technique;
        runtimeAbilities.Add(data);
        abilitySystem.SetGlobalAbilities(runtimeAbilities);

        OnTechniqueRegistered?.Invoke(technique);
        Debug.Log("[Cultivation] " + technique.DisplayName + " добавлена в хотбар (AbilitySystem).");
    }

    private AbilityData ToAbilityData(TechniqueData technique)
    {
        var data = ScriptableObject.CreateInstance<AbilityData>();
        data.hideFlags = HideFlags.DontSave;
        data.name = "TechniqueAbility_" + technique.TechniqueId;

        data.abilityId = technique.TechniqueId;
        data.displayName = technique.DisplayName;
        data.icon = technique.Icon;
        data.hotkey = KeyCode.None;
        data.type = IsMeleeTechnique(technique) ? AbilityType.Melee : AbilityType.Magic;
        data.cooldown = Mathf.Max(0.1f, technique.Cooldown);
        data.vfxPrefab = technique.VfxPrefab;
        data.castAnimation = technique.CastAnimationClip;

        return data;
    }

    private static bool IsMeleeTechnique(TechniqueData technique)
    {
        if (technique.Kind != TechniqueKind.Active) return false;
        return technique.DaoType == DaoType.Sword ||
               technique.DaoType == DaoType.Fist ||
               technique.DaoType == DaoType.Body;
    }

    private void Subscribe()
    {
        if (abilitySystem == null) return;
        abilitySystem.OnAbilityActivated -= HandleAbilityActivated;
        abilitySystem.OnAbilityActivated += HandleAbilityActivated;
    }

    private void Unsubscribe()
    {
        if (abilitySystem != null)
            abilitySystem.OnAbilityActivated -= HandleAbilityActivated;
    }

    private void HandleAbilityActivated(int slot, AbilityData ability)
    {
        if (ability == null || qi == null) return;
        if (!techniquesById.TryGetValue(ability.abilityId, out TechniqueData technique) || technique == null) return;

        if (!qi.HasQi(technique.QiCost))
        {
            Debug.LogWarning("[Cultivation] " + technique.DisplayName + ": не хватает Ци (" + technique.QiCost + ").");
            return;
        }

        qi.Spend(technique.QiCost);
    }
}
