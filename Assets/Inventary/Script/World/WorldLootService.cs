using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Создаёт контейнеры лута в мире и сливает близкие выбросы в один.
/// Единственное место, где появляется <see cref="WorldLootContainer"/> из префаба.
/// </summary>
public class WorldLootService : MonoBehaviour
{
    [Header("Префаб")]
    [SerializeField] private GameObject containerPrefab;

    [Header("Размещение")]
    [Tooltip("На сколько вперёд от выбрасывающего появляется лут.")]
    [SerializeField] private float dropDistance = 1.5f;

    [Tooltip("Выбросы ближе этого радиуса сливаются в один контейнер.")]
    [SerializeField] private float mergeRadius = 2.5f;

    [Tooltip("С какой высоты искать землю под точкой выброса.")]
    [SerializeField] private float groundRayHeight = 5f;

    [Tooltip("Слои земли для поиска точки приземления.")]
    [SerializeField] private LayerMask groundMask = ~0;

    private readonly List<WorldLootContainer> spawned = new List<WorldLootContainer>();

    /// <summary>
    /// Выбрасывает предметы перед указанным трансформом.
    /// Возвращает false, если префаб не задан или предмет невалиден — вызывающий
    /// код в этом случае не должен удалять предмет из инвентаря.
    /// </summary>
    public bool TryDrop(ItemData item, int quantity, Transform dropper)
    {
        if (item == null || quantity <= 0 || dropper == null) return false;

        if (containerPrefab == null)
        {
            Debug.LogWarning("[WorldLootService] Не назначен containerPrefab — выброс невозможен.");
            return false;
        }

        Vector3 point = dropper.position + dropper.forward * dropDistance;
        point = SnapToGround(point);
        point = SnapToNavMesh(point);

        WorldLootContainer nearest = FindNearest(point, mergeRadius);
        if (nearest != null)
        {
            nearest.Add(item, quantity);
            return true;
        }

        WorldLootContainer container = SpawnContainer(point);
        if (container == null) return false;

        container.Add(item, quantity);
        return true;
    }

    /// <summary>Создаёт контейнер в точке и берёт его под наблюдение.</summary>
    public WorldLootContainer SpawnContainer(Vector3 worldPosition)
    {
        if (containerPrefab == null)
        {
            Debug.LogWarning("[WorldLootService] Не назначен containerPrefab — контейнер не создан.");
            return null;
        }

        GameObject instance = Instantiate(containerPrefab, worldPosition, Quaternion.identity, transform);
        WorldLootContainer container = instance.GetComponent<WorldLootContainer>();

        if (container == null)
        {
            // Префаб без компонента — ошибка настройки, объект в сцене только мешает.
            Debug.LogError("[WorldLootService] В префабе контейнера нет WorldLootContainer.");
            Destroy(instance);
            return null;
        }

        spawned.Add(container);
        container.OnEmptied += HandleEmptied;
        return container;
    }

    /// <summary>Ближайший непустой контейнер в радиусе или null.</summary>
    public WorldLootContainer FindNearest(Vector3 position, float radius)
    {
        // Список созданных в рантайме не переживает загрузку сцены, поэтому
        // ищем по сцене: так выброс рядом со стартовым контейнером тоже сливается.
        WorldLootContainer[] containers = FindObjectsByType<WorldLootContainer>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        WorldLootContainer nearest = null;
        float bestSqr = radius * radius;

        for (int i = 0; i < containers.Length; i++)
        {
            WorldLootContainer container = containers[i];
            if (container == null || container.IsEmpty) continue;

            float sqr = (container.transform.position - position).sqrMagnitude;
            if (sqr > bestSqr) continue;

            bestSqr = sqr;
            nearest = container;
        }

        return nearest;
    }

    private void HandleEmptied(WorldLootContainer container)
    {
        container.OnEmptied -= HandleEmptied;
        spawned.Remove(container);
    }

    /// <summary>Опускает точку на землю лучом сверху; без попадания оставляет как есть.</summary>
    private Vector3 SnapToGround(Vector3 point)
    {
        Vector3 origin = point + Vector3.up * groundRayHeight;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundRayHeight * 2f,
                groundMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return point;
    }

    /// <summary>Прижимает точку к навмешу, чтобы до лута можно было дойти.</summary>
    private Vector3 SnapToNavMesh(Vector3 point)
    {
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, mergeRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return point;
    }
}
