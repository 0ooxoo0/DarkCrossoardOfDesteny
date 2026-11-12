using UnityEngine;

/// <summary>
/// Хитбокс оружия. Только переключает collider: урон наносит DestructibleObject
/// по тегу DammageObject. Для trigger-событий рядом обязателен kinematic Rigidbody.
/// </summary>
public class AttackHitbox : MonoBehaviour
{
    [SerializeField] private Collider hitboxCollider;

    public bool IsActive => hitboxCollider != null && hitboxCollider.enabled;

    private void Awake()
    {
        if (hitboxCollider == null) hitboxCollider = GetComponent<Collider>();
    }

    public void SetActive(bool active)
    {
        if (hitboxCollider == null) hitboxCollider = GetComponent<Collider>();
        if (hitboxCollider != null) hitboxCollider.enabled = active;
    }
}
