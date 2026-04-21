using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Room : MonoBehaviour
{
    private GenerateData _Parent;
    public List<PointGenerateData> PointArray = new List<PointGenerateData>();
    private Coroutine myCoroutine;

    void Awake()
    {
        GenerateData parentGen = transform.GetComponentInParent<GenerateData>();
        if (parentGen != null && parentGen.RoomArray != null)
        {
            parentGen.RoomArray.Add(this);
            _Parent = parentGen;
        }
        else
        {
            Debug.LogError("Room: не удалось найти GenerateData или RoomArray не инициализирован", this);
        }
        //GetComponent<BoxCollider>().enabled = false;
    }
    void Start()
    {
        transform.GetComponent<BoxCollider>().isTrigger = true; 
        int removedCount = PointArray.RemoveAll(room => room == null);
        myCoroutine = StartCoroutine(InstanseCoroutine());
    }

    IEnumerator InstanseCoroutine()
    {
        yield return new WaitForEndOfFrame();
        int removedCount = PointArray.RemoveAll(room => room == null);
        for (int i=0; i<PointArray.Count; i++)
        {
            if(PointArray[i] == null)
                PointArray.RemoveAt(i);
            _Parent._Instanse(PointArray[i].transform, PointArray[i]._PointPos);
        }
        //yield return new WaitForSeconds(Random.Range(0f,2f));
        //GetComponent<BoxCollider>().enabled = true;
                yield return null;
    }

    private void OnDestroy()
    {
        StopCoroutine(myCoroutine);
    }
    private void OnDisable()
    {
        StopCoroutine(myCoroutine);
    }
    private void OnTriggerStay(Collider other)
    {
        if (other.tag == "Room" && transform.tag == "Room")
        {
        Debug.Log("ThisName:" + transform.name + " name:" + other.name + " tag: " + other.tag);
            //GameObject obj = Instantiate(transform.GetComponentInParent<GenerateData>().RoomList.Stop[Random.Range(0, transform.parent.GetComponentInParent<GenerateData>().RoomList.Stop.Count)], transform.parent.position, transform.parent.rotation);
            //obj.transform.parent = transform.parent;
            //obj.transform.localScale = Vector3.one;
            GenerateData parentGen = transform.GetComponentInParent<GenerateData>();
            if (parentGen != null && parentGen.RoomArray != null)
            {
                parentGen.RoomArray.Remove(this);
            }
            int i = 0;
            while (i < other.transform.childCount)
            {
                other.transform.GetChild(0).gameObject.SetActive(true);
                i++;
            }
            gameObject.SetActive(false);

                //Destroy(gameObject);
        }
    }

    public void ColliderTrue()
    {
        transform.GetComponent<BoxCollider>().isTrigger = true;
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
