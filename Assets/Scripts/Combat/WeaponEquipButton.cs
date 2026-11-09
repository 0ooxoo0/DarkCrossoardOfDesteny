using UnityEngine;

/// <summary>
/// Кнопка выбора оружия в гардеробе. Вызывает EquipWeapon у того же Garderob,
/// к которому привязаны существующие кнопки (BasicHero_M).
/// </summary>
public class WeaponEquipButton : MonoBehaviour
{
    [SerializeField] private Garderob garderob;
    [SerializeField] private WeaponData weapon;

    public void Equip()
    {
        if (weapon == null)
        {
            Debug.LogWarning("[WeaponEquipButton] Не назначено оружие.");
            return;
        }

        if (garderob == null)
            garderob = FindFirstObjectByType<Garderob>(FindObjectsInactive.Include);

        if (garderob == null)
        {
            Debug.LogWarning("[WeaponEquipButton] В сцене нет Garderob.");
            return;
        }

        garderob.EquipWeapon(weapon);
    }
}
