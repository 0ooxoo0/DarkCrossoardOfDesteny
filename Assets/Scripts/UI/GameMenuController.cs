using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Единый владелец Esc: стек открытых панелей, пауза и кнопки меню.
/// Панели сообщают об открытии и закрытии, Esc закрывает верхнюю панель,
/// а когда стек пуст — открывает или закрывает паузу через <see cref="UiManager"/>.
/// </summary>
[DisallowMultipleComponent]
public class GameMenuController : MonoBehaviour
{
    [Header("Кнопки паузы")]
    [SerializeField] private string pauseMenuRootName = "PauseMenu";
    [SerializeField] private string buttonContainerName = "Кнопки";

    private readonly List<GameObject> openPanels = new List<GameObject>();
    private readonly HashSet<GameObject> registered = new HashSet<GameObject>();

    private UiManager uiManager;
    private bool buttonsWired;

    public static GameMenuController Instance { get; private set; }

    /// <summary>Открыта ли хотя бы одна панель стека.</summary>
    public bool IsAnyPanelOpen => openPanels.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<GameMenuController>() != null) return;

        var go = new GameObject("[Game Menu]");
        go.AddComponent<GameMenuController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        WirePauseButtons();

        // Если UiManager в сцене есть, Esc приходит от него; иначе — здесь.
        ResolveUiManager();
        if (uiManager != null) return;
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

        ToggleMenu();
    }

    /// <summary>Регистрирует панель как управляемую (без открытия).</summary>
    public void RegisterPanel(GameObject panel)
    {
        if (panel != null) registered.Add(panel);
    }

    /// <summary>Добавляет панель в стек открытых и показывает её.</summary>
    public void OpenPanel(GameObject panel)
    {
        if (panel == null) return;

        RegisterPanel(panel);
        if (!panel.activeSelf) panel.SetActive(true);

        if (!openPanels.Contains(panel))
            openPanels.Add(panel);
    }

    /// <summary>Панель закрылась сама — убирает её из стека.</summary>
    public void NotifyClosed(GameObject panel)
    {
        if (panel != null) openPanels.Remove(panel);
    }

    /// <summary>Закрывает верхнюю панель стека.</summary>
    public void CloseTopPanel()
    {
        if (openPanels.Count == 0) return;

        int last = openPanels.Count - 1;
        GameObject panel = openPanels[last];
        openPanels.RemoveAt(last);

        if (panel == null) return;

        // У панелей разный Close (инвентарь, культивация, скиллы, алхимия).
        panel.SendMessage("Close", SendMessageOptions.DontRequireReceiver);
        if (panel != null && panel.activeSelf) panel.SetActive(false);
    }

    /// <summary>Esc: закрыть верхнюю панель или переключить меню паузы.</summary>
    public void ToggleMenu()
    {
        CultivationUIRoot cultivationUi = CultivationUIRoot.Instance;
        if (cultivationUi != null && cultivationUi.MeditationScreen != null &&
            cultivationUi.MeditationScreen.IsOpen)
        {
            cultivationUi.MeditationScreen.Close();
            return;
        }

        if (openPanels.Count > 0)
        {
            CloseTopPanel();
            return;
        }

        ResolveUiManager();
        if (uiManager == null)
        {
            Debug.LogWarning("[GameMenu] UiManager не найден — меню паузы недоступно.");
            return;
        }

        if (uiManager.IsOpen) uiManager.CloseUI();
        else uiManager.OpenUI();
    }

    /// <summary>Открывает панель культивации из меню.</summary>
    public void OpenCultivation()
    {
        CultivationUIRoot ui = CultivationUIRoot.Instance;
        if (ui != null && ui.CultivationPanel != null) ui.CultivationPanel.Open();
    }

    /// <summary>Открывает экран скиллов.</summary>
    public void OpenSkills()
    {
        CultivationUIRoot ui = CultivationUIRoot.Instance;
        if (ui != null && ui.Skills != null) ui.Skills.Open();
    }

    /// <summary>Открывает походный крафт.</summary>
    public void OpenCrafting()
    {
        CultivationUIRoot ui = CultivationUIRoot.Instance;
        if (ui != null && ui.Alchemy != null) ui.Alchemy.OpenPortable();
    }

    /// <summary>Заглушка журнала до появления квестов.</summary>
    public void OpenJournal()
    {
        Debug.Log("[GameMenu] Журнал появится вместе с квестами.");
    }

    private void ResolveUiManager()
    {
        if (uiManager != null) return;
        uiManager = FindFirstObjectByType<UiManager>();
    }

    private void WirePauseButtons()
    {
        if (buttonsWired) return;

        Transform container = FindButtonContainer();
        if (container == null) return;

        buttonsWired = true;
        Button[] buttons = container.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null) continue;

            switch (button.name)
            {
                case "Культивация": button.onClick.AddListener(OpenCultivation); break;
                case "Техники": button.onClick.AddListener(OpenSkills); break;
                case "Крафт": button.onClick.AddListener(OpenCrafting); break;
                case "Журнал": button.onClick.AddListener(OpenJournal); break;
            }
        }
    }

    private Transform FindButtonContainer()
    {
        var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate.name != buttonContainerName) continue;

            Transform parent = candidate.parent;
            while (parent != null)
            {
                if (parent.name == pauseMenuRootName) return candidate;
                parent = parent.parent;
            }
        }

        return null;
    }
}
