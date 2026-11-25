using UnityEngine;

public class HandController : MonoBehaviour
{
    Animator anim;
    bool compress;
    public Vector3 offset;
    private void Start()
    {
        anim = GetComponent<Animator>();
        Cursor.visible = false;
    }

    void Update()
    {
        transform.position = Input.mousePosition + offset;
        if (Input.GetMouseButtonDown(0))
        {
            compress = true;
            anim.SetBool("Compression", true);
        }
        if(Input.GetMouseButton(0))
        {
        }
        else if (compress != false)
        {
            compress = false;
            anim.SetBool("Compression", false);
        }
        if (Input.GetMouseButtonUp(0) && compress != false)
        {
            compress = false;
            transform.position = Input.mousePosition;
            anim.SetBool("Compression", false);
        }
    }
}
