using System.Collections;
using UnityEngine;

public class RoomSpawner : MonoBehaviour
{
    public Direction direction;
    public enum Direction { Up, Down, Left, Right, None }

    private RoomVariants variants;
    private bool spawned = false;

    private void Start()
    {
        variants = GameObject.FindGameObjectWithTag("Rooms")?.GetComponent<RoomVariants>();
        if (variants == null)
        {
            Debug.LogError("RoomVariants not found! Tag 'Rooms' required.");
            return;
        }
        Invoke(nameof(Spawn), 0.2f);
        Destroy(gameObject, 3f);
    }

    public void Spawn()
    {
        if (spawned || variants == null) return;

        GameObject roomPrefab = null;
        switch (direction)
        {
            case Direction.Up: roomPrefab = GetRandom(variants.upRooms); break;
            case Direction.Down: roomPrefab = GetRandom(variants.downRooms); break;
            case Direction.Right: roomPrefab = GetRandom(variants.rightRooms); break;
            case Direction.Left: roomPrefab = GetRandom(variants.leftRooms); break;
        }

        if (roomPrefab == null) return;

        // Вычисляем позицию спавна с учётом размера комнаты
        Vector3 spawnPosition = transform.position;
        RoomSize roomSize = roomPrefab.GetComponent<RoomSize>();

        if (roomSize != null)
        {
            switch (direction)
            {
                case Direction.Up: spawnPosition += Vector3.forward * roomSize.offsetUp; break;
                case Direction.Down: spawnPosition += Vector3.back * roomSize.offsetDown; break;
                case Direction.Right: spawnPosition += Vector3.right * roomSize.offsetRight; break;
                case Direction.Left: spawnPosition += Vector3.left * roomSize.offsetLeft; break;
            }
        }
        else
        {
            // Если компонент отсутствует, используем стандартное смещение 20
            float defaultOffset = 20f;
            switch (direction)
            {
                case Direction.Up: spawnPosition += Vector3.forward * defaultOffset; break;
                case Direction.Down: spawnPosition += Vector3.back * defaultOffset; break;
                case Direction.Right: spawnPosition += Vector3.right * defaultOffset; break;
                case Direction.Left: spawnPosition += Vector3.left * defaultOffset; break;
            }
            Debug.LogWarning($"Room {roomPrefab.name} has no RoomSize component. Using default offset {defaultOffset}");
        }

        GameObject newRoom = Instantiate(roomPrefab, spawnPosition, roomPrefab.transform.rotation);
        spawned = true;
        Debug.Log($"Spawned {roomPrefab.name} at {spawnPosition} (direction {direction})");
    }

    private GameObject GetRandom(GameObject[] array)
    {
        return (array != null && array.Length > 0) ? array[Random.Range(0, array.Length)] : null;
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("RoomPoint") && other.GetComponent<RoomSpawner>()?.spawned == true)
            Destroy(gameObject);
    }
}