using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

public class Garderob : MonoBehaviour
{
    //public List<Fase> _Fase = new List<Fase>();
    //public List<Body> _Body = new List<Body>();
    [SerializeField] private List<string> nameMassive;
    public List<GameObject> Eyebrows = new List<GameObject>();
    public List<GameObject> Eye = new List<GameObject>();
    public List<GameObject> Mouths = new List<GameObject>();
    public List<GameObject> M_BottomBody = new List<GameObject>();
    public List<GameObject> M_Head = new List<GameObject>();
    public List<GameObject> M_TopBody = new List<GameObject>();
    public List<GameObject> _FacialHair = new List<GameObject>();
    public List<GameObject> _Hair = new List<GameObject>();

    public List<List<GameObject>> _ArrayList = new List<List<GameObject>>();

    private void Start()
    {
        _ArrayList.Add(Eyebrows);
        _ArrayList.Add(Eye);
        _ArrayList.Add(Mouths);
        _ArrayList.Add(M_BottomBody);
        _ArrayList.Add(M_Head);
        _ArrayList.Add(M_TopBody);
        _ArrayList.Add(_FacialHair);
        _ArrayList.Add(_Hair);

        ActiveArray = _ArrayList[0];
        NextMassive(0);
    }

    public Material _Material;
    public List<Color> color;

    public List<GameObject> ActiveArray;
    int index=0;

    [SerializeField] private TextMeshProUGUI text;

    public void Next(int i)
    {
        if (ActiveArray[index] != null && index! < 0)
            ActiveArray[index].SetActive(false);
        index +=i;
        if (index >= ActiveArray.Count)
            index = 0;
        if (index < 0)
            index = ActiveArray.Count-1;

        if (ActiveArray[index] != null && index! < 0)
            ActiveArray[index].SetActive(true);
    }

    public void NextMassive(int i)
    {
            int _index = _ArrayList.IndexOf(ActiveArray);
        _index+=i;
        if (_index >= _ArrayList.Count)
            _index = 0;
        if (_index < 0)
            _index = _ArrayList.Count-1;

        if (_ArrayList[_index] != null && _index! < 0)
            ActiveArray = _ArrayList[_index];
        Debug.Log("NameMassive _index = "+_index);
        if (_ArrayList[_index] != null && _index!<0)
            text.text = nameMassive[_index];
    }
}

[System.Serializable]
    public class Fase
{
    List<GameObject> Eyebrows;
    List<GameObject> Eye;
    List<GameObject> Mouths;
}
[System.Serializable]
    public class Body
{
    List<GameObject> M_BottomBody;
    List<GameObject> M_Head;
    List<GameObject> M_TopBody;
}


