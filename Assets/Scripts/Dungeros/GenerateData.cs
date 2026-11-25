using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

public class GenerateData : MonoBehaviour
{
    public List<Room> RoomArray = new List<Room>();
    [SerializeField] private int MaxRoom = 5;
    public RoomPos RoomList;
    [SerializeField] private NavMeshSurface nav;
    public List<GameObject> SpawnObjects;
    public List<GameObject> EnemySpawn;
    public List<GameObject> Chest;

    private const float NavMeshQuietPeriod = 0.5f;
    private float stableTime;
    private int lastRoomCount;
    private bool generationRequested;
    private bool navMeshBuilt;
    private bool warnedMissingNav;
    private int pendingGenerationWork;

    public static int ActiveRoomCount { get; private set; }
    private static int reservedRoomCount;
    internal int RoomLimit => Mathf.Max(1, MaxRoom);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveRoomCount()
    {
        ActiveRoomCount = 0;
        reservedRoomCount = 0;
    }

    internal static bool ReserveActiveRoom(int limit)
    {
        // FIX: P2.5 ? reserve capacity before Instantiate so parallel point coroutines cannot exceed MaxRoom.
        if (ActiveRoomCount + reservedRoomCount >= limit) return false;
        reservedRoomCount++;
        return true;
    }

    internal static void RegisterActiveRoom()
    {
        if (reservedRoomCount > 0) reservedRoomCount--;
        ActiveRoomCount++;
    }

    internal static void ReleaseRoomReservation()
    {
        if (reservedRoomCount > 0) reservedRoomCount--;
    }

    internal static void UnregisterActiveRoom()
    {
        ActiveRoomCount = Mathf.Max(0, ActiveRoomCount - 1);
    }

    private void Update()
    {
        if (!generationRequested || navMeshBuilt)
            return;
        if (pendingGenerationWork > 0)
        {
            stableTime = 0f;
            return;
        }

        int roomCount = RoomArray != null ? RoomArray.Count : 0;
        if (roomCount != lastRoomCount)
        {
            lastRoomCount = roomCount;
            stableTime = 0f;
            return;
        }

        stableTime += Time.deltaTime;
        if (stableTime >= NavMeshQuietPeriod)
            FinalizeGeneration();
    }

    public void _Instanse(Transform pos, pointPos PosName)
    {
        if (pos == null)
        {
            Debug.LogWarning(name + ": generation point is null; room spawn skipped.", this);
            return;
        }
        if (RoomArray == null)
        {
            Debug.LogError(name + ": RoomArray is null.", this);
            return;
        }
        if (RoomList == null)
        {
            Debug.LogError(name + ": RoomList is null.", this);
            return;
        }

        NotifyGenerationProgress();

        // FIX: P1.3 — place one Stop room at the room cap and return before trying a normal room.
        if (RoomArray.Count >= RoomLimit)
        {
            TrySpawnStop(pos);
            return;
        }

        List<GameObject> roomPool = GetPool(PosName);
        if (roomPool == null || roomPool.Count == 0)
        {
            Debug.LogWarning(name + ": room pool for " + PosName + " is empty; trying forward fallback.", this);
            roomPool = RoomList.forward;
        }

        if (roomPool == null || roomPool.Count == 0)
        {
            // Пул для этого направления пуст и запасной forward тоже: комната не появится,
            // это неисправимая ошибка конфигурации, а не предупреждение.
            Debug.LogError(name + ": RoomList.forward is null or empty.", this);
            return;
        }

        InstantiateRoom(roomPool, pos);
    }

    // FIX: P1.4 — map every point direction to its corresponding serialized pool.
    private List<GameObject> GetPool(pointPos direction)
    {
        switch (direction)
        {
            case pointPos.Down: return RoomList.down;
            case pointPos.Left: return RoomList.left;
            case pointPos.Right: return RoomList.right;
            case pointPos.Forward:
            default: return RoomList.forward;
        }
    }

    private void InstantiateRoom(List<GameObject> pool, Transform pos)
    {
        if (pos == null || pool == null || pool.Count == 0)
            return;

        GameObject prefab = null;
        for (int attempt = 0; attempt < pool.Count; attempt++)
        {
            GameObject candidate = pool[Random.Range(0, pool.Count)];
            if (candidate == null) continue;
            prefab = candidate;
            break;
        }
        if (prefab == null)
        {
            Debug.LogWarning(name + ": selected room pool has no valid prefab; spawn skipped.", this);
            return;
        }

        Room roomInPrefab = prefab.GetComponentInChildren<Room>(false);
        bool prefabContainsRoom = roomInPrefab != null;
                // FIX: P2.5 — count active rooms before every recursive room Instantiate, preventing cap overshoot across concurrent point coroutines.
        if (prefabContainsRoom && (RoomArray.Count >= RoomLimit || !ReserveActiveRoom(RoomLimit)))
        {
            Debug.LogWarning(name + ": active room cap reached; recursive room spawn skipped.", this);
            return;
        }

        // FIX: P4.3 — do not overwrite the prefab's authored scale.
        try
        {
            GameObject instance = Instantiate(prefab, pos.position, pos.rotation, transform);
            Room instanceRoom = instance.GetComponentInChildren<Room>(true);
            if (prefabContainsRoom && (instanceRoom == null || !RoomArray.Contains(instanceRoom)))
                ReleaseRoomReservation();
        }
        catch
        {
            if (prefabContainsRoom) ReleaseRoomReservation();
            throw;
        }
        NotifyGenerationProgress();
    }

    public bool TrySpawnStop(Transform pos)
    {
        if (pos == null || RoomList == null || RoomList.Stop == null || RoomList.Stop.Count == 0)
        {
            Debug.LogWarning(name + ": RoomList.Stop is null or empty; stop room was not spawned.", this);
            return false;
        }
        if (RoomArray != null && RoomArray.Count > RoomLimit)
        {
            Debug.LogWarning(name + ": limit exceeded; Stop room was not spawned.", this);
            return false;
        }

        int startIndex = Random.Range(0, RoomList.Stop.Count);
        for (int offset = 0; offset < RoomList.Stop.Count; offset++)
        {
            GameObject prefab = RoomList.Stop[(startIndex + offset) % RoomList.Stop.Count];
            if (prefab == null) continue;
            Room roomInPrefab = prefab.GetComponentInChildren<Room>(false);
            bool stopContainsRoom = roomInPrefab != null;
            int stopRoomLimit = RoomArray != null && RoomArray.Count == RoomLimit
                ? RoomLimit + 1
                : RoomLimit;
            if (stopContainsRoom && !ReserveActiveRoom(stopRoomLimit))
            {
                Debug.LogWarning(name + ": active room limit reached; Stop prefab contains a Room and was skipped.", this);
                return false;
            }
            // FIX: P1.1 — Stop.prefab is tagged Stena; keep it under the point's room so the same-parent wall test closes that point.
            try
            {
                GameObject instance = Instantiate(prefab, pos.position, pos.rotation, pos.parent);
                Room instanceRoom = instance.GetComponentInChildren<Room>(true);
                if (stopContainsRoom && (instanceRoom == null || RoomArray == null || !RoomArray.Contains(instanceRoom)))
                    ReleaseRoomReservation();
            }
            catch
            {
                if (stopContainsRoom) ReleaseRoomReservation();
                throw;
            }
            NotifyGenerationProgress();
            return true;
        }

        Debug.LogWarning(name + ": RoomList.Stop contains no valid prefabs.", this);
        return false;
    }

    public void BeginGenerationWork()
    {
        pendingGenerationWork++;
        NotifyGenerationProgress();
    }

    public void EndGenerationWork()
    {
        pendingGenerationWork = Mathf.Max(0, pendingGenerationWork - 1);
        NotifyGenerationProgress();
    }

    // Called by Room before processing points, including points that become Stop rooms.
    public void NotifyGenerationProgress()
    {
        generationRequested = true;
        navMeshBuilt = false;
        stableTime = 0f;
        lastRoomCount = RoomArray != null ? RoomArray.Count : 0;
    }

    // FIX: P3.1 — build navigation once after room count has remained stable.
    public void FinalizeGeneration()
    {
        if (!generationRequested || navMeshBuilt)
            return;

        if (nav != null)
            nav.BuildNavMesh();
        else if (!warnedMissingNav)
        {
            Debug.LogWarning(name + ": NavMeshSurface reference is not assigned; navigation was not rebuilt.", this);
            warnedMissingNav = true;
        }

        navMeshBuilt = true;
        generationRequested = false;
    }
}
