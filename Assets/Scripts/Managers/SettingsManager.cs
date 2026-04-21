using UnityEngine;
using UnityEngine.InputSystem;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [SerializeField] private PlayerController playerController;

    private const string CONTROL_MODE_KEY = "ControlMode";

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
    }

    private void Start()
    {
        // Загружаем сохранённый режим
        //int savedMode = PlayerPrefs.GetInt(CONTROL_MODE_KEY, (int)PlayerController.ControlMode.ClickToMove);
        //PlayerController.ControlMode mode = (PlayerController.ControlMode)savedMode;
        //playerController.SetControlMode(mode);

        // Загружаем сохранённые переназначения клавиш
        LoadBindings();
    }

    // Вызывается из UI при выборе режима
    public void ChangeControlMode(int modeIndex)
    {
        //PlayerController.ControlMode mode = (PlayerController.ControlMode)modeIndex;
        //playerController.SetControlMode(mode);
        PlayerPrefs.SetInt(CONTROL_MODE_KEY, modeIndex);
        PlayerPrefs.Save();
    }

    // Сохраняем переопределения биндингов
    public void SaveBindings()
    {
        if (InputManager.Instance?.Controls != null)
        {
            string rebinds = InputManager.Instance.Controls.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString("Bindings", rebinds);
            PlayerPrefs.Save();
            Debug.Log("Bindings saved");
        }
    }

    // Загружаем переопределения биндингов
    public void LoadBindings()
    {
        if (InputManager.Instance?.Controls != null && PlayerPrefs.HasKey("Bindings"))
        {
            string rebinds = PlayerPrefs.GetString("Bindings");
            InputManager.Instance.Controls.LoadBindingOverridesFromJson(rebinds);
            Debug.Log("Bindings loaded");
        }
    }
}