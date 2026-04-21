using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AddRoom : MonoBehaviour
{
    [Header("Walls (optional)")]
    public GameObject[] walls;
    public GameObject wallEffect;

    private RoomVariants variants;

    private void Awake()
    {
        variants = GameObject.FindGameObjectWithTag("Rooms")?.GetComponent<RoomVariants>();
    }

    private void Start()
    {
        if (variants != null)
            variants.rooms.Add(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && walls.Length > 0)
        {
            foreach (var wall in walls)
                if (wall != null) wall.SetActive(true);
        }
    }

    public void DestroyWalls()
    {
        foreach (GameObject wall in walls)
        {
            if (wall != null && wallEffect != null)
            {
                Instantiate(wallEffect, wall.transform.position, Quaternion.identity);
                Destroy(wall);
            }
        }
    }
}