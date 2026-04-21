using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeRoom : MonoBehaviour
{
    public Vector3 CameraCangePose;
    public Vector3 PlayerChangePos;
    private Camera cam;

    void Start()
    {
        cam = Camera.main.GetComponent<Camera>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.position += PlayerChangePos;
            cam.transform.position += CameraCangePose;
        }
    }
}