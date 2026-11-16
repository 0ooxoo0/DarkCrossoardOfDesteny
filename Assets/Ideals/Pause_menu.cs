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
        // Игра управляется мышью: курсор всегда свободный и видимый,
        // иначе после первого Esc наведение на лут перестаёт работать.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
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
            active = true;
            time = 0.1f;
        }
        if (active == true && Input.GetKeyDown(KeyCode.Escape) && time == 0)
        {
            if (!Settings.activeSelf && !SettingsGraphic.activeSelf && !SettingsVolume.activeSelf && !Naviki.activeSelf && !Karta.activeSelf)
            {
                PauseMenu.SetActive(false);
                active = false;
                time = 0.1f;
                return;
            }
            if (SettingsGraphic.activeSelf)
            {
                SettingsGraphic.SetActive(false);
                return;
            }
            if (SettingsVolume.activeSelf)
            {
                SettingsVolume.SetActive(false);
                return;
            }
            if (Settings.activeSelf)
            {
                Settings.SetActive(false);
                return;
            }
            if (Naviki.activeSelf)
            {
                Naviki.SetActive(false);
                return;
            }
            if (Karta.activeSelf)
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
        audioMixerButton.SetFloat("������", Mathf.Log10(volume) * 20);
    }    
    public void SetVolumeFith(float volume)
    {
        audioMixerFiht.SetFloat("�����", Mathf.Log10(volume) * 20);
    }    
    public void SetVolumeMove(float volume)
    {
        audioMixerMove.SetFloat("����", Mathf.Log10(volume) * 20);
    }    
    public void SetVolumeOcrujenie(float volume)
    {
        audioMixerOcrujenie.SetFloat("���������", Mathf.Log10(volume) * 20);
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
        active = false;
        time = 0.1f;
    }
}
