using UnityEngine;

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
        if (!destroyedByGameplay || Destructeffect == null)
            return;

        GameObject obj = Instantiate(Destructeffect, transform.position, transform.rotation);
        if (obj != null && obj.TryGetComponent(out ParticleSystem particleSystem))
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.startColor = _color;
        }
    }
}
