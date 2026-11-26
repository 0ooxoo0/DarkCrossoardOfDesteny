using System;
using UnityEngine;

/// <summary>Тип попадания: обычный удар, блок или добивание.</summary>
public enum ImpactKind
{
    Hit,
    Blocked,
    Killed
}

public struct CombatImpact
{
    public Vector3 Point;
    public float Damage;
    public GameObject Attacker;
    public Transform Victim;
    public ImpactKind Kind;
    public bool IsCritical;
}

public static class CombatEvents
{
    public static event System.Action<CombatImpact> Impact;

    public static void Report(CombatImpact impact)
    {
        if (Impact != null) Impact(impact);
    }
}
