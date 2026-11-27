using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Экипировка оружия: модель в правой руке, активный хитбокс и передача способностей.
/// Один Animator Controller используется и для меча, и для кулаков;
/// переключение делает параметр WeaponMode (0 = кулаки, 1 = меч).
/// </summary>
public class WeaponManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private AbilitySystem abilitySystem;

    [Header("Fists")]
    [SerializeField] private Weapon fistWeapon;

    [Header("Animation")]
    [Tooltip("Базовый контроллер. Если пусто — берётся текущий контроллер Animator.")]
    [SerializeField] private RuntimeAnimatorController baseController;
    [Tooltip("Клип, который временно заменяется клипом каста из AbilitySystem.")]
    [SerializeField] private AnimationClip baseAttackClip;
    [SerializeField] private string weaponModeParam = "WeaponMode";

    [Header("Sword")]
    [SerializeField] private Transform rightHand;

    [Header("Start")]
    [SerializeField] private WeaponData startingWeapon;

    public WeaponData CurrentWeapon => currentWeapon;

    public event Action<WeaponData> OnWeaponEquipped;

    public bool UseAnimationEvents => currentWeapon != null && currentWeapon.useAnimationEvents;

    public bool SuppressHitbox
    {
        get => suppressHitbox;
        set
        {
            suppressHitbox = value;
            if (suppressHitbox) ApplyHitbox(false);
        }
    }

    private Garderob[] garderobs;
    private WeaponData currentWeapon;
    private GameObject weaponModelInstance;
    private CombatHitbox swordHitbox;
    private CombatHitbox fistHitbox;
    private AnimatorOverrideController castOverrideInstance;
    private Coroutine castRestoreRoutine;
    private bool suppressHitbox;
    private float playerDamageMultiplier = 1f;
    private float? techniqueDamage;

    /// <summary>Сила текущей техники: переживает пересоздание модели оружия внутри каста.</summary>
    private float? TechniqueDamage => techniqueDamage;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();

        if (animator != null && animator.runtimeAnimatorController != null)
            baseController = animator.runtimeAnimatorController;

        if (baseAttackClip == null)
            baseAttackClip = FindClipByName(baseController, "Left Jab");
        if (baseAttackClip == null)
            baseAttackClip = FindClipByName(baseController, "Normal Attack 1");

        if (rightHand == null && animator != null)
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

        if (fistWeapon == null) fistWeapon = FindFistWeapon();
        if (fistWeapon != null)
        {
            fistHitbox = fistWeapon.GetComponent<CombatHitbox>();
            if (fistHitbox == null)
                fistHitbox = fistWeapon.gameObject.AddComponent<CombatHitbox>();
            fistHitbox.DamageMultiplier = playerDamageMultiplier;
        }

        EnsureFistRigidbody();
    }

    private void Start()
    {
        garderobs = FindObjectsByType<Garderob>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Garderob garderob in garderobs)
        {
            if (garderob != null) garderob.OnWeaponChanged += EquipWeapon;
        }

        if (startingWeapon == null)
        {
            startingWeapon = ScriptableObject.CreateInstance<WeaponData>();
            startingWeapon.type = WeaponType.Fists;
            startingWeapon.damage = 12f;
            startingWeapon.useAnimationEvents = true;
        }

        if (currentWeapon == null) EquipWeapon(startingWeapon);
    }

    private void OnDestroy()
    {
        if (garderobs != null)
        {
            foreach (Garderob garderob in garderobs)
            {
                if (garderob != null) garderob.OnWeaponChanged -= EquipWeapon;
            }
        }

        if (castRestoreRoutine != null)
        {
            StopCoroutine(castRestoreRoutine);
            castRestoreRoutine = null;
        }

        DestroyCastOverride();
        ClearWeaponModel();
    }

    public void EquipWeapon(WeaponData weapon)
    {
        if (weapon == null) return;
        if (currentWeapon == weapon) return;

        if (castRestoreRoutine != null)
        {
            StopCoroutine(castRestoreRoutine);
            castRestoreRoutine = null;
        }

        DestroyCastOverride();
        if (abilitySystem != null) abilitySystem.CancelCurrentAbility();

        currentWeapon = weapon;

        // Один контроллер на оба оружия: Animator сам выбирает ветку атак.
        if (animator != null)
            animator.SetInteger(weaponModeParam, weapon.type == WeaponType.Sword ? 1 : 0);

        ClearWeaponModel();

        if (weapon.type == WeaponType.Sword)
        {
            CreateSword(weapon);
            if (fistWeapon != null) fistWeapon.gameObject.SetActive(false);
        }
        else
        {
            if (fistWeapon != null)
            {
                fistWeapon.gameObject.SetActive(true);
                fistWeapon.SetHitboxActive(false);
                EnsureFistRigidbody();

                if (fistHitbox != null)
                {
                    fistHitbox.Configure(weapon.damage, CombatTeam.Player, transform);
                    fistHitbox.DamageMultiplier = playerDamageMultiplier;
                }
            }
        }

        if (abilitySystem != null) abilitySystem.SetAbilities(weapon.abilities);

        ApplyHitbox(false);
        if (playerController != null) playerController.ResetHitboxState();

        OnWeaponEquipped?.Invoke(weapon);
    }

    /// <summary>
    /// fromAnimationEvent = true только из PlayerController/AnimationEventRelay.
    /// Флаг useAnimationEvents решает, какой источник управляет хитбоксом.
    /// </summary>
    public void SetHitboxActive(bool active, bool fromAnimationEvent = false)
    {
        if (suppressHitbox)
        {
            ApplyHitbox(false);
            return;
        }

        if (currentWeapon != null && currentWeapon.useAnimationEvents != fromAnimationEvent)
            return;

        if (currentWeapon == null && fromAnimationEvent)
            return;

        ApplyHitbox(active);
    }

    public void EnableHitbox()
    {
        SetHitboxActive(true, true);
    }

    /// <summary>Временная замена урона активного хитбокса (сила техники). null — урон оружия.</summary>
    public void SetDamageOverride(float? value)
    {
        techniqueDamage = value;
        if (fistHitbox != null) fistHitbox.SetDamageOverride(value);
        if (swordHitbox != null) swordHitbox.SetDamageOverride(value);
    }

    /// <summary>Открыто ли окно удара: нужно технике, чтобы дождаться попадания.</summary>
    public bool IsHitboxActive =>
        (fistHitbox != null && fistHitbox.IsActive) || (swordHitbox != null && swordHitbox.IsActive);

    /// <summary>Множитель урона оружия от пассивов. Override техники не затрагивает.</summary>
    public float PlayerDamageMultiplier
    {
        get => playerDamageMultiplier;
        set
        {
            playerDamageMultiplier = Mathf.Max(0f, value);
            if (fistHitbox != null) fistHitbox.DamageMultiplier = playerDamageMultiplier;
            if (swordHitbox != null) swordHitbox.DamageMultiplier = playerDamageMultiplier;
        }
    }

    public void DisableHitbox()
    {
        SetHitboxActive(false, true);
    }

    /// <summary>
    /// Временно заменяет боевой клип клипом каста и запускает атаку.
    /// </summary>
    public bool PlayCastAnimation(AnimationClip clip)
    {
        if (clip == null || animator == null || playerController == null) return false;

        if (baseAttackClip == null)
            baseAttackClip = FindClipByName(baseController, "Left Jab");

        if (baseAttackClip == null)
        {
            Debug.LogWarning("[WeaponManager] В контроллере нет клипа Left Jab / Normal Attack 1 для каста.");
            return false;
        }

        if (castRestoreRoutine != null)
        {
            StopCoroutine(castRestoreRoutine);
            castRestoreRoutine = null;
        }

        DestroyCastOverride();

        AnimatorOverrideController castOverride = new AnimatorOverrideController(baseController);
        castOverride[baseAttackClip] = clip;
        castOverrideInstance = castOverride;
        animator.runtimeAnimatorController = castOverride;

        if (!playerController.PlayAttackAnimation())
        {
            RestoreWeaponController();
            DestroyCastOverride();
            return false;
        }

        castRestoreRoutine = StartCoroutine(RestoreAfterCast(clip.length / 1.25f + 0.15f));
        return true;
    }

    private IEnumerator RestoreAfterCast(float delay)
    {
        yield return new WaitForSeconds(delay);

        RestoreWeaponController();
        ApplyHitbox(false);

        if (playerController != null)
        {
            playerController.ResetHitboxState();
            playerController.ResumeMovement();
        }

        DestroyCastOverride();
        castRestoreRoutine = null;
    }

    private void RestoreWeaponController()
    {
        if (animator == null) return;
        if (baseController != null) animator.runtimeAnimatorController = baseController;
    }

    private void DestroyCastOverride()
    {
        if (castOverrideInstance == null) return;
        Destroy(castOverrideInstance);
        castOverrideInstance = null;
    }

    private void ApplyHitbox(bool active)
    {
        bool swordActive = currentWeapon != null && currentWeapon.type == WeaponType.Sword;

        if (fistWeapon != null)
        {
            bool fistActive = !swordActive && active;
            fistWeapon.SetHitboxActive(fistActive);
            if (fistHitbox != null) fistHitbox.SetActive(fistActive);
        }

        if (swordHitbox != null)
            swordHitbox.SetActive(swordActive && active);
    }

    private void CreateSword(WeaponData weapon)
    {
        if (weapon.weaponModelPrefab == null || rightHand == null)
        {
            Debug.LogWarning("[WeaponManager] Не задана модель меча или кость правой руки.");
            return;
        }

        weaponModelInstance = Instantiate(weapon.weaponModelPrefab, rightHand);
        weaponModelInstance.transform.localPosition = weapon.weaponLocalPosition;
        weaponModelInstance.transform.localEulerAngles = weapon.weaponLocalEulerAngles;
        weaponModelInstance.transform.localScale = weapon.weaponLocalScale;

        foreach (Collider modelCollider in weaponModelInstance.GetComponentsInChildren<Collider>(true))
            modelCollider.enabled = false;

        GameObject hitboxObject = new GameObject("Hitbox");
        hitboxObject.transform.SetParent(weaponModelInstance.transform, false);
        hitboxObject.transform.localPosition = weapon.hitboxLocalPosition;
        hitboxObject.transform.localEulerAngles = weapon.hitboxLocalEulerAngles;

        BoxCollider box = hitboxObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = weapon.hitboxSize;

        TrySetTag(hitboxObject, weapon.PrimaryDamageTag);

        Rigidbody body = hitboxObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        swordHitbox = hitboxObject.AddComponent<CombatHitbox>();
        swordHitbox.Configure(weapon.damage, CombatTeam.Player, transform);
        swordHitbox.DamageMultiplier = playerDamageMultiplier;
        if (TechniqueDamage.HasValue) swordHitbox.SetDamageOverride(TechniqueDamage);
        swordHitbox.SetActive(false);
    }

    private void ClearWeaponModel()
    {
        swordHitbox = null;
        if (weaponModelInstance == null) return;

        Destroy(weaponModelInstance);
        weaponModelInstance = null;
    }

    private void EnsureFistRigidbody()
    {
        if (fistWeapon == null) return;
        if (fistWeapon.GetComponent<Rigidbody>() != null) return;

        Rigidbody body = fistWeapon.gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private Weapon FindFistWeapon()
    {
        Weapon[] weapons = GetComponentsInChildren<Weapon>(true);
        Weapon fallback = null;

        foreach (Weapon candidate in weapons)
        {
            if (candidate == null) continue;
            if (fallback == null) fallback = candidate;
            if (candidate.CompareTag("DammageObject")) return candidate;
        }

        return fallback;
    }

    private static AnimationClip FindClipByName(RuntimeAnimatorController controller, string clipName)
    {
        if (controller == null) return null;

        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip != null && clip.name == clipName) return clip;
        }

        return null;
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
            Debug.LogWarning("[WeaponManager] Тег не объявлен в Tag Manager: " + tagName +
                             ". Хитбокс останется Untagged.");
        }
    }
}
