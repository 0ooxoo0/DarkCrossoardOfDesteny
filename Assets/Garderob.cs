using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

public class Garderob : MonoBehaviour
{
    // Единственные новые публичные члены: событие выбора оружия и его вызов.
    public event Action<WeaponData> OnWeaponChanged;

    public void EquipWeapon(WeaponData weapon)
    {
        OnWeaponChanged?.Invoke(weapon);
    }

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

        Debug.Log("1");
        if (ActiveArray[index] != null && index > -1)
            ActiveArray[index].SetActive(false);
        else
            Debug.Log("1.1");

        Debug.Log("2");
        Debug.Log("index " + index);
        index +=i;
        Debug.Log("index " + index);
        if (index >= ActiveArray.Count)
            index = 0;
        else
            Debug.Log("2.1");
        if (index < 0)
            index = ActiveArray.Count-1;
        else
            Debug.Log("3.1");

        Debug.Log("3");
        if (ActiveArray[index] != null && index > -1)
            ActiveArray[index].SetActive(true);
        else
            Debug.Log("4.1");
        Debug.Log("4");
    }

    public void NextMassive(int i)
    {
        Debug.Log("1");
        int _index = _ArrayList.IndexOf(ActiveArray);
        _index+=i;
        if (_index >= _ArrayList.Count)
            _index = 0;
        if (_index < 0)
            _index = _ArrayList.Count-1;

        Debug.Log("2"); 
        if (_ArrayList[_index] == null)
        {
            Debug.LogError("Masive Name ненайдено");
            Debug.Log("2.1");
        }
        else
            Debug.LogError("Masive Name найден");

        if (_index > 0)
        {
            Debug.LogError("index<0");
            Debug.Log("2.2");
        }
        else
            Debug.LogError("index>0");

        if (_ArrayList[_index] != null && _index > -1)
        {
            Debug.Log("3");
            ActiveArray = _ArrayList[_index];
            Debug.Log("4");
            text.text = nameMassive[_index];

            Debug.Log("5");
        }
        else
        {
            if (_ArrayList[_index] == null)
            {
                Debug.LogError("Masive Name ненайдено");
                Debug.Log("5.1");
            }
            if (_index! < 0)
            {
                Debug.LogError("index<0");
                Debug.Log("5.2");
            }

            Debug.Log("6");
        }
        Debug.Log("NameMassive _index = "+ nameMassive[_index] + " || " + _index);

        Debug.Log("7");
        Debug.Log("nameMassive: "+text.text +"||"+ nameMassive[_index]);

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


