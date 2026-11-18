using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Корень UI культивации: собирает все панели под существующим Canvas и раздаёт ввод.
/// Чужие элементы HUD (хотбар, HP-бар, инвентарь) не изменяются.
/// </summary>
[DisallowMultipleComponent]
public class CultivationUIRoot : MonoBehaviour
{
    public static CultivationUIRoot Instance { get; private set; }

    public Canvas Canvas { get; private set; }
    public PlayerVitalsUI Vitals { get; private set; }
    public DebuffPanelUI Debuffs { get; private set; }
    public AlchemyPanelUI Alchemy { get; private set; }
    public SkillListUI Skills { get; private set; }
    public QiBarUI QiBar { get; private set; }
    public CultivationUI CultivationPanel { get; private set; }
    public TechniqueListUI Techniques { get; private set; }
    public MeditationUI MeditationOverlay { get; private set; }
    public BreakthroughUI BreakthroughPanel { get; private set; }
    public CultivationQuickBarUI QuickBar { get; private set; }

    private MeditationController meditation;
    private CultivationCastController caster;
    private UiManager uiManager;

    public bool IsInputBlocked =>
        (CultivationPanel != null && CultivationPanel.IsOpen) ||
        (Techniques != null && Techniques.IsOpen) ||
        (Alchemy != null && Alchemy.IsOpen) ||
        (Skills != null && Skills.IsOpen) ||
        (BreakthroughPanel != null && BreakthroughPanel.gameObject.activeSelf);

    public static CultivationUIRoot Create(QiComponent qi, CultivationSystem cultivation, DaoSystem dao,
        MeditationController meditationController, BreakthroughSequence breakthrough, CultivationConfig config,
        CultivationCastController caster, Action<TechniqueData> assignHandler, UiManager manager,
        CultivationDebuffController debuffController = null, AlchemyService alchemyService = null,
        AlchemySkill alchemySkill = null, IReadOnlyList<AlchemyRecipe> alchemyRecipes = null,
        PassiveSkillService passiveService = null, CultivationAbilityBridge abilityBridge = null)
    {
        if (Instance != null) return Instance;

        CultivationUIFactory.EnsureEventSystem();
        var canvas = CultivationUIFactory.FindOrCreateCanvas();
        var rootRect = CultivationUIFactory.CreateRect("Cultivation UI", canvas.transform);
        CultivationUIFactory.Stretch(rootRect);

        var root = rootRect.gameObject.AddComponent<CultivationUIRoot>();
        root.Build(canvas, qi, cultivation, dao, meditationController, breakthrough, config, caster,
            assignHandler, manager, debuffController, alchemyService, alchemySkill, alchemyRecipes,
            passiveService, abilityBridge);
        return root;
    }

    private void Build(Canvas canvas, QiComponent qi, CultivationSystem cultivation, DaoSystem dao,
        MeditationController meditationController, BreakthroughSequence breakthrough, CultivationConfig config,
        CultivationCastController caster, Action<TechniqueData> assignHandler, UiManager manager,
        CultivationDebuffController debuffController, AlchemyService alchemyService,
        AlchemySkill alchemySkill, IReadOnlyList<AlchemyRecipe> alchemyRecipes,
        PassiveSkillService passiveService, CultivationAbilityBridge abilityBridge)
    {
        Instance = this;
        Canvas = canvas;
        meditation = meditationController;
        this.caster = caster;
        uiManager = manager;

        var vitalsRect = CultivationUIFactory.CreateRect("Player Vitals", canvas.transform);
        Vitals = vitalsRect.gameObject.AddComponent<PlayerVitalsUI>();
        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        Vitals.Build(playerHealth, qi, cultivation, dao, canvas.transform);
        QiBar = Vitals.QiBar;

        if (debuffController != null)
        {
            var debuffRect = CultivationUIFactory.CreateRect("Debuff Panel", canvas.transform);
            Debuffs = debuffRect.gameObject.AddComponent<DebuffPanelUI>();
            Debuffs.Build(debuffController, canvas.transform);
        }

        if (alchemyService != null)
        {
            var alchemyRect = CultivationUIFactory.CreateRect("Alchemy Panel", canvas.transform);
            Alchemy = alchemyRect.gameObject.AddComponent<AlchemyPanelUI>();
            Alchemy.Build(alchemyService, alchemySkill, FindFirstObjectByType<Inventory>(FindObjectsInactive.Include),
                cultivation, dao, alchemyRecipes, canvas.transform);
        }

        var skillsRect = CultivationUIFactory.CreateRect("Skill List", canvas.transform);
        Skills = skillsRect.gameObject.AddComponent<SkillListUI>();
        Skills.Build(dao, cultivation, passiveService, abilityBridge, canvas.transform);

        var techniqueRect = CultivationUIFactory.CreateRect("Technique List", transform);
        Techniques = techniqueRect.gameObject.AddComponent<TechniqueListUI>();
        Techniques.Build(dao, cultivation, config);

        if (assignHandler != null)
            Techniques.AssignFallback = assignHandler;

        var cultivationRect = CultivationUIFactory.CreateRect("Cultivation Panel", transform);
        CultivationPanel = cultivationRect.gameObject.AddComponent<CultivationUI>();
        CultivationPanel.Build(cultivation, dao, meditation, breakthrough, config, Techniques, uiManager);

        var meditationRect = CultivationUIFactory.CreateRect("Meditation Overlay", transform);
        MeditationOverlay = meditationRect.gameObject.AddComponent<MeditationUI>();
        MeditationOverlay.Build(meditation, cultivation, dao);

        var breakthroughRect = CultivationUIFactory.CreateRect("Breakthrough Panel", transform);
        BreakthroughPanel = breakthroughRect.gameObject.AddComponent<BreakthroughUI>();
        BreakthroughPanel.Build(breakthrough, cultivation);

        if (caster == null) return;

        var quickRect = CultivationUIFactory.CreateRect("Quick Bar", transform);
        QuickBar = quickRect.gameObject.AddComponent<CultivationQuickBarUI>();
        QuickBar.Build(caster, Techniques, dao);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (CultivationPanel == null) return;

        if (CultivationInput.Pressed(Key.Tab, KeyCode.Tab) || CultivationInput.Pressed(Key.C, KeyCode.C))
        {
            if (Techniques != null && Techniques.IsOpen) Techniques.Close();
            CultivationPanel.Toggle();
        }

        if (CultivationInput.Pressed(Key.M, KeyCode.M) && !IsInputBlocked && meditation != null)
        {
            if (meditation.IsMeditating) meditation.ExitMeditation();
            else meditation.EnterMeditation();
        }

        UpdateQuickBarVisibility();
    }

    private void UpdateQuickBarVisibility()
    {
        if (QuickBar == null) return;

        bool meditating = meditation != null && meditation.IsMeditating;
        bool suppressed = caster != null && caster.IsSuppressed;
        bool shouldShow = !meditating && !suppressed;

        if (QuickBar.gameObject.activeSelf != shouldShow)
            QuickBar.gameObject.SetActive(shouldShow);
    }
}
