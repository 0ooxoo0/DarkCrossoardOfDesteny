
using System.Collections.Generic;
using UnityEngine;

public class PointGenerateData : MonoBehaviour
{
    [SerializeField] List<PointGenerateData> array;
    public pointPos _PointPos;
    private int StenaNumber = 0;
void Awake()
{
        if(Random.Range(0,5) == Random.Range(0, 5))
        {
            GameObject obj = Instantiate(transform.GetComponentInParent<GenerateData>().RoomList.Stop[Random.Range(0, transform.GetComponentInParent<GenerateData>().RoomList.Stop.Count)], transform);
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
        if (other.transform.parent != transform.parent)
        {
            if (other.tag == "Stena" && transform.tag == "Untaggeed")
            {
                Debug.Log("Position:" + transform.position +" ThisName:" + transform.name + "name:" + other.name + "tag: " + other.tag);
                if (other.transform.parent != transform.parent)
                {
                    StenaNumber++;
                }
                if (other.transform.parent = transform.parent)
                {
                    if (StenaNumber>0)
                        Destroy(gameObject);

                    if (other.transform.parent != transform.parent)
                        Destroy(other.gameObject);
                }
                Destroy(gameObject);
            }
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.tag == "Untagged")
        {
            GameObject obj = Instantiate(transform.GetComponentInParent<GenerateData>().RoomList.Stop[Random.Range(0, transform.GetComponentInParent<GenerateData>().RoomList.Stop.Count)], transform);
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
