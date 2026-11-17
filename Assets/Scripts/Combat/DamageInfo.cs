using UnityEngine;

/// <summary>
/// Описание попадания: сколько урона, от кого и куда.
/// Отдельная структура, чтобы здоровье и оружие не зависели друг от друга.
/// </summary>
public struct DamageInfo
{
    public float Amount;
    public GameObject Source;
    public Vector3 HitPoint;
    public bool IsCritical;

    public DamageInfo(float amount, GameObject source, Vector3 hitPoint, bool isCritical = false)
    {
        Amount = amount;
        Source = source;
        HitPoint = hitPoint;
        IsCritical = isCritical;
    }
}
