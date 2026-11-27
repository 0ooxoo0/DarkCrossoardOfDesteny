using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Пять слотов способностей: ввод 1-5, кулдауны, melee через штатную атаку
/// оружия и магия через временную trigger-зону с тегом DammageObject.
/// </summary>
public class AbilitySystem : MonoBehaviour
{
    public const int SlotCount = 5;

    public event Action OnAbilitiesChanged;
    public event Action<int, AbilityData> OnAbilityActivated;
    /// <summary>(слот, способность, причина) — способность не запущена из-за Ци/Дао/реалма/оружия.</summary>
    public event Action<int, AbilityData, string> OnAbilityActivationBlocked;

    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private List<AbilityData> globalAbilities = new List<AbilityData>();

    [Header("Cultivation")]
    [SerializeField] private QiComponent qi;
    [SerializeField] private DaoSystem dao;
    [SerializeField] private CultivationSystem cultivation;

    [Header("Прицел")]
    [Tooltip("Слои, по которым ищется точка прицела. Trigger-коллайдеры игнорируются всегда.")]
    [SerializeField] private LayerMask aimMask = ~0;
    [Tooltip("Высота точки вылета над корнем персонажа.")]
    [SerializeField, Min(0f)] private float aimHeight = 1.2f;
    [Tooltip("Боевые звуки каста. Пусто — ищется на игроке.")]
    [SerializeField] private CombatAudio combatAudio;

    private float damageMultiplier = 1f;

    /// <summary>
    /// Единственный множитель урона способностей. Пассивные скиллы пишут сюда,
    /// потребители урона умножают силу на это значение.
    /// </summary>
    public float DamageMultiplier
    {
        get => damageMultiplier;
        set => damageMultiplier = Mathf.Max(0f, value);
    }

    private readonly List<AbilityData> techniqueAbilities = new List<AbilityData>();

    private static readonly Key[] SlotKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5
    };

    private readonly AbilityData[] abilities = new AbilityData[SlotCount];
    private readonly float[] cooldownEnd = new float[SlotCount];
    private readonly List<AbilityData> weaponAbilities = new List<AbilityData>();

    private UiManager uiManager;
    private Coroutine activeAbility;

    private void Awake()
    {
        if (weaponManager == null) weaponManager = GetComponent<WeaponManager>();
        if (playerController == null) playerController = GetComponent<PlayerController>();
        EnsureCultivationRefs();
        uiManager = FindFirstObjectByType<UiManager>();
        RebuildSlots();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (Time.timeScale <= 0f) return;
        if (uiManager != null && uiManager.IsOpen) return;
        // Панель культивации/техник перекрывает ввод 1–5.
        if (CultivationUIRoot.Instance != null && CultivationUIRoot.Instance.IsInputBlocked) return;

        for (int i = 0; i < SlotCount; i++)
        {
            if (Keyboard.current[SlotKeys[i]].wasPressedThisFrame)
                TryActivate(i);
        }
    }

    public void SetAbilities(List<AbilityData> list)
    {
        weaponAbilities.Clear();
        if (list != null) weaponAbilities.AddRange(list);
        RebuildSlots();
    }

    public void SetGlobalAbilities(List<AbilityData> list)
    {
        globalAbilities.Clear();
        if (list != null) globalAbilities.AddRange(list);
        RebuildSlots();
    }

    /// <summary>Техники Дао. Хранятся отдельно, чтобы не затирать globalAbilities (магию оружия).</summary>
    public void SetTechniqueAbilities(List<AbilityData> list)
    {
        techniqueAbilities.Clear();
        if (list != null) techniqueAbilities.AddRange(list);
        RebuildSlots();
    }

    private void RebuildSlots()
    {
        AbilityData[] next = new AbilityData[SlotCount];

        // Базовые атаки оружия — слоты 1–3.
        int weaponCount = Mathf.Min(3, weaponAbilities.Count);
        for (int i = 0; i < weaponCount; i++)
            next[i] = weaponAbilities[i];

        if (globalAbilities != null)
        {
            foreach (AbilityData ability in globalAbilities)
            {
                if (ability == null) continue;

                int slot = HotkeyToSlot(ability.hotkey);
                if (slot < 0) slot = FirstFreeSlot(next);
                if (slot >= 0) next[slot] = ability;
            }
        }

        // Техники Дао — приоритетно слоты 4–5 (индексы 3–4), поверх магии.
        for (int i = 0; i < techniqueAbilities.Count; i++)
        {
            AbilityData ability = techniqueAbilities[i];
            if (ability == null) continue;

            int slot = HotkeyToSlot(ability.hotkey);
            if (slot < 3 || slot >= SlotCount)
                slot = FirstFreeSlot(next, 3, SlotCount - 1);
            if (slot >= 0) next[slot] = ability;
        }

        AbilityData[] oldAbilities = (AbilityData[])abilities.Clone();
        float[] oldCooldown = (float[])cooldownEnd.Clone();

        for (int i = 0; i < SlotCount; i++)
        {
            abilities[i] = next[i];
            int oldIndex = IndexOf(oldAbilities, next[i]);
            cooldownEnd[i] = oldIndex >= 0 ? oldCooldown[oldIndex] : 0f;
        }

        OnAbilitiesChanged?.Invoke();
    }

    private static int HotkeyToSlot(KeyCode hotkey)
    {
        int index = (int)hotkey - (int)KeyCode.Alpha1;
        return index >= 0 && index < SlotCount ? index : -1;
    }

    private static int FirstFreeSlot(AbilityData[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == null) return i;
        }
        return -1;
    }

    private static int FirstFreeSlot(AbilityData[] array, int start, int end)
    {
        int from = Mathf.Max(0, start);
        int to = Mathf.Min(array.Length - 1, end);
        for (int i = from; i <= to; i++)
        {
            if (array[i] == null) return i;
        }
        return -1;
    }

    private static int IndexOf(AbilityData[] array, AbilityData value)
    {
        if (value == null) return -1;

        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == value) return i;
        }
        return -1;
    }

    public bool ActivateAbility(int index) => TryActivate(index);

    public AbilityData GetAbility(int index)
    {
        return index >= 0 && index < SlotCount ? abilities[index] : null;
    }

    public float GetCooldownRemaining(int index)
    {
        if (index < 0 || index >= SlotCount) return 0f;
        return Mathf.Max(0f, cooldownEnd[index] - Time.time);
    }

    public float GetCooldownFraction(int index)
    {
        AbilityData ability = GetAbility(index);
        if (ability == null || ability.cooldown <= 0.0001f) return 0f;
        return Mathf.Clamp01(GetCooldownRemaining(index) / ability.cooldown);
    }

    public bool IsAbilityAvailable(int index)
    {
        return CanActivateAbility(index, out _);
    }

    /// <summary>Полная проверка перед кастом: слот, КД, оружие, Дао, реалм и Ци.</summary>
    public bool CanActivateAbility(int index, out string reason)
    {
        reason = string.Empty;

        if (index < 0 || index >= SlotCount)
        {
            reason = "Неверный слот";
            return false;
        }

        AbilityData ability = GetAbility(index);
        if (ability == null)
        {
            reason = "Пустой слот";
            return false;
        }

        if (activeAbility != null)
        {
            reason = "Уже идёт другой каст";
            return false;
        }

        if (Time.time < cooldownEnd[index])
        {
            reason = "Перезарядка";
            return false;
        }

        if (!IsWeaponCompatible(ability))
        {
            reason = "Не подходит оружие";
            return false;
        }

        if (playerController != null && playerController.IsAttacking)
        {
            reason = "Идёт атака";
            return false;
        }

        return ValidateCultivation(ability, out reason);
    }

    private bool ValidateCultivation(AbilityData ability, out string reason)
    {
        reason = string.Empty;
        EnsureCultivationRefs();

        if (ability.requiredDao.HasValue)
        {
            DaoType required = ability.requiredDao.Value;

            if (dao == null)
            {
                reason = "На игроке нет DaoSystem";
                return false;
            }

            if (!dao.IsDaoUnlocked(required))
            {
                reason = "Дао не открыт: " + DaoTypeInfo.GetDisplayName(required);
                return false;
            }

            if (!dao.ActiveDao.HasValue || dao.ActiveDao.Value != required)
            {
                reason = "Нужен активный Дао: " + DaoTypeInfo.GetDisplayName(required);
                return false;
            }

            int requiredLevel = Mathf.Max(0, ability.requiredDaoLevel);
            if (dao.GetLevel(required) < requiredLevel)
            {
                reason = "Нужен уровень Дао " + requiredLevel;
                return false;
            }
        }

        int requiredRealm = Mathf.Max(0, ability.requiredRealmIndex);
        if (requiredRealm > 0)
        {
            if (cultivation == null)
            {
                reason = "На игроке нет CultivationSystem";
                return false;
            }

            if (cultivation.CurrentRealmIndex < requiredRealm)
            {
                reason = "Нужен реалм " + (requiredRealm + 1);
                return false;
            }
        }

        int cost = Mathf.Max(0, ability.qiCost);
        if (cost > 0)
        {
            if (qi == null)
            {
                reason = "На игроке нет QiComponent";
                return false;
            }

            if (!qi.HasQi(cost))
            {
                reason = "Не хватает Ци: нужно " + cost;
                return false;
            }
        }

        return true;
    }

    private void EnsureCultivationRefs()
    {
        if (qi == null) qi = GetComponent<QiComponent>();
        if (dao == null) dao = GetComponent<DaoSystem>();
        if (cultivation == null) cultivation = GetComponent<CultivationSystem>();
    }

    public void CancelCurrentAbility()
    {
        if (activeAbility != null)
        {
            StopCoroutine(activeAbility);
            activeAbility = null;
        }

        if (weaponManager != null) weaponManager.SuppressHitbox = false;
        if (playerController != null) playerController.ResumeMovement();
    }

    private bool TryActivate(int index)
    {
        if (index < 0 || index >= SlotCount) return false;
        if (Time.timeScale <= 0f) return false;
        if (uiManager != null && uiManager.IsOpen) return false;

        AbilityData ability = GetAbility(index);
        if (ability == null) return false;

        if (!CanActivateAbility(index, out string reason))
        {
            OnAbilityActivationBlocked?.Invoke(index, ability, reason);
            return false;
        }

        int cost = Mathf.Max(0, ability.qiCost);
        if (cost > 0)
        {
            if (qi == null || !qi.Spend(cost))
            {
                OnAbilityActivationBlocked?.Invoke(index, ability, "Не хватает Ци: нужно " + cost);
                return false;
            }
        }

        cooldownEnd[index] = Time.time + ability.cooldown;
        OnAbilityActivated?.Invoke(index, ability);
        activeAbility = StartCoroutine(ExecuteAbility(index, ability));
        return true;
    }

    private IEnumerator ExecuteAbility(int index, AbilityData ability)
    {
        if (ability.type == AbilityType.Melee)
            yield return ExecuteMelee(index, ability);
        else
            yield return ExecuteMagic(index, ability);

        activeAbility = null;
    }

    /// <summary>
    /// Power > 0 — урон техники, Power = 0 (Block) — урон оружия.
    /// Сила учитывается только у техник Дао (слоты 4–5): способности оружия 1–3
    /// сохраняют своё поведение, даже если в их ассете появится power.
    /// </summary>
    private float? ResolveTechniqueDamage(AbilityData ability)
    {
        if (ability == null || ability.power <= 0) return null;
        if (!techniqueAbilities.Contains(ability)) return null;
        return ability.power * Mathf.Max(0f, DamageMultiplier);
    }

    /// <summary>Боевой звук каста: живёт на игроке, сцена ссылку не хранит.</summary>
    private CombatAudio ResolveCombatAudio()
    {
        if (combatAudio == null)
            combatAudio = playerController != null
                ? playerController.GetComponent<CombatAudio>()
                : GetComponent<CombatAudio>();
        return combatAudio;
    }

    private IEnumerator ExecuteMelee(int index, AbilityData ability)
    {
        // Melee переиспользует штатный триггер Attack и хитбокс оружия.
        bool started;
        float? damageOverride = ResolveTechniqueDamage(ability);
        if (weaponManager != null) weaponManager.SetDamageOverride(damageOverride);

        try
        {
            if (ability.castAnimation != null && weaponManager != null)
                started = weaponManager.PlayCastAnimation(ability.castAnimation);
            else if (playerController != null)
                started = playerController.PlayAttackAnimation();
            else
                started = false;

            if (!started)
            {
                // Каст не начался: КД не ставим и возвращаем Ци.
                cooldownEnd[index] = 0f;
                RefundQi(ability);
                yield break;
            }

            // Окно удара открывает Animation Event в конце клипа, поэтому ждём не саму
            // атаку, а фактическое срабатывание хитбокса: иначе override сбросится раньше
            // попадания и меч ударит базовым уроном оружия.
            float waitWindow = 1.5f;
            while (waitWindow > 0f && !(weaponManager != null && weaponManager.IsHitboxActive))
            {
                waitWindow -= Time.deltaTime;
                yield return null;
            }

            float waitClose = 5f;
            while (waitClose > 0f && weaponManager != null && weaponManager.IsHitboxActive)
            {
                waitClose -= Time.deltaTime;
                yield return null;
            }

            // Хитбокса нет вовсе (техника без оружия) — ждём хотя бы конец атаки.
            float waitAttack = 2f;
            while (waitAttack > 0f && playerController != null && playerController.IsAttacking)
            {
                waitAttack -= Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            if (weaponManager != null) weaponManager.SetDamageOverride(null);
        }
    }

    private IEnumerator ExecuteMagic(int index, AbilityData ability)
    {
        if (weaponManager != null) weaponManager.SuppressHitbox = true;
        if (playerController != null) playerController.StopMovement();

        CombatAudio audio = ResolveCombatAudio();
        if (audio != null) audio.PlayCast();

        bool started = true;
        if (ability.castAnimation != null && weaponManager != null)
            started = weaponManager.PlayCastAnimation(ability.castAnimation);

        if (!started)
        {
            if (weaponManager != null) weaponManager.SuppressHitbox = false;
            if (playerController != null) playerController.ResumeMovement();
            cooldownEnd[index] = 0f;
            RefundQi(ability);
            yield break;
        }

        yield return new WaitForSeconds(Mathf.Max(0f, ability.vfxDelay));

        // Точка считается после задержки: за 0.2 с игрок успевает довернуть камеру,
        // и снаряд должен уйти уже по новой цели. Не кэшируем между кастами.
        Transform caster = ResolveCaster();
        float range = Mathf.Max(1f, ability.magicRange);
        Vector3 aimPoint = AimResolver.Resolve(caster, range, aimMask, aimHeight, caster.forward);
        if (playerController != null) playerController.FaceTowards(aimPoint, 0.35f);

        SpawnVfx(ability, aimPoint);
        CreateMagicHitbox(ability, aimPoint);

        // Ждём окончания каста, чтобы Animator не остался в Attack.
        float timeout = ability.castAnimation != null
            ? ability.castAnimation.length / 1.25f + 1f
            : 0.5f;

        while (playerController != null && playerController.IsAttacking && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (weaponManager != null) weaponManager.SuppressHitbox = false;
        if (playerController != null) playerController.ResumeMovement();
    }

    /// <summary>Корень кастующего: игрок, иначе сам объект способностей.</summary>
    private Transform ResolveCaster()
    {
        return playerController != null ? playerController.transform : transform;
    }

    /// <summary>Точка вылета эффекта: на высоте прицела, чуть впереди персонажа.</summary>
    private Vector3 ResolveMuzzle(Transform caster, AbilityData ability)
    {
        return caster.position + Vector3.up * aimHeight + caster.forward * Mathf.Max(0f, ability.magicSpawnDistance);
    }

    private void SpawnVfx(AbilityData ability, Vector3 aimPoint)
    {
        if (ability.vfxPrefab == null) return;

        Transform caster = ResolveCaster();
        Vector3 spawn = ResolveMuzzle(caster, ability);
        Vector3 direction = aimPoint - spawn;
        Quaternion rotation = direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction)
            : caster.rotation;

        GameObject vfx = Instantiate(ability.vfxPrefab, spawn, rotation);
        if (ability.vfxLifetime > 0f) Destroy(vfx, ability.vfxLifetime);
    }

    private void CreateMagicHitbox(AbilityData ability, Vector3 aimPoint)
    {
        Transform caster = ResolveCaster();

        GameObject zone = new GameObject("MagicHitbox_Temp");
        zone.transform.position = aimPoint;
        // Тег остаётся для пропов: DestructibleObject реагирует на DammageObject.
        TrySetTag(zone, ability.PrimaryDamageTag);

        SphereCollider sphere = zone.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = Mathf.Max(0.01f, ability.magicRadius);

        // Враги получают урон через IDamageable/CombatHitbox, а не через тег.
        // Меле-техники оружия идут штатным хитбоксом, поэтому зона нужна только магии.
        if (ability.type != AbilityType.Magic)
        {
            Destroy(zone, Mathf.Max(0.05f, ability.magicHitboxLifetime));
            return;
        }

        // Триггер без Rigidbody не даёт OnTriggerEnter: добавляем kinematic-тело.
        Rigidbody body = zone.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        // Магию тоже можно блокировать: CombatHitbox спрашивает ICombatBlockable до урона.
        var hitbox = zone.AddComponent<CombatHitbox>();
        hitbox.Configure(ability.power * Mathf.Max(0f, DamageMultiplier), CombatTeam.Player, caster);
        hitbox.TargetLayers = ability.damageLayers;
        hitbox.SetActive(true);

        Destroy(zone, Mathf.Max(0.05f, ability.magicHitboxLifetime));
    }

    /// <summary>Возврат Ци, если каст не состоялся. КД при этом не ставится.</summary>
    private void RefundQi(AbilityData ability)
    {
        int cost = Mathf.Max(0, ability != null ? ability.qiCost : 0);
        if (cost > 0 && qi != null) qi.Restore(cost);
    }

    private bool IsWeaponCompatible(AbilityData ability)
    {
        if (ability.requiredWeapon == WeaponType.Any) return true;
        if (weaponManager == null || weaponManager.CurrentWeapon == null) return false;
        return weaponManager.CurrentWeapon.type == ability.requiredWeapon;
    }

    private static void TrySetTag(GameObject target, string tagName)
    {
        if (target == null || string.IsNullOrWhiteSpace(tagName)) return;

        try
        {
            target.tag = tagName;
        }
        catch (UnityException)
        {
            Debug.LogWarning("[AbilitySystem] Тег не объявлен в Tag Manager: " + tagName);
        }
    }
}
