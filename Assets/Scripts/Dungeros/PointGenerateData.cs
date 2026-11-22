
using System.Collections.Generic;
using UnityEngine;

public class PointGenerateData : MonoBehaviour
{
    [SerializeField] List<PointGenerateData> array;
    public pointPos _PointPos;
    private int StenaNumber = 0;
void Awake()
{
        if (Random.Range(0,5) == Random.Range(0, 5))
        {
            GenerateData generateData = transform.GetComponentInParent<GenerateData>();
            if (generateData == null || generateData.RoomList == null || generateData.RoomList.Stop == null || generateData.RoomList.Stop.Count == 0)
            {
                Debug.LogError(name + ": RoomList.Stop is null or empty.", this);
                return;
            }
            GameObject obj = Instantiate(generateData.RoomList.Stop[Random.Range(0, generateData.RoomList.Stop.Count)], transform);
            obj.transform.parent = transform.parent;
            transform.GetComponentInParent<Room>().PointArray.Remove(GetComponent<PointGenerateData>());
            gameObject.SetActive(false);
            //Destroy(gameObject);
        }

}
    private void Start()
    {
        Room parentRoom = transform.parent.GetComponent<Room>();
        if (parentRoom != null && parentRoom.PointArray != null)
        {
            parentRoom.PointArray.Add(this);
        }
        else
        {
            Debug.LogError("PointGenerateData: не удалось найти Room или PointArray не инициализирован", this);
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Stena") || !CompareTag("Untaggeed"))
            return;

        bool hasSameParent = other.transform.parent == transform.parent;
        if (!hasSameParent)
            return;

        Debug.Log("Position:" + transform.position + " ThisName:" + transform.name + "name:" + other.name + "tag: " + other.tag);
        StenaNumber++;
        if (StenaNumber > 0)
            Destroy(gameObject);
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.tag == "Untagged")
        {
            GenerateData generateData = transform.GetComponentInParent<GenerateData>();
            if (generateData == null || generateData.RoomList == null || generateData.RoomList.Stop == null || generateData.RoomList.Stop.Count == 0)
            {
                Debug.LogError(name + ": RoomList.Stop is null or empty.", this);
                return;
            }
            GameObject obj = Instantiate(generateData.RoomList.Stop[Random.Range(0, generateData.RoomList.Stop.Count)], transform);
            obj.transform.parent = transform.parent;
            transform.GetComponentInParent<Room>().PointArray.Remove(GetComponent<PointGenerateData>());
            gameObject.SetActive(false);
        }
    }
}
public enum pointPos
{
    Forward,
    Down,
    Left,
    Right
}
