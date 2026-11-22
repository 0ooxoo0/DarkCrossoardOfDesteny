using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Room : MonoBehaviour
{
    private GenerateData _Parent;
    public List<PointGenerateData> PointArray = new List<PointGenerateData>();
    private Coroutine myCoroutine;
    private bool coroutineCancelled;
    private bool isRegistered;
    private static int nextCreationOrder;
    private int creationOrder;
    private bool warnedMissingRoomTag;

    private const string RoomTag = "Room";

    private void Awake()
    {
        creationOrder = ++nextCreationOrder;
        GenerateData parentGen = transform.GetComponentInParent<GenerateData>();
        if (parentGen != null && parentGen.RoomArray != null)
        {
            _Parent = parentGen;
            RegisterRoom();
        }
        else
        {
            Debug.LogError(name + ": GenerateData parent or RoomArray is not configured.", this);
        }
    }

    private void OnEnable()
    {
        coroutineCancelled = false;
        if (_Parent == null)
            _Parent = GetComponentInParent<GenerateData>();
        RegisterRoom();
        StartPointProcessing();
    }

    private void Start()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            Debug.LogError(name + ": BoxCollider is missing.", this);
            return;
        }
        boxCollider.isTrigger = true;
        StartPointProcessing();
    }

    private void RegisterRoom()
    {
        if (isRegistered || _Parent == null || _Parent.RoomArray == null) return;
        if (!_Parent.RoomArray.Contains(this)) _Parent.RoomArray.Add(this);
        if (!isRegistered) GenerateData.RegisterActiveRoom();
        isRegistered = true;
    }

    private void UnregisterRoom()
    {
        if (!isRegistered) return;
        if (_Parent != null && _Parent.RoomArray != null)
            _Parent.RoomArray.Remove(this);
        GenerateData.UnregisterActiveRoom();
        isRegistered = false;
    }

    public void RegisterPoint(PointGenerateData point)
    {
        if (point == null || PointArray == null || PointArray.Contains(point)) return;
        PointArray.Add(point);
    }

    public void UnregisterPoint(PointGenerateData point)
    {
        if (PointArray != null && point != null) PointArray.Remove(point);
    }

    private void StartPointProcessing()
    {
        if (myCoroutine != null || coroutineCancelled) return;
        if (_Parent == null)
        {
            Debug.LogError(name + ": GenerateData parent is missing; points were not processed.", this);
            return;
        }
        _Parent.BeginGenerationWork();
        myCoroutine = StartCoroutine(InstanseCoroutine());
    }

    private IEnumerator InstanseCoroutine()
    {
        yield return new WaitForEndOfFrame();
        if (coroutineCancelled || this == null) yield break;

        // FIX: P3.2 — snapshot before recursive room creation; points added by new rooms cannot mutate this iteration.
        List<PointGenerateData> points = PointArray != null
            ? new List<PointGenerateData>(PointArray)
            : new List<PointGenerateData>();

        for (int i = points.Count - 1; i >= 0; i--)
        {
            if (coroutineCancelled || this == null) yield break;
            PointGenerateData point = points[i];
            if (point == null || !point.TryBeginProcessing()) continue;

            if (TrySpawnStopForCap(point))
                continue;

            _Parent._Instanse(point.transform, point._PointPos);
            point.EndProcessing();
            yield return null;
        }

        myCoroutine = null;
        if (_Parent != null) _Parent.EndGenerationWork();
    }

    private bool TrySpawnStopForCap(PointGenerateData point)
    {
        if (_Parent == null || _Parent.RoomArray == null || _Parent.RoomList == null)
            return false;
        if (_Parent.RoomArray.Count < _Parent.RoomLimit)
            return false;

        // FIX: P2.5 — create Stop from the room coroutine, and count it against the same cap.
        if (!_Parent.TrySpawnStop(point.transform))
            Debug.LogWarning(name + ": room cap reached but no Stop room could be spawned.", this);
        point.gameObject.SetActive(false);
        return true;
    }

    private void CancelCoroutine()
    {
        // FIX: P2.3/P3.2 — request cancellation cooperatively; Unity stops disabled/destroyed component coroutines.
        coroutineCancelled = true;
        if (myCoroutine != null && _Parent != null)
            _Parent.EndGenerationWork();
        myCoroutine = null;
    }

    private void OnDestroy()
    {
        CancelCoroutine();
        UnregisterRoom();
    }

    private void OnDisable()
    {
        CancelCoroutine();
        UnregisterRoom();
    }

    private void OnTriggerStay(Collider other)
    {
        if (other == null) return;

        bool thisIsRoom = CompareTag(RoomTag);
        Room otherRoom = other.GetComponent<Room>();
        if (otherRoom == null) otherRoom = other.GetComponentInParent<Room>();
        bool otherIsRoom = other.CompareTag(RoomTag) || otherRoom != null;

        // FIX: P4.5 — report a missing room tag once, without logging every physics stay frame.
        if ((!thisIsRoom || (otherRoom != null && !other.CompareTag(RoomTag))) && !warnedMissingRoomTag)
        {
            Debug.LogWarning(name + ": room overlap involves an object without the Room tag.", this);
            warnedMissingRoomTag = true;
        }
        if (!thisIsRoom || !otherIsRoom) return;
        if (otherRoom == null || otherRoom == this) return;

        // FIX: P2.2 — keep the room with more direct children; if tied, keep the older room.
        Room roomToDisable;
        if (transform.childCount != otherRoom.transform.childCount)
            roomToDisable = transform.childCount < otherRoom.transform.childCount ? this : otherRoom;
        else
            roomToDisable = creationOrder > otherRoom.creationOrder ? this : otherRoom;

        Room roomToKeep = roomToDisable == this ? otherRoom : this;
        // FIX: P2.1 ? activate every child of the kept room using the loop index, not child zero repeatedly.
        foreach (Transform child in roomToKeep.transform)
        {
            if (child.GetComponent<PointGenerateData>() != null) continue;
            child.gameObject.SetActive(true);
        }

        roomToDisable.UnregisterRoom();
        roomToDisable.gameObject.SetActive(false);
        roomToKeep._Parent?.NotifyGenerationProgress();
    }

    public void ColliderTrue()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null) boxCollider.isTrigger = true;
    }
}

[System.Serializable]
public class RoomPos
{
    public List<GameObject> forward;
    public List<GameObject> down;
    public List<GameObject> left;
    public List<GameObject> right;
    public List<GameObject> Stop;
}
