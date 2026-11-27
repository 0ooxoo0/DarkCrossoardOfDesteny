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

    /// <summary>Последнее сообщение о назначении/снятии — для тоста UI, не только Debug.Log.</summary>
    public string LastAssignMessage { get; private set; } = string.Empty;

    public int RegisteredCount => runtimeAbilities.Count;

    /// <summary>Destroy не работает в EditMode (тесты) — там нужен DestroyImmediate.</summary>
    private static void DestroyAbility(AbilityData data)
    {
        if (data == null) return;

        if (Application.isPlaying) Destroy(data);
        else DestroyImmediate(data);
    }

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
                DestroyAbility(runtimeAbilities[i]);
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
            LastAssignMessage = technique.DisplayName + " — пассивная техника, в слот не кладётся.";
            return false;
        }

        // MVP: Block и Aura ещё не реализованы в AbilitySystem — не обманываем игрока.
        if (technique.Kind == TechniqueKind.Block || technique.Kind == TechniqueKind.Aura)
        {
            LastAssignMessage = technique.DisplayName + " — механика в разработке, в слот не назначается.";
            Debug.Log("[Cultivation] " + LastAssignMessage);
            return false;
        }

        if (abilitySystem == null)
        {
            Debug.LogWarning("[Cultivation] На игроке нет AbilitySystem — технику некуда положить.");
            LastAssignMessage = "На игроке нет AbilitySystem — технику некуда положить.";
            return false;
        }

        if (dao != null && !dao.IsDaoUnlocked(technique.DaoType))
        {
            Debug.LogWarning("[Cultivation] Дао " + technique.DaoType + " не открыт — технику нельзя назначить.");
            LastAssignMessage = "Дао " + DaoTypeInfo.GetDisplayName(technique.DaoType) + " не открыт.";
            return false;
        }

        int slot = slotIndex;
        if (slot < 3 || slot >= AbilitySystem.SlotCount)
            slot = FindFreeTechniqueSlot();
        if (slot < 0)
        {
            Debug.Log("[Cultivation] Слоты 4–5 заняты, технику некуда положить.");
            LastAssignMessage = "Слоты 4–5 заняты. Снимите технику, чтобы освободить слот.";
            return false;
        }

        // Повторное назначение той же техники в тот же слот — no-op, не переносим.
        AbilityData existing = FindRuntimeAbility(technique.TechniqueId);
        if (existing != null && HotkeyToSlot(existing.hotkey) == slot)
        {
            LastAssignMessage = technique.DisplayName + " уже в слоте " + (slot + 1) + ".";
            return false;
        }

        AbilityData data = existing;
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

        LastAssignMessage = technique.DisplayName + " назначена в слот " + (slot + 1) + ".";
        OnTechniqueRegistered?.Invoke(technique);
        Debug.Log("[Cultivation] " + LastAssignMessage + " (AbilitySystem).");
        return true;
    }

    /// <summary>Снять технику со слота 4–5. true — слот был занят.</summary>
    public bool UnassignTechnique(int slotIndex)
    {
        if (slotIndex < 3 || slotIndex >= AbilitySystem.SlotCount) return false;

        bool removed = false;
        for (int i = runtimeAbilities.Count - 1; i >= 0; i--)
        {
            AbilityData data = runtimeAbilities[i];
            if (data == null)
            {
                runtimeAbilities.RemoveAt(i);
                continue;
            }
            if (HotkeyToSlot(data.hotkey) != slotIndex) continue;

            runtimeAbilities.RemoveAt(i);
            techniquesById.Remove(data.abilityId);
            LastAssignMessage = data.displayName + " снята со слота " + (slotIndex + 1) + ".";
            DestroyAbility(data);
            removed = true;
            break;
        }

        if (!removed) return false;

        if (abilitySystem != null)
            abilitySystem.SetTechniqueAbilities(runtimeAbilities);

        OnTechniqueRegistered?.Invoke(null);
        return true;
    }

    /// <summary>Техника в слоте 4–5 (индексы 3–4); null — слот пуст.</summary>
    public TechniqueData GetAssignedTechnique(int slotIndex)
    {
        for (int i = 0; i < runtimeAbilities.Count; i++)
        {
            AbilityData data = runtimeAbilities[i];
            if (data == null || HotkeyToSlot(data.hotkey) != slotIndex) continue;
            return techniquesById.TryGetValue(data.abilityId, out TechniqueData technique) ? technique : null;
        }
        return null;
    }

    /// <summary>ID назначенных техник по слотам 4–5. Пустая строка — пустой слот.</summary>
    public string[] GetAssignedTechniqueIds()
    {
        var ids = new string[AbilitySystem.SlotCount - 3];
        for (int slot = 3; slot < AbilitySystem.SlotCount; slot++)
        {
            TechniqueData technique = GetAssignedTechnique(slot);
            ids[slot - 3] = technique != null ? technique.TechniqueId : string.Empty;
        }
        return ids;
    }

    /// <summary>Восстановление назначений из сохранения. Возвращает число занятых слотов.</summary>
    public int RestoreAssignments(string[] ids)
    {
        if (ids == null || dao == null || abilitySystem == null) return 0;

        int restored = 0;
        for (int slot = 3; slot < AbilitySystem.SlotCount; slot++)
        {
            int index = slot - 3;
            if (index >= ids.Length) break;

            string id = ids[index];
            if (string.IsNullOrEmpty(id)) continue;

            TechniqueData technique = dao.GetTechniqueById(id);
            if (technique == null)
            {
                Debug.LogWarning("[Cultivation] Техника из сохранения не найдена: " + id);
                continue;
            }

            if (AssignTechnique(technique, slot) || GetAssignedTechnique(slot) == technique) restored++;
        }

        return restored;
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
            DestroyAbility(data);
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
            DestroyAbility(candidate);
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
        data.power = technique.Power;
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
