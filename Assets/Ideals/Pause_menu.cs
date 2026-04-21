using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class Pause_menu : MonoBehaviour
{
    public GameObject camera;
    public GameObject camm;
    public TextMeshProUGUI Points;
    public GameObject Button;
    public GameObject Settings;
    public GameObject SettingsGraphic;
    public GameObject SettingsVolume;
    public GameObject Naviki;
    public GameObject Karta;
    public AudioMixer audioMixerButton;
    public AudioMixer audioMixerOcrujenie;
    public AudioMixer audioMixerFiht;
    public AudioMixer audioMixerMove;
    public GameObject ClickMusic;
    public GameObject PauseMenu;
    public static bool active = false;
    public bool actives = false;
    public float time;
    void Start()
    {
        
    }

    void Update()
    {
        if (PauseMenu.activeInHierarchy)
        {}
        else
        {
            if (Input.GetKeyDown(KeyCode.LeftAlt))
            {
                //camm.GetComponent<CinemachineFreeLook>().m_YAxis.m_MaxSpeed = 0;
                //camm.GetComponent<CinemachineFreeLook>().m_XAxis.m_MaxSpeed = 0;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Input.GetKeyUp(KeyCode.LeftAlt))
            {
                //camm.GetComponent<CinemachineFreeLook>().m_YAxis.m_MaxSpeed = 2;
                //camm.GetComponent<CinemachineFreeLook>().m_XAxis.m_MaxSpeed = 500;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        //Points.text = "" + LevelUp.LevlPoints;
        actives = active;
        if (time>0)
        {
            time -= Time.deltaTime;
        }
        if(time<0)
        {
            time = 0;
        }
        if (active == false && Input.GetKeyDown(KeyCode.Escape) && time == 0)
        {
            PauseMenu.SetActive(true);
            Button.SetActive(true);
            Settings.SetActive(false);
            SettingsGraphic.SetActive(false);
            SettingsVolume.SetActive(false);
            Naviki.SetActive(false);
            Karta.SetActive(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            active = true;
            time = 0.1f;
        }
        if(active == true && Input.GetKeyDown(KeyCode.Escape) && time == 0)
        {
            if (Settings.active == false && SettingsGraphic.active == false && SettingsVolume.active == false && Naviki.active == false && Karta.active == false)
            {
                PauseMenu.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                active = false;
                time = 0.1f;
                return;
            }
            if(SettingsGraphic.active == true)
            {
                SettingsGraphic.SetActive(false);
                return;
            }
            if(SettingsVolume.active == true)
            {
                SettingsVolume.SetActive(false);
                return;
            }
            if (Settings.active == true)
            {
                Settings.SetActive(false);
                return;
            }
            if (Naviki.active == true)
            {
                Naviki.SetActive(false);
                return;
            }
            if (Karta.active == true)
            {
                Karta.SetActive(false);
                return;
            }
        }
    }

    public void Click()
    {
        Instantiate(ClickMusic, gameObject.transform);
    }

    public void NextScene(int SceneNumber)
    {
        SceneManager.LoadScene(SceneNumber);
    }

    public void SetVolumeButton(float volume)
    {
        audioMixerButton.SetFloat("Кнопки", Mathf.Log10(volume) * 20);
    }    
    public void SetVolumeFith(float volume)
    {
        audioMixerFiht.SetFloat("Удары", Mathf.Log10(volume) * 20);
    }    
    public void SetVolumeMove(float volume)
    {
        audioMixerMove.SetFloat("Шаги", Mathf.Log10(volume) * 20);
    }    
    public void SetVolumeOcrujenie(float volume)
    {
        audioMixerOcrujenie.SetFloat("Окружение", Mathf.Log10(volume) * 20);
    }
    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);
    }

    public void Sound()
    {
        AudioListener.pause = !AudioListener.pause;
    }

    public void Active()
    {
        PauseMenu.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        active = false;
        time = 0.1f;
    }
}
