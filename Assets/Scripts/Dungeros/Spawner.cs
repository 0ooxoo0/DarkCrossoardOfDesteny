using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

public class Spawner : MonoBehaviour
{
    [SerializeField] private int ChangeSpawn = 50;
    [SerializeField] private bool Chest = false;
    [SerializeField] private int time = 5;
    private float i = 0;
    public void SpawnObjects()
    {
        GenerateData generateData = transform.GetComponentInParent<GenerateData>();
        if (generateData == null)
        {
            Debug.LogError(name + ": GenerateData parent is missing.", this);
            enabled = false;
            return;
        }
        if (Chest && (generateData.Chest == null || generateData.Chest.Count == 0))
        {
            Debug.LogError(generateData.name + ": Chest list is null or empty.", generateData);
            enabled = false;
            return;
        }
        if (generateData.SpawnObjects == null || generateData.SpawnObjects.Count == 0)
        {
            Debug.LogError(generateData.name + ": SpawnObjects list is null or empty.", generateData);
            enabled = false;
            return;
        }
        if (generateData.EnemySpawn == null || generateData.EnemySpawn.Count == 0)
        {
            Debug.LogError(generateData.name + ": EnemySpawn list is null or empty.", generateData);
            enabled = false;
            return;
        }
        if (Chest)
        {
            if (Random.Range(0, 100) <= ChangeSpawn)
            {
                Instantiate(generateData.Chest[Random.Range(0, generateData.Chest.Count)], transform);
            }
            else
            {
                if (Random.Range(0, 100) <= ChangeSpawn)
                {
                    if (Random.Range(0, 100) <= ChangeSpawn)
                        Instantiate(generateData.SpawnObjects[Random.Range(0, generateData.SpawnObjects.Count)], transform);
                    else
                        Instantiate(generateData.EnemySpawn[Random.Range(0, generateData.EnemySpawn.Count)], transform);

                }
            }
        }
        else
        {
            if (Random.Range(0, 100) <= ChangeSpawn)
            {
                if (Random.Range(0, 100) <= ChangeSpawn)
                    Instantiate(generateData.SpawnObjects[Random.Range(0, generateData.SpawnObjects.Count)], transform);
                else
                    Instantiate(generateData.EnemySpawn[Random.Range(0, generateData.EnemySpawn.Count)], transform);
            }
        }
        enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (i <= time)
        {
            i += Time.deltaTime;
        }
        else
            SpawnObjects();
    }
}
