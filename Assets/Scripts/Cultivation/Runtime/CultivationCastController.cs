using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Быстрый демо-каст техник на клавишах 1–5, пока в проекте нет AbilitySystem.
/// Проверяет требования Дао/реалма, списывает Ци, запускает КД и VFX.
/// Как только на игроке появится AbilitySystem/WeaponManager — отключается сам.
/// </summary>
[DisallowMultipleComponent]
public class CultivationCastController : MonoBehaviour
{
    public const int SlotCount = 5;

    [Header("Ссылки")]
    [SerializeField] private DaoSystem dao;
    [SerializeField] private QiComponent qi;
    [SerializeField] private CultivationSystem cultivation;
    [SerializeField] private MeditationController meditation;

    [Header("Демо-каст")]
    [SerializeField] private bool enableDemoCasting = true;
    [SerializeField] private bool dealDamage = true;
    [SerializeField, Min(0.5f)] private float damageRadius = 3.5f;
    [SerializeField, Min(0f)] private float casterReach = 1.1f;

    private readonly TechniqueData[] slots = new TechniqueData[SlotCount];
    private readonly float[] cooldowns = new float[SlotCount];
    private float checkTimer;
    private bool suppressed;

    public event Action<int, TechniqueData> OnSlotChanged;
    public event Action<int, TechniqueData> OnCast;
    public event Action<string> OnMessage;

    public bool IsSuppressed => suppressed;
    public bool IsEnabled => enableDemoCasting && !suppressed;

    public void Initialize(DaoSystem daoSystem, QiComponent qiComponent, CultivationSystem cultivationSystem,
        MeditationController meditationController, bool allowCasting, bool allowDamage)
    {
        dao = daoSystem;
        qi = qiComponent;
        cultivation = cultivationSystem;
        meditation = meditationController;
        enableDemoCasting = allowCasting;
        dealDamage = allowDamage;
        CheckExternalSystems();
    }

    public TechniqueData GetSlot(int index)
    {
        return index >= 0 && index < SlotCount ? slots[index] : null;
    }

    public float GetCooldown(int index)
    {
        return index >= 0 && index < SlotCount ? cooldowns[index] : 0f;
    }

    public int Assign(TechniqueData technique, int slotIndex = -1)
    {
        if (technique == null) return -1;

        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == technique)
            {
                OnSlotChanged?.Invoke(i, technique);
                return i;
            }
        }

        int index = slotIndex;
        if (index < 0 || index >= SlotCount) index = FindFreeSlot();
        if (index < 0) index = 0;

        slots[index] = technique;
        cooldowns[index] = 0f;
        OnSlotChanged?.Invoke(index, technique);
        return index;
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= SlotCount) return;
        slots[index] = null;
        cooldowns[index] = 0f;
        OnSlotChanged?.Invoke(index, null);
    }

    public bool TryCast(int index)
    {
        if (!IsEnabled) return false;
        if (index < 0 || index >= SlotCount) return false;

        TechniqueData technique = slots[index];
        if (technique == null)
        {
            Message("Слот " + (index + 1) + " пуст — назначь технику в списке (Техники)");
            return false;
        }

        if (technique.Kind == TechniqueKind.Passive)
        {
            Message(technique.DisplayName + " — пассивная техника, действует постоянно");
            return false;
        }

        if (cooldowns[index] > 0f)
        {
            Message("Перезарядка: " + cooldowns[index].ToString("0.0") + " с");
            return false;
        }

        if (dao != null && !dao.IsTechniqueAvailable(technique))
        {
            Message("Недоступно: нужно Дао " + technique.RequiredDaoLevel + " ур. и реалм " + (technique.RequiredRealmIndex + 1));
            return false;
        }

        if (qi != null && !qi.HasQi(technique.QiCost))
        {
            Message("Не хватает Ци: нужно " + technique.QiCost);
            return false;
        }

        if (qi != null) qi.Spend(technique.QiCost);
        cooldowns[index] = technique.Cooldown;

        if (meditation != null && meditation.IsMeditating)
            meditation.Interrupt();

        SpawnVfx(technique);
        ApplyDamage(technique);

        OnCast?.Invoke(index, technique);
        Message(technique.DisplayName + (technique.Power > 0 ? " · сила " + technique.Power : string.Empty));
        return true;
    }

    private void Update()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (cooldowns[i] > 0f)
                cooldowns[i] = Mathf.Max(0f, cooldowns[i] - Time.deltaTime);
        }

        checkTimer -= Time.deltaTime;
        if (checkTimer <= 0f)
        {
            checkTimer = 2f;
            CheckExternalSystems();
        }

        if (!IsEnabled) return;
        if (CultivationUIRoot.Instance != null && CultivationUIRoot.Instance.IsInputBlocked) return;

        for (int i = 0; i < SlotCount; i++)
        {
            if (CultivationInput.Pressed(Key.Digit1 + i, KeyCode.Alpha1 + i) ||
                CultivationInput.Pressed(Key.Numpad1 + i, KeyCode.Keypad1 + i))
            {
                TryCast(i);
                return;
            }
        }
    }

    private void CheckExternalSystems()
    {
        if (suppressed) return;

        var behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null) continue;

            string typeName = behaviours[i].GetType().Name;
            if (typeName == "AbilitySystem" || typeName == "WeaponManager")
            {
                suppressed = true;
                Message("Найден " + typeName + " — демо-каст отключён, техники ведёт Промт B.");
                return;
            }
        }
    }

    private int FindFreeSlot()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == null) return i;
        }
        return -1;
    }

    private void SpawnVfx(TechniqueData technique)
    {
        Vector3 point = transform.position + Vector3.up * 1f + transform.forward * casterReach;

        if (technique.VfxPrefab != null)
        {
            var instance = Instantiate(technique.VfxPrefab, point, Quaternion.identity);
            Destroy(instance, 5f);
            return;
        }

        CultivationVfxFactory.CreateCastBurst(point, ResolveColor(technique.DaoType));
    }

    private void ApplyDamage(TechniqueData technique)
    {
        if (!dealDamage || technique.Power <= 0) return;

        var colliders = Physics.OverlapSphere(transform.position + Vector3.up * 0.9f, damageRadius);
        var visited = new HashSet<GameObject>();
        GameObject selfRoot = transform.root.gameObject;

        for (int i = 0; i < colliders.Length; i++)
        {
            var col = colliders[i];
            if (col == null) continue;

            // Идём от коллайдера вверх: враг может быть в контейнере, а HP — на его корне.
            Transform current = col.transform;
            for (int depth = 0; depth < 4 && current != null; depth++)
            {
                if (current.gameObject == selfRoot) break;
                if (!visited.Add(current.gameObject)) break;

                // В проекте встречаются оба написания: BossController.TakeDamamge и PlayerHpController.TakeDammage.
                current.gameObject.SendMessage("TakeDamamge", technique.Power, SendMessageOptions.DontRequireReceiver);
                current.gameObject.SendMessage("TakeDammage", technique.Power, SendMessageOptions.DontRequireReceiver);
                current = current.parent;
            }
        }
    }

    private Color ResolveColor(DaoType type)
    {
        if (dao != null)
        {
            var path = dao.GetDaoPath(type);
            if (path != null) return path.Color;
        }
        return DaoTypeInfo.GetColor(type);
    }

    private void Message(string text)
    {
        OnMessage?.Invoke(text);
        Debug.Log("[Cultivation] " + text);
    }
}
