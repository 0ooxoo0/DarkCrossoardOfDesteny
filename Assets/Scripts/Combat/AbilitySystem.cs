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

    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private PlayerController playerController;

    private static readonly Key[] SlotKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5
    };

    private readonly AbilityData[] abilities = new AbilityData[SlotCount];
    private readonly float[] cooldownEnd = new float[SlotCount];

    private UiManager uiManager;
    private Coroutine activeAbility;

    private void Awake()
    {
        if (weaponManager == null) weaponManager = GetComponent<WeaponManager>();
        if (playerController == null) playerController = GetComponent<PlayerController>();
        uiManager = FindFirstObjectByType<UiManager>();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (Time.timeScale <= 0f) return;
        if (uiManager != null && uiManager.IsOpen) return;

        for (int i = 0; i < SlotCount; i++)
        {
            if (Keyboard.current[SlotKeys[i]].wasPressedThisFrame)
                TryActivate(i);
        }
    }

    public void SetAbilities(List<AbilityData> list)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            abilities[i] = list != null && i < list.Count ? list[i] : null;
            cooldownEnd[i] = 0f;
        }

        OnAbilitiesChanged?.Invoke();
    }

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
        AbilityData ability = GetAbility(index);
        if (ability == null) return false;
        if (activeAbility != null) return false;
        if (Time.time < cooldownEnd[index]) return false;
        return IsWeaponCompatible(ability);
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
        AbilityData ability = GetAbility(index);
        if (ability == null) return false;
        if (activeAbility != null) return false;
        if (playerController != null && playerController.IsAttacking) return false;
        if (Time.time < cooldownEnd[index]) return false;
        if (!IsWeaponCompatible(ability)) return false;

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

    private IEnumerator ExecuteMelee(int index, AbilityData ability)
    {
        // Melee переиспользует штатный триггер Attack и хитбокс оружия.
        bool started;
        if (ability.castAnimation != null && weaponManager != null)
            started = weaponManager.PlayCastAnimation(ability.castAnimation);
        else if (playerController != null)
            started = playerController.PlayAttackAnimation();
        else
            started = false;

        if (!started)
        {
            cooldownEnd[index] = 0f;
            yield break;
        }

        float timeout = 5f;
        while (playerController != null && playerController.IsAttacking && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator ExecuteMagic(int index, AbilityData ability)
    {
        if (weaponManager != null) weaponManager.SuppressHitbox = true;
        if (playerController != null) playerController.StopMovement();

        if (ability.castAnimation != null && weaponManager != null)
            weaponManager.PlayCastAnimation(ability.castAnimation);

        yield return new WaitForSeconds(Mathf.Max(0f, ability.vfxDelay));

        SpawnVfx(ability);
        CreateMagicHitbox(ability);

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

    private void SpawnVfx(AbilityData ability)
    {
        if (ability.vfxPrefab == null) return;

        Transform origin = playerController != null ? playerController.transform : transform;
        Vector3 spawn = origin.position + origin.forward * ability.magicSpawnDistance;
        GameObject vfx = Instantiate(ability.vfxPrefab, spawn, origin.rotation);

        if (ability.vfxLifetime > 0f) Destroy(vfx, ability.vfxLifetime);
    }

    private void CreateMagicHitbox(AbilityData ability)
    {
        Transform origin = playerController != null ? playerController.transform : transform;
        Vector3 spawn = origin.position + origin.forward * ability.magicSpawnDistance;

        GameObject zone = new GameObject("MagicHitbox_Temp");
        zone.transform.position = spawn;
        TrySetTag(zone, ability.PrimaryDamageTag);

        SphereCollider sphere = zone.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = Mathf.Max(0.01f, ability.magicRadius);

        // Как и у меча: trigger без Rigidbody не даёт OnTriggerEnter.
        Rigidbody body = zone.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        // OverlapSphere — диагностика зоны; реальный урон делает
        // DestructibleObject.OnTriggerEnter по тегу.
        Collider[] hits = Physics.OverlapSphere(
            spawn,
            sphere.radius,
            ability.damageLayers,
            QueryTriggerInteraction.Ignore);

        if (hits.Length > 0)
        {
            Debug.Log("[AbilitySystem] Зона магии задела " + hits.Length + " коллайдер(ов).");
        }

        Destroy(zone, Mathf.Max(0.05f, ability.magicHitboxLifetime));
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
