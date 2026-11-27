using UnityEngine;

/// <summary>
/// Временная зона магического урона: trigger-сфера с CombatHitbox.
/// Общий код для мгновенных кастов, снарядов, лучей и аур —
/// блокировка, дедупликация и проверка препятствий уже внутри CombatHitbox.
/// </summary>
public static class MagicZone
{
    /// <summary>Создаёт зону и уничтожает её через lifetime. tag — для пропов (DammageObject).</summary>
    public static GameObject Spawn(Vector3 point, float radius, float damage, float lifetime,
        Transform owner, LayerMask targetLayers, string tag = null)
    {
        GameObject zone = new GameObject("MagicZone_Temp");
        zone.transform.position = point;

        if (!string.IsNullOrWhiteSpace(tag)) TrySetTag(zone, tag);

        SphereCollider sphere = zone.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = Mathf.Max(0.01f, radius);

        // Триггер без Rigidbody не даёт OnTriggerEnter.
        Rigidbody body = zone.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        var hitbox = zone.AddComponent<CombatHitbox>();
        hitbox.Configure(damage, CombatTeam.Player, owner);
        hitbox.TargetLayers = targetLayers;
        hitbox.SetActive(true);

        Object.Destroy(zone, Mathf.Max(0.05f, lifetime));
        return zone;
    }

    private static void TrySetTag(GameObject target, string tagName)
    {
        try
        {
            target.tag = tagName;
        }
        catch (UnityException)
        {
            Debug.LogWarning("[MagicZone] Тег не объявлен в Tag Manager: " + tagName);
        }
    }
}
