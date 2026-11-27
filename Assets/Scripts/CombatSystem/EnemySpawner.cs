using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Простой спавнер врагов по точкам: держит в мире заданное число боевых врагов
/// и возвращает их после смерти. Тяжёлого сканирования сцены нет — точки задаются
/// в инспекторе, а живые враги считаются по своему же списку.
/// </summary>
[DisallowMultipleComponent]
public class EnemySpawner : MonoBehaviour
{
    [Header("Что и сколько")]
    [Tooltip("Префаб боевого врага.")]
    [SerializeField] private GameObject enemyPrefab;
    [Tooltip("Сколько врагов держать живыми одновременно.")]
    [SerializeField, Min(0)] private int maxAlive = 4;
    [Tooltip("Сколько всего врагов выпустить за сессию. 0 — без ограничения.")]
    [SerializeField, Min(0)] private int totalLimit = 0;

    [Header("Точки спавна")]
    [Tooltip("Точки появления. Пусто — используется позиция самого спавнера.")]
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("Радиус разброса вокруг выбранной точки.")]
    [SerializeField, Min(0f)] private float spawnRadius = 2f;

    [Header("Тайминги")]
    [Tooltip("Пауза между появлениями, секунд.")]
    [SerializeField, Min(0f)] private float spawnInterval = 8f;
    [Tooltip("Пауза после смерти врага до следующего появления, секунд.")]
    [SerializeField, Min(0f)] private float respawnDelay = 12f;

    private readonly List<GameObject> alive = new List<GameObject>();
    private float nextSpawnTime;
    private int spawnedTotal;
    private int nextPoint;

    /// <summary>Сколько врагов живо прямо сейчас.</summary>
    public int AliveCount => alive.Count;

    /// <summary>Сколько всего выпущено за сессию.</summary>
    public int SpawnedTotal => spawnedTotal;

    private void Start()
    {
        nextSpawnTime = Time.time + Mathf.Min(spawnInterval, 2f);
    }

    private void Update()
    {
        // Чистим умерших: список маленький, тяжёлых поисков по сцене нет.
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (alive[i] == null) alive.RemoveAt(i);
        }

        if (enemyPrefab == null) return;
        if (totalLimit > 0 && spawnedTotal >= totalLimit) return;
        if (alive.Count >= maxAlive) return;
        if (Time.time < nextSpawnTime) return;

        SpawnOne();
        nextSpawnTime = Time.time + (alive.Count == 0 ? respawnDelay : spawnInterval);
    }

    private void SpawnOne()
    {
        Vector3 point = ResolvePoint();
        GameObject enemy = Instantiate(enemyPrefab, point, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

        alive.Add(enemy);
        spawnedTotal++;
    }

    private Vector3 ResolvePoint()
    {
        Vector3 origin = transform.position;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform point = spawnPoints[nextPoint % spawnPoints.Length];
            nextPoint++;
            if (point != null) origin = point.position;
        }

        if (spawnRadius <= 0f) return origin;

        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        return origin + new Vector3(offset.x, 0f, offset.y);
    }
}
