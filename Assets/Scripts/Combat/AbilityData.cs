using System.Collections.Generic;
using UnityEngine;

public enum AbilityType
{
    Melee,
    Magic
}

[CreateAssetMenu(menuName = "Combat/Ability Data", fileName = "NewAbility")]
public class AbilityData : ScriptableObject
{
    [Header("Identity")]
    public string abilityId;
    public string displayName;
    public Sprite icon;
    public KeyCode hotkey = KeyCode.Alpha1;

    [Header("Behaviour")]
    public AbilityType type;
    public float cooldown = 1f;
    public GameObject vfxPrefab;
    public AnimationClip castAnimation;
    public WeaponType requiredWeapon = WeaponType.Any;

    [Header("Damage")]
    [Tooltip("Сейчас используется только первый тег (по умолчанию DammageObject). Список оставлен для расширения.")]
    public List<string> damageTags = new List<string> { "DammageObject" };
    public LayerMask damageLayers = ~0;

    [Header("Magic")]
    public float magicRange = 2.5f;
    public float magicRadius = 2f;
    public float magicSpawnDistance = 1.5f;
    public float vfxDelay = 0.2f;
    public float vfxLifetime = 2f;
    public float magicHitboxLifetime = 0.2f;

    // Первый непустой тег списка. Поведение сейчас прежнее: только DammageObject.
    public string PrimaryDamageTag
    {
        get
        {
            if (damageTags != null)
            {
                for (int i = 0; i < damageTags.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(damageTags[i]))
                        return damageTags[i];
                }
            }

            return "DammageObject";
        }
    }
}
