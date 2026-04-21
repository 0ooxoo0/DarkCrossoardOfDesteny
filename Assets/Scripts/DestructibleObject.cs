using UnityEngine;
using UnityEngine.Events;

public class DestructibleObject : MonoBehaviour
{
    public GameObject Destructeffect;
    public Color _color;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(other.tag);
        if(other.tag == "DammageObject")
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        GameObject obj = Instantiate<GameObject>(Destructeffect, transform.position, transform.rotation);
        obj.GetComponent<ParticleSystem>().startColor = _color;
    }
}
