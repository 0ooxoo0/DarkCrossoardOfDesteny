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
        if (Chest)
        {
            if (Random.Range(0, 100) <= ChangeSpawn)
            {
                Instantiate(transform.GetComponentInParent<GenerateData>().Chest[Random.Range(0, transform.GetComponentInParent<GenerateData>().Chest.Count)], transform);
            }
            else
            {
                if (Random.Range(0, 100) <= ChangeSpawn)
                {
                    if (Random.Range(0, 100) <= ChangeSpawn)
                        Instantiate(transform.GetComponentInParent<GenerateData>().SpawnObjects[Random.Range(0, transform.GetComponentInParent<GenerateData>().SpawnObjects.Count)], transform);
                    else
                        Instantiate(transform.GetComponentInParent<GenerateData>().EnemySpawn[Random.Range(0, transform.GetComponentInParent<GenerateData>().EnemySpawn.Count)], transform);

                }
            }
        }
        else
        {
            if (Random.Range(0, 100) <= ChangeSpawn)
            {
                if (Random.Range(0, 100) <= ChangeSpawn)
                    Instantiate(transform.GetComponentInParent<GenerateData>().SpawnObjects[Random.Range(0, transform.GetComponentInParent<GenerateData>().SpawnObjects.Count)], transform);
                else
                    Instantiate(transform.GetComponentInParent<GenerateData>().EnemySpawn[Random.Range(0, transform.GetComponentInParent<GenerateData>().EnemySpawn.Count)], transform);
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
