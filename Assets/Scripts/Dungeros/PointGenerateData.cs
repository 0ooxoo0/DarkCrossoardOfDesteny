using UnityEngine;

public class PointGenerateData : MonoBehaviour
{
    private const string RoomTag = "Room";

    [Tooltip("Тег стены, закрывающей проход. Переопределяется в сценах подземелья.")]
    [SerializeField] private string wallTag = "Stena";

    public pointPos _PointPos;
    private int StenaNumber;
    private Room parentRoom;
    private bool isRegistered;
    private bool isProcessing;
    private static readonly System.Collections.Generic.HashSet<int> ProcessingPoints = new System.Collections.Generic.HashSet<int>();

    private void Awake()
    {
        parentRoom = GetComponentInParent<Room>();
    }

    private void OnEnable()
    {
        if (parentRoom == null)
            parentRoom = GetComponentInParent<Room>();

        if (parentRoom != null && !isRegistered)
        {
            parentRoom.RegisterPoint(this);
            isRegistered = true;
        }
    }

    private void OnDisable()
    {
        UnregisterPoint();
    }

    private void OnDestroy()
    {
        UnregisterPoint();
    }

    private void UnregisterPoint()
    {
        ProcessingPoints.Remove(GetInstanceID());
        isProcessing = false;
        if (!isRegistered) return;
        if (parentRoom != null) parentRoom.UnregisterPoint(this);
        isRegistered = false;
    }

    // FIX: P2.5 — Stop creation is deferred to Room's managed coroutine; never Instantiate from Awake.
    public bool TryBeginProcessing()
    {
        int instanceId = GetInstanceID();
        if (isProcessing || !isActiveAndEnabled || ProcessingPoints.Contains(instanceId)) return false;
        isProcessing = true;
        ProcessingPoints.Add(instanceId);
        return true;
    }

    public void EndProcessing()
    {
        isProcessing = false;
        ProcessingPoints.Remove(GetInstanceID());
    }

    // FIX: P1.2 — only a Room collider means the point left an opening; generic Untagged colliders are ignored.
    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;
        Room otherRoom = other.GetComponent<Room>();
        if (otherRoom == null) otherRoom = other.GetComponentInParent<Room>();
        if (otherRoom == null) return;

        // FIX: P1.2 — a point exiting a different room opening gets exactly one guarded Stop; the same room is ignored.
        if (otherRoom == parentRoom) return;
        if (other.isTrigger) return;
        SpawnStopAndDisable();
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleTriggerEnter(other);
    }

    /// <summary>
    /// Обработка входа в триггер. Вынесена из <c>OnTriggerEnter</c>, чтобы тесты
    /// вызывали ту же логику напрямую, без отправки сообщений и отражения.
    /// </summary>
    internal void HandleTriggerEnter(Collider other)
    {
        // FIX: P1.1 — accept the serialized wall tag and do not depend on the point's own tag.
        if (other == null || string.IsNullOrWhiteSpace(wallTag) || !other.CompareTag(wallTag))
            return;

        Room wallRoom = other.GetComponentInParent<Room>();
        bool hasSameParent = other.transform.parent == transform.parent;
        bool belongsToDifferentRoom = wallRoom != null && wallRoom != parentRoom;

        // FIX: P1.1 — a wall from this point's room closes the point; a foreign room wall also closes it to prevent corridors entering that room.
        if (!hasSameParent && !belongsToDifferentRoom)
            return;

        StenaNumber++;
        if (StenaNumber > 0)
            DisablePoint();
    }

    // FIX: P1.2 — shared, guarded Stop placement replaces duplicate Awake/trigger spawn code.
    public void SpawnStopAndDisable()
    {
        if (!TryBeginProcessing()) return;

        GenerateData generateData = GetComponentInParent<GenerateData>();
        if (generateData == null)
        {
            Debug.LogWarning(name + ": Stop room could not be spawned; point remains available.", this);
            EndProcessing();
            return;
        }

        if (!generateData.TrySpawnStop(transform))
        {
            if (generateData.RoomArray != null && generateData.RoomArray.Count > generateData.RoomLimit)
                DisablePoint();
            else
            {
                Debug.LogWarning(name + ": Stop room could not be spawned; point remains available.", this);
                EndProcessing();
            }
            return;
        }

        DisablePoint();
    }

    private void DisablePoint()
    {
        UnregisterPoint();
        isProcessing = false;
        gameObject.SetActive(false);
    }
}

public enum pointPos
{
    Forward,
    Down,
    Left,
    Right
}
