using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomVariants : MonoBehaviour
{
    [Header("Room Prefabs")]
    public GameObject[] upRooms;
    public GameObject[] downRooms;
    public GameObject[] rightRooms;
    public GameObject[] leftRooms;

    [Header("Chest (instead of boss)")]
    public GameObject chestPrefab;

    [Header("Generation Limits")]
    public int maxRooms = 10;
    public float generationDelay = 5f;

    [HideInInspector] public List<GameObject> rooms = new List<GameObject>();

    private void Start()
    {
        // Автоматически создаём спавнер, если его нет
        if (FindObjectsOfType<RoomSpawner>().Length == 0)
        {
            AddRoom startRoom = FindObjectOfType<AddRoom>();
            if (startRoom != null)
            {
                GameObject spawnerObj = new GameObject("AutoSpawner");
                spawnerObj.transform.position = startRoom.transform.position;
                RoomSpawner spawner = spawnerObj.AddComponent<RoomSpawner>();
                spawner.direction = RoomSpawner.Direction.Up; // можно сменить
                Debug.Log("Auto-created RoomSpawner at start room position");
            }
            else
            {
                Debug.LogError("No AddRoom found in scene. Please place a starting room with AddRoom component.");
            }
        }

        StartCoroutine(FinishGeneration());
    }

    private IEnumerator FinishGeneration()
    {
        yield return new WaitForSeconds(generationDelay);
        Debug.Log($"Generation finished. Rooms count: {rooms.Count}");

        if (rooms.Count >= maxRooms)
        {
            var spawners = FindObjectsOfType<RoomSpawner>();
            foreach (var spawner in spawners)
                Destroy(spawner.gameObject);
        }

        if (rooms.Count > 0 && chestPrefab != null)
        {
            int chestRoomIndex = Random.Range(0, rooms.Count);
            GameObject chestRoom = rooms[chestRoomIndex];
            Instantiate(chestPrefab, chestRoom.transform.position, Quaternion.identity);
            Debug.Log($"Chest spawned in room: {chestRoom.name}");
        }
        else
        {
            Debug.LogWarning($"Cannot spawn chest. Rooms: {rooms.Count}, chestPrefab: {chestPrefab != null}");
        }
    }
}