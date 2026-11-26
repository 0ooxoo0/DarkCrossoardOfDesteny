using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

#if UNITY_ANDROID
#error This project currently targets PC only; Android touch input is not supported.
#endif

/// <summary>
/// Единый владелец Esc: стек открытых панелей, пауза и кнопки меню.
/// Панели сообщают об открытии и закрытии, Esc закрывает верхнюю панель,
/// а когда стек пуст — открывает или закрывает паузу через <see cref="UiManager"/>.
/// </summary>
[DisallowMultipleComponent]
public class GameMenuController : MonoBehaviour
{
    [Header("Кнопки паузы")]
    [SerializeField] private Button cultivationButton;
    [SerializeField] private Button skillsButton;
    [SerializeField] private Button craftingButton;
    [SerializeField] private Button journalButton;

    [Header("Legacy scene lookup")]
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
        // Пока игрок назначает клавишу, Esc — это отмена ребинда,
        // а не закрытие паузы. Флаг EscapeConsumedThisFrame нужен потому,
        // что менеджер может отменить ребинд раньше в этом же кадре.
        if (InputRemapManager.IsRebinding || InputRemapManager.EscapeConsumedThisFrame)
            return;

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

    /// <summary>Журнал пока не реализован; его кнопка отключена.</summary>
    public void OpenJournal()
    {
        Debug.LogWarning(name + ": journal is not implemented.", this);
    }

    private void ResolveUiManager()
    {
        if (uiManager != null) return;
        uiManager = FindFirstObjectByType<UiManager>();
    }

    private void WirePauseButtons()
    {
        if (buttonsWired) return;
        buttonsWired = true;

        ResolveButton(ref cultivationButton, "Культивация", nameof(cultivationButton));
        ResolveButton(ref skillsButton, "Техники", nameof(skillsButton));
        ResolveButton(ref craftingButton, "Крафт", nameof(craftingButton));
        ResolveButton(ref journalButton, "Журнал", nameof(journalButton));

        WireButton(cultivationButton, OpenCultivation, nameof(cultivationButton));
        WireButton(skillsButton, OpenSkills, nameof(skillsButton));
        WireButton(craftingButton, OpenCrafting, nameof(craftingButton));

        if (journalButton != null)
        {
            journalButton.interactable = false;
            Debug.LogWarning(name + ": journalButton disabled because the journal is not implemented.", this);
        }
    }

    private void ResolveButton(ref Button button, string legacyName, string fieldName)
    {
        if (button == null)
        {
            Transform container = FindButtonContainer();
            if (container != null)
            {
                Transform legacyObject = container.Find(legacyName);
                if (legacyObject != null) button = legacyObject.GetComponent<Button>();
            }
        }

        if (button == null)
            Debug.LogWarning(name + ": " + fieldName + " is not assigned or found.", this);
    }

    private void WireButton(Button button, UnityEngine.Events.UnityAction action, string fieldName)
    {
        if (button == null) return;
        button.onClick.AddListener(action);
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
