using System.Collections.Generic;
using UnityEngine;

public enum WeaponType
{
    Any,
    Fists,
    Sword
}

[CreateAssetMenu(menuName = "Combat/Weapon Data", fileName = "NewWeapon")]
public class WeaponData : ScriptableObject
{
    [Header("Identity")]
    public string weaponId;
    public string displayName;

    [Header("Animation")]
    public AnimatorOverrideController animatorOverride;
    public WeaponType type;

    [Tooltip("true — хитбокс переключают AnimationEvent; false — PlayerController по normalizedTime. Два источника одновременно не работают.")]
    public bool useAnimationEvents = true;

    [Header("Abilities")]
    public List<AbilityData> abilities = new List<AbilityData>();

    [Header("Damage")]
    [Tooltip("Сейчас используется только первый тег (по умолчанию DammageObject). Список оставлен для расширения.")]
    public List<string> damageTags = new List<string> { "DammageObject" };

    [Header("Visual")]
    public GameObject weaponModelPrefab;
    public Vector3 weaponLocalPosition;
    public Vector3 weaponLocalEulerAngles;
    public Vector3 weaponLocalScale = Vector3.one;

    [Header("Sword Hitbox")]
    public Vector3 hitboxLocalPosition;
    public Vector3 hitboxLocalEulerAngles;
    public Vector3 hitboxSize = new Vector3(0.15f, 0.15f, 1.2f);

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
