using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Мост к AbilitySystem: TechniqueData → AbilityData и регистрация в слотах 4–5.
/// Техники хранятся отдельным списком и не затирают globalAbilities (магию оружия).
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

    /// <summary>Кладёт технику в свободный слот 4–5.</summary>
    public void RegisterTechnique(TechniqueData technique)
    {
        AssignTechnique(technique);
    }

    /// <summary>Назначить технику в слот 4–5 (индексы 3–4). -1 = первый свободный.</summary>
    public bool AssignTechnique(TechniqueData technique, int slotIndex = -1)
    {
        if (technique == null) return false;

        if (technique.Kind == TechniqueKind.Passive)
        {
            Debug.Log("[Cultivation] " + technique.DisplayName + " — пассивная техника, в слот не кладётся.");
            return false;
        }

        if (abilitySystem == null)
        {
            Debug.LogWarning("[Cultivation] На игроке нет AbilitySystem — технику некуда положить.");
            return false;
        }

        if (dao != null && !dao.IsDaoUnlocked(technique.DaoType))
        {
            Debug.LogWarning("[Cultivation] Дао " + technique.DaoType + " не открыт — технику нельзя назначить.");
            return false;
        }

        int slot = slotIndex;
        if (slot < 3 || slot >= AbilitySystem.SlotCount)
            slot = FindFreeTechniqueSlot();
        if (slot < 0)
        {
            Debug.Log("[Cultivation] Слоты 4–5 заняты, технику некуда положить.");
            return false;
        }

        AbilityData data = FindRuntimeAbility(technique.TechniqueId);
        if (data == null)
        {
            data = ToAbilityData(technique);
            if (data == null) return false;
            runtimeAbilities.Add(data);
        }

        RemoveOtherAtSlot(slot, data);
        data.hotkey = SlotToKey(slot);
        techniquesById[technique.TechniqueId] = technique;

        abilitySystem.SetTechniqueAbilities(runtimeAbilities);

        OnTechniqueRegistered?.Invoke(technique);
        Debug.Log("[Cultivation] " + technique.DisplayName + " назначена в слот " + (slot + 1) + " (AbilitySystem).");
        return true;
    }

    public void UnassignTechnique(TechniqueData technique)
    {
        if (technique == null) return;

        for (int i = runtimeAbilities.Count - 1; i >= 0; i--)
        {
            AbilityData data = runtimeAbilities[i];
            if (data == null || data.abilityId != technique.TechniqueId) continue;

            runtimeAbilities.RemoveAt(i);
            techniquesById.Remove(technique.TechniqueId);
            Destroy(data);
            break;
        }

        if (abilitySystem != null)
            abilitySystem.SetTechniqueAbilities(runtimeAbilities);
    }

    private AbilityData FindRuntimeAbility(string techniqueId)
    {
        for (int i = 0; i < runtimeAbilities.Count; i++)
        {
            if (runtimeAbilities[i] != null && runtimeAbilities[i].abilityId == techniqueId)
                return runtimeAbilities[i];
        }
        return null;
    }

    private int FindFreeTechniqueSlot()
    {
        for (int slot = 3; slot < AbilitySystem.SlotCount; slot++)
        {
            bool used = false;
            for (int i = 0; i < runtimeAbilities.Count; i++)
            {
                if (runtimeAbilities[i] != null && HotkeyToSlot(runtimeAbilities[i].hotkey) == slot)
                {
                    used = true;
                    break;
                }
            }
            if (!used) return slot;
        }
        return -1;
    }

    private void RemoveOtherAtSlot(int slot, AbilityData keep)
    {
        for (int i = runtimeAbilities.Count - 1; i >= 0; i--)
        {
            AbilityData candidate = runtimeAbilities[i];
            if (candidate == null)
            {
                runtimeAbilities.RemoveAt(i);
                continue;
            }
            if (candidate == keep) continue;
            if (HotkeyToSlot(candidate.hotkey) != slot) continue;

            runtimeAbilities.RemoveAt(i);
            techniquesById.Remove(candidate.abilityId);
            Destroy(candidate);
        }
    }

    private static int HotkeyToSlot(KeyCode hotkey)
    {
        int index = (int)hotkey - (int)KeyCode.Alpha1;
        return index >= 0 && index < AbilitySystem.SlotCount ? index : -1;
    }

    private static KeyCode SlotToKey(int slot)
    {
        return (KeyCode)((int)KeyCode.Alpha1 + Mathf.Clamp(slot, 0, AbilitySystem.SlotCount - 1));
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

        data.requiredDao = technique.DaoType;
        data.requiredDaoLevel = technique.RequiredDaoLevel;
        data.requiredRealmIndex = technique.RequiredRealmIndex;
        data.qiCost = technique.QiCost;
        data.requiredWeapon = ResolveRequiredWeapon(technique);

        return data;
    }

    private static bool IsMeleeTechnique(TechniqueData technique)
    {
        if (technique.Kind == TechniqueKind.Passive) return false;
        if (technique.Kind == TechniqueKind.Aura) return false;
        return technique.DaoType == DaoType.Sword ||
               technique.DaoType == DaoType.Fist ||
               technique.DaoType == DaoType.Body;
    }

    private static WeaponType ResolveRequiredWeapon(TechniqueData technique)
    {
        switch (technique.DaoType)
        {
            case DaoType.Sword: return WeaponType.Sword;
            case DaoType.Fist:
            case DaoType.Body: return WeaponType.Fists;
            default: return WeaponType.Any;
        }
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
        // Ци списывается в AbilitySystem.TryActivate до установки КД.
        // Здесь оставлена только страховка от повторного списания.
        if (ability == null || qi == null) return;
    }
}
