using UnityEngine;

/// <summary>
/// Устаревшая ветка урона по тегу: удаляет объект при касании коллайдера с тегом
/// DammageObject. Боевой урон идёт через <see cref="CombatHitbox"/> и <see cref="IDamageable"/>,
/// поэтому класс оставлен только для старых пропов.
/// </summary>
[System.Obsolete("Legacy: урон идёт через CombatHitbox/IDamageable. Не использовать в новых сценах.")]
public class DestructibleObject : MonoBehaviour
{
    public GameObject Destructeffect;
    public Color _color;

    private bool destroyedByGameplay;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(other.tag);
        if (other.CompareTag("DammageObject"))
        {
            destroyedByGameplay = true;
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        // Instantiate из OnDestroy при выходе из плеера роняет редактор:
        // владельца уже нет, а префаб грузится вслепую.
        if (!Application.isPlaying) return;
        if (!destroyedByGameplay || Destructeffect == null)
            return;

        GameObject obj = Instantiate(Destructeffect, transform.position, transform.rotation);
        if (obj != null && obj.TryGetComponent(out ParticleSystem particleSystem))
        {
            var main = particleSystem.main;
            main.startColor = _color;
        }
    }
}
