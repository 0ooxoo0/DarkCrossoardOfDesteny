using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SoundItem
{
    public string name;
    public AudioClip clip;
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private List<SoundItem> soundList = new List<SoundItem>();

    private AudioSource audioSource;
    private Dictionary<string, AudioClip> soundDictionary = new Dictionary<string, AudioClip>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        InitializeSoundDictionary();
    }

    private void InitializeSoundDictionary()
    {
        soundDictionary.Clear();
        foreach (SoundItem sound in soundList)
        {
            if (sound != null && !string.IsNullOrEmpty(sound.name) && sound.clip != null)
            {
                if (!soundDictionary.ContainsKey(sound.name))
                {
                    soundDictionary.Add(sound.name, sound.clip);
                }
                else
                {
                    Debug.LogWarning($"Duplicate sound name found: {sound.name}");
                }
            }
        }
    }

    public void PlaySound(string name)
    {
        if (soundDictionary.ContainsKey(name))
        {
            audioSource.PlayOneShot(soundDictionary[name]);
            Debug.Log($"Playing sound: {name}");
        }
        else
        {
            Debug.LogWarning($"Sound not found: {name}");
        }
    }

    public void PlaySound(string name, float volume)
    {
        if (soundDictionary.ContainsKey(name))
        {
            audioSource.PlayOneShot(soundDictionary[name], volume);
            Debug.Log($"Playing sound: {name} with volume {volume}");
        }
        else
        {
            Debug.LogWarning($"Sound not found: {name}");
        }
    }

    public void StopSound()
    {
        audioSource.Stop();
    }

    public bool SoundExists(string name)
    {
        return soundDictionary.ContainsKey(name);
    }

    // Метод для добавления звука во время выполнения
    public void AddSound(string name, AudioClip clip)
    {
        if (string.IsNullOrEmpty(name) || clip == null)
        {
            Debug.LogError("Invalid sound data");
            return;
        }

        if (!soundDictionary.ContainsKey(name))
        {
            SoundItem newSound = new SoundItem { name = name, clip = clip };
            soundList.Add(newSound);
            soundDictionary.Add(name, clip);
        }
        else
        {
            Debug.LogWarning($"Sound with name '{name}' already exists");
        }
    }

    // Метод для удаления звука
    public void RemoveSound(string name)
    {
        if (soundDictionary.ContainsKey(name))
        {
            soundDictionary.Remove(name);
            soundList.RemoveAll(sound => sound.name == name);
        }
    }

    // Обновление словаря при изменении в инспекторе (для редактора)
    private void OnValidate()
    {
        InitializeSoundDictionary();
    }
}