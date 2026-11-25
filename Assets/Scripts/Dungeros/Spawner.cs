using UnityEngine;
using UnityEngine.AI;

public class Spawner : MonoBehaviour
{
    [SerializeField] private int ChangeSpawn = 50;

    /// <summary>Шанс выбора ветки добычи. Тесты задают значение явно.</summary>
    internal int ChangeSpawnForTests
    {
        get => ChangeSpawn;
        set => ChangeSpawn = value;
    }

    [SerializeField] private bool Chest = false;
    [SerializeField] private int time = 5;
    private float i = 0;
    private int navAttempts;

    public void SpawnObjects()
    {
        GenerateData generateData = transform.GetComponentInParent<GenerateData>();
        if (generateData == null)
        {
            Debug.LogError(name + ": GenerateData parent is missing.", this);
            enabled = false;
            return;
        }

        // FIX: P4.2 — do not place runtime objects at spawn points outside the baked navigation surface.
        // FIX: P4.2 — wait for generation's single NavMesh bake rather than silently disabling every room spawner before it exists.
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
        {
            if (++navAttempts > 300)
            {
                Debug.LogError(name + ": NavMesh not available after 300 frames; spawner disabled.", this);
                enabled = false;
            }
            return;
        }

        navAttempts = 0;
        spawnPosition = navHit.position;
        GameObject prefabToSpawn = GetSelectedPrefab(generateData);
        if (prefabToSpawn == null)
        {
            enabled = false;
            return;
        }

        Instantiate(prefabToSpawn, spawnPosition, transform.rotation, transform);
        enabled = false;
    }

    private GameObject GetSelectedPrefab(GenerateData generateData)
    {
        return SelectPrefab(generateData);
    }

    /// <summary>
    /// Выбор префаба для спавна. Тесты вызывают ту же логику напрямую,
    /// без отражения и без запуска физического цикла обновления.
    /// </summary>
    internal GameObject GetSelectedPrefabForTests(GenerateData generateData)
    {
        return SelectPrefab(generateData);
    }

    private GameObject SelectPrefab(GenerateData generateData)
    {
        if (Chest)
        {
            if (Random.Range(0, 100) <= ChangeSpawn)
            {
                return GetRandomPrefab(generateData.Chest, generateData, "Chest");
            }
            else if (Random.Range(0, 100) <= ChangeSpawn)
            {
                // FIX: P4.1 — validate a spawn list only in the random branch that uses it.
                if (Random.Range(0, 100) <= ChangeSpawn)
                    return GetRandomPrefab(generateData.SpawnObjects, generateData, "SpawnObjects");
                else
                    return GetRandomPrefab(generateData.EnemySpawn, generateData, "EnemySpawn");
            }
        }
        else if (Random.Range(0, 100) <= ChangeSpawn)
        {
            // FIX: P4.1 — empty unused lists no longer disable this spawner.
            if (Random.Range(0, 100) <= ChangeSpawn)
                return GetRandomPrefab(generateData.SpawnObjects, generateData, "SpawnObjects");
            else
                return GetRandomPrefab(generateData.EnemySpawn, generateData, "EnemySpawn");
        }

        return null;
    }

    private GameObject GetRandomPrefab(System.Collections.Generic.List<GameObject> prefabs, GenerateData owner, string fieldName)
    {
        if (prefabs == null || prefabs.Count == 0)
        {
            Debug.LogError(owner.name + ": " + fieldName + " list is null or empty.", owner);
            return null;
        }

        GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
        if (prefab == null)
            Debug.LogError(owner.name + ": " + fieldName + " contains a null prefab.", owner);
        return prefab;
    }

    private Vector3 spawnPosition;

    private void Update()
    {
        if (i <= time)
        {
            i += Time.deltaTime;
        }
        else
        {
            SpawnObjects();
        }
    }
}
