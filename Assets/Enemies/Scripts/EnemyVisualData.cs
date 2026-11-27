using UnityEngine;

/// <summary>
/// Visual identity of one enemy: model prefab, optional weapon and attachment tuning.
/// Catalogued by <see cref="EnemyController"/> and selected by <see cref="id"/>.
/// </summary>
[CreateAssetMenu(fileName = "EnemyVisual_", menuName = "Enemies/Enemy Visual Data", order = 0)]
public class EnemyVisualData : ScriptableObject
{
    [Header("Identity")]
    public int id;
    public string displayName;

    [Header("Models")]
    public GameObject characterPrefab;
    public GameObject weaponPrefab;

    [Header("Animator (optional; overrides the controller on the model)")]
    public RuntimeAnimatorController animatorController;

    [Header("Weapon attachment")]
    public string weaponBoneName = "Hand_R";
    public Vector3 weaponLocalPosition = Vector3.zero;
    public Vector3 weaponLocalEuler = Vector3.zero;
    public Vector3 weaponLocalScale = Vector3.one;
}
