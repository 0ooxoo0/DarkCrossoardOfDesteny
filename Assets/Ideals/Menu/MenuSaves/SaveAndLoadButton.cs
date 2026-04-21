using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.ComponentModel;

public class SaveAndLoadButton : MonoBehaviour
{
    public static int io = 1;
    public GameObject Scroll;
    public TextMeshProUGUI text;
    public Toggle Toggle;
    public int ii;
    // Start is called before the first frame update
    void Start()
    {
        Scroll = GameObject.Find("Content928");

        SaveNameSaveFalse();
        LoadSaveName();
        SaveSaveName();
        
    }

    // Update is called once per frame
    //void Update()
    //{
    //    if(ScrollViewAdapterrr.Activeee == true)
    //        SaveSaveName();
    //}

    public void SaveNameSaveTrue()
    {
        PlayerPrefs.SetInt("SaveBlock", int.Parse(text.text));
        PlayerPrefs.Save();
        Toggle.isOn= true;
        Scroll.GetComponent<ScrollViewAdapterrr>().Munys();
    }
    public void SaveNameSaveFalse()
    {
        if(text.text == "" + PlayerPrefs.GetInt("SaveBlock"))
        Toggle.isOn = true;
        if(text.text != "" + PlayerPrefs.GetInt("SaveBlock"))
        Toggle.isOn = false;
    }
    public void LoadNameSave()
    {
        if(PlayerPrefs.HasKey(text.text))
        {
            if(PlayerPrefs.GetInt(text.text) == 1)
            {
                Toggle.isOn = true;
                SaveSaveName();
            }
            if(PlayerPrefs.GetInt(text.text) == 0)
            {
                Toggle.isOn = false;
            }
        }
    }
    public void Munys()
    {
        if(PlayerPrefs.GetInt(text.text) > 0)
        PlayerPrefs.SetInt(text.text, PlayerPrefs.GetInt(text.text) - 1);
        LoadNameSave();
    }

    public void SaveSaveName()
    {
        int i = 1;
        string data = "";
        for(;;i++)
        {
            if (File.Exists(Application.persistentDataPath + "/SaveName" + i + "_" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt"))
            {
                data = File.ReadAllText(Application.persistentDataPath + "/SaveName" + i + "_" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt");
                if (data == text.text)
                {
                    ii = i;
                    Debug.Log("1" + "  " + i + "!!!!!!!!!!!!");
                    return;
                }

                Debug.Log("1" + "  " + i + " | "+data+"!="+text.text) ;
            }
            else
            {
                    Debug.Log("2" + " | "+i);
                File.ReadAllText(Application.persistentDataPath + "/SaveName" + i + "_" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt");
                    ii = i;
                return;
            }
        }
        //string data = "";
        //for (int b=0; ;b++)
        //{
        //    //Load(data, b);
        //    if(data != text.text || data == "")
        //        Save(text.text, b);

        //    if (PlayerPrefs.HasKey(b + "SaveName"))
        //    {
        //        if (PlayerPrefs.GetString(b + "SaveName") == text.text)
        //        {
        //            Debug.Log("!!" + b + " : " + PlayerPrefs.GetString(b + "SaveName"));
        //            return;
        //        }
        //    }
        //    else
        //    {
        //        Save(text.text, b);
        //        PlayerPrefs.SetString(b + "SaveName", text.text);
        //        PlayerPrefs.Save();
        //        Debug.Log(b + " : " + PlayerPrefs.GetString(b + "SaveName"));
        //        return;
        //    }
        //}
    }

    public void LoadSaveName()
    {
        int i = io;
        string data = "";
        for (; ; i++)
        {
            if (File.Exists(Application.persistentDataPath + "/SaveName" + i + "_" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt"))
            {
                data = File.ReadAllText(Application.persistentDataPath + "/SaveName" + i + "_" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt");
                text.text = data;
                Debug.Log("3" + data);
                io++;
                return; 
            }
            else
            {
                Debug.Log("4");
                ii = i;
                return;
            }
        }
        //string data = "";
        //if (text.text == "")
        //{
        //    for (int b = 0; ; b++)
        //    {

        //        Debug.Log(PlayerPrefs.GetString(b + "SaveName"));
        //        if (PlayerPrefs.HasKey(b + "SaveName"))
        //        {
        //            Load(data, b);
        //            Debug.Log(PlayerPrefs.GetString(b + "SaveName"));
        //            text.text = PlayerPrefs.GetString(b + "SaveName");
        //            PlayerPrefs.DeleteKey(b + "SaveName");
        //            return;
        //        }
        //        else
        //        {
        //            return;
        //        }
        //    }
        //}
    }

    public void DeliteSaveName()
    {
        File.Delete(Application.persistentDataPath + "/SaveName" + ii + "_" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt");
        Destroy(gameObject);
    }

    public void Save(string data, int Number)
    {
        File.WriteAllText(Application.persistentDataPath + "/SaveSaveMenu" + Number+ "__" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt", data);
    }

    public void Load(string data, int Number)
    {
        data = File.ReadAllText(Application.persistentDataPath + "/SaveSaveMenu" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt");
    }

    public void Delite(int Number)
    {
        File.Delete(Application.persistentDataPath + "/SaveSaveMenu" + Number + "__" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt");
    }
}
