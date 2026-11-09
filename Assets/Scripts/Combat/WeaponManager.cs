using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Экипировка оружия: AnimatorOverrideController, модель в правой руке,
/// активный хитбокс и передача способностей в AbilitySystem.
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
    [SerializeField] private RuntimeAnimatorController baseController;
    [SerializeField] private AnimationClip baseAttackClip;

    [Header("Sword")]
    [SerializeField] private Transform rightHand;

    [Header("Start")]
    [SerializeField] private WeaponData startingWeapon;

    public WeaponData CurrentWeapon => currentWeapon;

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
    private AttackHitbox swordHitbox;
    private RuntimeAnimatorController currentWeaponController;
    private AnimatorOverrideController castOverrideInstance;
    private Coroutine castRestoreRoutine;
    private bool suppressHitbox;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();

        if (baseController == null) baseController = animator != null ? animator.runtimeAnimatorController : null;
        if (baseAttackClip == null) baseAttackClip = FindClipByName(baseController, "PunchRight");
        if (rightHand == null && animator != null) rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (fistWeapon == null) fistWeapon = FindFistWeapon();

        EnsureFistRigidbody();
    }

    private void Start()
    {
        garderobs = FindObjectsByType<Garderob>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Garderob garderob in garderobs)
        {
            if (garderob != null) garderob.OnWeaponChanged += EquipWeapon;
        }

        if (startingWeapon != null) EquipWeapon(startingWeapon);
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

        // В сцене два Garderob (BasicHero_M и игрок). Оба шлют событие —
        // повторная экипировка того же оружия не должна сбрасывать Animator.
        if (currentWeapon == weapon) return;

        if (castRestoreRoutine != null)
        {
            StopCoroutine(castRestoreRoutine);
            castRestoreRoutine = null;
        }

        RestoreWeaponController();
        DestroyCastOverride();

        if (abilitySystem != null) abilitySystem.CancelCurrentAbility();

        currentWeapon = weapon;

        RuntimeAnimatorController controller = weapon.animatorOverride != null
            ? (RuntimeAnimatorController)weapon.animatorOverride
            : baseController;

        if (animator != null && controller != null)
            animator.runtimeAnimatorController = controller;

        currentWeaponController = controller;
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
            }
        }

        if (abilitySystem != null) abilitySystem.SetAbilities(weapon.abilities);

        ApplyHitbox(false);
        if (playerController != null) playerController.ResetHitboxState();
    }

    /// <summary>
    /// fromAnimationEvent = true только из AnimationEventRelay. Флаг
    /// useAnimationEvents решает, какой источник управляет хитбоксом.
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

    public void DisableHitbox()
    {
        SetHitboxActive(false, true);
    }

    /// <summary>
    /// Проигрывает клип каста в состоянии Attack через временный override,
    /// затем возвращает контроллер текущего оружия.
    /// </summary>
    public bool PlayCastAnimation(AnimationClip clip)
    {
        if (clip == null || animator == null || playerController == null) return false;

        if (baseAttackClip == null) baseAttackClip = FindClipByName(baseController, "PunchRight");
        if (baseAttackClip == null)
        {
            Debug.LogWarning("[WeaponManager] В базовом контроллере нет клипа PunchRight.");
            return false;
        }

        if (castRestoreRoutine != null)
        {
            StopCoroutine(castRestoreRoutine);
            castRestoreRoutine = null;
        }

        DestroyCastOverride();

        AnimatorOverrideController castOverride = new AnimatorOverrideController(baseController);

        // Сохраняем overrides текущего оружия (стойка/бег), но Attack заменяем кастом.
        if (currentWeaponController is AnimatorOverrideController weaponOverride)
        {
            List<KeyValuePair<AnimationClip, AnimationClip>> overrides =
                new List<KeyValuePair<AnimationClip, AnimationClip>>();
            weaponOverride.GetOverrides(overrides);

            foreach (KeyValuePair<AnimationClip, AnimationClip> pair in overrides)
            {
                if (pair.Key != null && pair.Value != null && pair.Key != baseAttackClip)
                    castOverride[pair.Key] = pair.Value;
            }
        }

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

        RuntimeAnimatorController controller = currentWeaponController != null
            ? currentWeaponController
            : baseController;

        if (controller != null) animator.runtimeAnimatorController = controller;
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

        if (fistWeapon != null) fistWeapon.SetHitboxActive(!swordActive && active);
        if (swordHitbox != null) swordHitbox.SetActive(swordActive && active);
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

        // Коллайдеры модели не должны участвовать в физике — урон наносит
        // отдельный trigger-хитбокс на клинке.
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

        AttackHitbox hitbox = hitboxObject.AddComponent<AttackHitbox>();
        hitbox.SetActive(false);
        swordHitbox = hitbox;
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

        // Rigidbody нужен, чтобы trigger-хитбокс получал OnTriggerEnter.
        // Существующий Rigidbody (например, в Game.unity) не меняем.
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
