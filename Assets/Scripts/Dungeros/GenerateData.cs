using System.Collections;
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

    public void _Instanse(Transform pos, pointPos PosName)
    {
        if (RoomArray.Count > MaxRoom)
        {
            if (nav != null)
                nav.BuildNavMesh(); 

                    Debug.Log("Forward");
            if (RoomList.forward != null && RoomList.forward.Count > 0)
            {
                GameObject obj = Instantiate(RoomList.Stop[Random.Range(0, RoomList.Stop.Count)], pos);
                obj.transform.parent = transform;
                obj.transform.localScale = Vector3.one;
            }

            if (RoomArray.Count > MaxRoom * 2)
                return;
        }
        if (RoomList.forward != null && RoomList.forward.Count > 0)
        {
            GameObject obj = Instantiate(RoomList.forward[Random.Range(0, RoomList.forward.Count)], pos);
            obj.transform.parent = transform;
            obj.transform.localScale = Vector3.one;
        }

            //switch (PosName)
            //{
            //    case pointPos.Forward:

            //        Debug.Log("Forward");
            //        if (RoomList.forward != null && RoomList.forward.Count > 0)
            //        {
            //            GameObject obj = Instantiate(RoomList.forward[Random.Range(0, RoomList.forward.Count)], pos);
            //            obj.transform.parent = transform;
            //            obj.transform.rotation = Quaternion.identity;
            //        }
            //        break;

            //    case pointPos.Down:
            //        Debug.Log("Down");
            //        if (RoomList.down != null && RoomList.down.Count > 0)
            //        { 
            //            GameObject obj = Instantiate(RoomList.down[Random.Range(0, RoomList.down.Count)], pos);
            //        obj.transform.parent = transform;
            //        obj.transform.rotation = Quaternion.identity;
            //}
            //break;

            //    case pointPos.Left:
            //        Debug.Log("Left");
            //        if (RoomList.left != null && RoomList.left.Count > 0) 
            //        {
            //        GameObject obj = Instantiate(RoomList.left[Random.Range(0, RoomList.left.Count)], pos);
            //    obj.transform.parent = transform;
            //    obj.transform.rotation = Quaternion.identity;
            //}
            //break;

            //    case pointPos.Right:
            //        Debug.Log("Right");
            //        if (RoomList.right != null && RoomList.right.Count > 0)
            //        {
            //        GameObject obj = Instantiate(RoomList.right[Random.Range(0, RoomList.right.Count)], pos);
            //    obj.transform.parent = transform;
            //    obj.transform.rotation = Quaternion.identity;
            //}

            //break;
            //}
        }
}

