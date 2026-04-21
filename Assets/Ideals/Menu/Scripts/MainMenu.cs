using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using UnityEngine.UI;
using UnityEngine.Audio;
using System;
using TMPro;

public class MainMenu : MonoBehaviour
{
    public AudioMixer audioMixer;
    public AudioMixer audioMixer1;
    public AudioMixer audioMixer2;
    public AudioMixer audioMixer3;
    public float VolumeVelume;
    public float VolumeVelume1;
    public float VolumeVelume2;
    public float VolumeVelume3;
    public Slider slider;
    public Slider slider1;
    public Slider slider2;
    public Slider slider3;
    public GameObject Settings;

    public string data;

    public void SetVolume(float volume)
    {
        audioMixer.SetFloat("Volume", Mathf.Log10(volume) * 20);
    }
    public void SetVolume1(float volume)
    {
        audioMixer1.SetFloat("Volume", Mathf.Log10(volume) * 20);
    }
    public void SetVolume2(float volume)
    {
        audioMixer2.SetFloat("Volume", Mathf.Log10(volume) * 20);
    }
    public void SetVolume3(float volume)
    {
        audioMixer3.SetFloat("Volume", Mathf.Log10(volume) * 20);
    }

    public void SetQuality(int QualityIndex)
    {
        QualitySettings.SetQualityLevel(QualityIndex);
    }

    public void QualityLoad(TMP_Dropdown DD)
    {
        DD.value = QualitySettings.GetQualityLevel();
    }

    public void Sound()
    {
        AudioListener.pause = !AudioListener.pause;
    }

    public void Stop()
    {
        Time.timeScale = 0;
    }

    public void Continie()
    {
        Time.timeScale = 1;
    }
    public void NextScene(int _sceneNumber)//Переменная отвечает за номер сцены
    {
        SceneManager.LoadScene(_sceneNumber);
    }
    public void QuitGame()
    {
        Application.Quit();
    }

    public void CollectInfo()
    {
        VolumeVelume = slider.value;
        VolumeVelume1 = slider1.value;
        VolumeVelume2 = slider2.value;
        VolumeVelume3 = slider3.value;
    }

    public void SetInfo()
    {
        slider.value = VolumeVelume;
        slider1.value = VolumeVelume1;
        slider2.value = VolumeVelume2;
        slider3.value = VolumeVelume3;
    }

    public void save()
    {
        CollectInfo();
        data = "" + VolumeVelume;
        File.WriteAllText(Application.persistentDataPath + "/SaveVolume" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt", data);
        data = "" + VolumeVelume1;
        File.WriteAllText(Application.persistentDataPath + "/SaveVolume1" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt", data);
        data = "" + VolumeVelume2;
        File.WriteAllText(Application.persistentDataPath + "/SaveVolume2" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt", data);
        data = "" + VolumeVelume3;
        File.WriteAllText(Application.persistentDataPath + "/SaveVolume3" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt", data);
    }
    public void load()
    {
        if (File.Exists(Application.persistentDataPath + "/SaveVolume" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt"))
        {
            data = File.ReadAllText(Application.persistentDataPath + "/SaveVolume" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt");
            VolumeVelume = float.Parse(data);
        }
        if (File.Exists(Application.persistentDataPath + "/SaveVolume1" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt"))
        {
            data = File.ReadAllText(Application.persistentDataPath + "/SaveVolume1" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt");
            VolumeVelume1 = float.Parse(data);
        }
        if (File.Exists(Application.persistentDataPath + "/SaveVolume2" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt"))
        {
            data = File.ReadAllText(Application.persistentDataPath + "/SaveVolume2" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt");
            VolumeVelume2 = float.Parse(data);
        }
        if (File.Exists(Application.persistentDataPath + "/SaveVolume3" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt"))
        {
            data = File.ReadAllText(Application.persistentDataPath + "/SaveVolume3" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt");
            VolumeVelume3 = float.Parse(data);
        }
        if (File.Exists(Application.persistentDataPath + "/SaveVolume" + ("" + 0 + PlayerPrefs.GetInt("SaveBlock")) + Application.version + ".txt") || File.Exists(Application.persistentDataPath + "/SaveVolume1" + Application.version + ".txt") || File.Exists(Application.persistentDataPath + "/SaveVolume2" + Application.version + ".txt") || File.Exists(Application.persistentDataPath + "/SaveVolume3" + Application.version + ".txt"))
        {
            SetInfo();
        }
        
    }
    void Start()
    {
        load();
        if (SceneManager.GetActiveScene().name != "Menu")
        gameObject.SetActive(false);
    }
    private void FixedUpdate()
    {
        //if(Settings.activeSelf == true)
        //{

        //    Debug.Log("11111111");
        //    //SetInfo();
        //}

        if(Time.timeScale == 1 && SceneManager.GetActiveScene().name != "Menu")
        {
            gameObject.SetActive(false);
        }
    }
}
