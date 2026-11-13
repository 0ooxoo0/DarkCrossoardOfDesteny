using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Автоподключение культивации: находит игрока в сцене, вешает компоненты,
/// грузит CultivationConfig, восстанавливает прогресс и собирает UI.
/// Чужие скрипты не модифицируются.
/// </summary>
[DefaultExecutionOrder(-90)]
[DisallowMultipleComponent]
public class CultivationBootstrap : MonoBehaviour
{
    private static CultivationBootstrap instance;

    public static CultivationBootstrap Instance => instance;

    /// <summary>(qiBonus, hpBonus, regenBonus) — владелец HP (Промт B) применяет бонусы реалма.</summary>
    public static event Action<int, int, float> PlayerRealmBonusesChanged;

    public CultivationConfig Config { get; private set; }
    public GameObject Player { get; private set; }
    public QiComponent Qi { get; private set; }
    public CultivationSystem Cultivation { get; private set; }
    public DaoSystem Dao { get; private set; }
    public MeditationController Meditation { get; private set; }
    public BreakthroughSequence Breakthrough { get; private set; }
    public DaoAuraController Aura { get; private set; }
    public CultivationCastController Caster { get; private set; }
    public CultivationAbilityBridge Bridge { get; private set; }
    public CultivationUIRoot UI { get; private set; }
    public Inventory PlayerInventory { get; private set; }

    private float baseMaxQi;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeOnLoad()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        BootScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BootScene();
    }

    private static void BootScene()
    {
        if (instance != null) return;

        var go = new GameObject("[Cultivation]");
        instance = go.AddComponent<CultivationBootstrap>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        StartCoroutine(BootRoutine());
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause) Persist();
    }

    private void OnApplicationQuit()
    {
        Persist();
    }

    private void OnDestroy()
    {
        Persist();
        UnsubscribeStateCapture();

        if (Cultivation != null)
        {
            Cultivation.OnRealmBonusesChanged -= HandleRealmBonuses;
            Cultivation.OnBreakthroughSucceeded -= HandleBreakthroughSucceeded;
        }

        UnsubscribeTechniqueEvents();

        if (instance == this) instance = null;
    }

    private IEnumerator BootRoutine()
    {
        // Ждём кадр, чтобы PlayerController и HUD успели инициализироваться.
        yield return null;

        Config = CultivationConfig.Load();
        if (Config == null || !Config.AutoBootstrap)
        {
            Destroy(gameObject);
            yield break;
        }

        Player = FindPlayer();

        // Игрок может появиться чуть позже — подождём до 5 секунд.
        for (int attempt = 0; attempt < 10 && Player == null; attempt++)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            Player = FindPlayer();
        }

        if (Player == null)
        {
            Destroy(gameObject);
            yield break;
        }

        if (Player.GetComponent<CultivationSystem>() == null)
            WireNewComponents();
        else
            BindExistingComponents();

        BuildUI();
    }

    private void WireNewComponents()
    {
        Qi = AddOrGet<QiComponent>(Player);
        Cultivation = AddOrGet<CultivationSystem>(Player);
        Dao = AddOrGet<DaoSystem>(Player);
        AddOrGet<MeditationInterruptSource>(Player);
        Meditation = AddOrGet<MeditationController>(Player);
        Breakthrough = AddOrGet<BreakthroughSequence>(Player);
        Aura = AddOrGet<DaoAuraController>(Player);

        baseMaxQi = Config.MaxQi;
        Qi.Configure(Config.MaxQi, Config.QiRegenPerSecond, true);

        var realms = new List<CultivationRealm>(Config.Realms);
        var paths = new List<DaoPath>(Config.DaoPaths);
        var startingDaos = new List<DaoType>(Config.StartingUnlockedDaos);

        Cultivation.Initialize(realms, Config.StartRealmIndex, Config.StartStage,
            Config.BaseExperienceQi, Config.ExperienceGrowth, Config.BaseBreakthroughChance);
        Dao.Initialize(paths, Cultivation, Config.CanSwitchAnywhere, startingDaos,
            Config.PointsPerLevel, Config.StartingDaoLevel);
        Meditation.Initialize(Cultivation, Dao, Qi, Config.MeditationTickInterval, Config.MeditationXpPerTick,
            Config.MeditationDaoPointsPerTick, Config.MeditationQiRestorePerSecond, Config.MeditationComprehensionPerMinute);
        Breakthrough.Initialize(Cultivation, Config.BreakthroughDuration);

        Cultivation.OnRealmBonusesChanged += HandleRealmBonuses;
        HandleRealmBonuses(Cultivation.TotalQiBonus, Cultivation.TotalHpBonus, Cultivation.TotalRegenBonus);

        CultivationStateCache.LoadFromPrefs();
        if (CultivationStateCache.TryRestore(Cultivation, Dao, Qi))
            Debug.Log("[Cultivation] Прогресс восстановлен.");

        SetupTechniqueOutput();
        WireInventory();
        Aura.Initialize(Dao, Cultivation);
        SubscribeStateCapture();

        Debug.Log("[Cultivation] Подключено: " + Cultivation.GetProgressLabel() +
                  ", Ци " + Qi.Current + "/" + Qi.Max +
                  (Bridge != null ? ", техники → AbilitySystem (хотбар 4–5)" : ", демо-каст (1–5)") +
                  ". Tab — панель, M — медитация.");
    }

    private void BindExistingComponents()
    {
        Cultivation = Player.GetComponent<CultivationSystem>();
        Qi = Player.GetComponent<QiComponent>();
        Dao = Player.GetComponent<DaoSystem>();
        Meditation = Player.GetComponent<MeditationController>();
        Breakthrough = Player.GetComponent<BreakthroughSequence>();
        Aura = Player.GetComponent<DaoAuraController>();
        Caster = Player.GetComponent<CultivationCastController>();
        Bridge = Player.GetComponent<CultivationAbilityBridge>();

        if (Qi != null) baseMaxQi = Qi.MaxQi;

        if (Cultivation != null)
        {
            Cultivation.OnRealmBonusesChanged -= HandleRealmBonuses;
            Cultivation.OnRealmBonusesChanged += HandleRealmBonuses;
        }

        CultivationStateCache.LoadFromPrefs();
        if (Qi != null || Cultivation != null)
            CultivationStateCache.TryRestore(Cultivation, Dao, Qi);

        SetupTechniqueOutput();
        WireInventory();
        SubscribeStateCapture();
    }

    /// <summary>
    /// Если на игроке есть AbilitySystem (Промт B) — техники уходят в его слоты 4–5.
    /// Иначе включается демо-каст с собственной панелью.
    /// </summary>
    private void SetupTechniqueOutput()
    {
        var abilitySystem = Player.GetComponent<AbilitySystem>();

        if (abilitySystem != null)
        {
            Bridge = AddOrGet<CultivationAbilityBridge>(Player);
            Bridge.Initialize(Dao, Qi, abilitySystem);
            return;
        }

        Caster = AddOrGet<CultivationCastController>(Player);
        Caster.Initialize(Dao, Qi, Cultivation, Meditation, Config.EnableDemoCasting, Config.DemoDealsDamage);
    }

    /// <summary>Первый запуск: одна доступная техника сразу попадает в хотбар, чтобы нажать «1».</summary>
    private void AutoRegisterStarterTechnique()
    {
        if (Bridge == null || Dao == null) return;
        if (Bridge.RegisteredCount > 0) return;

        var available = Dao.GetAvailableTechniques();
        if (available.Count > 0)
            Bridge.RegisterTechnique(available[0]);
    }

    private void AutoAssignStarterTechnique()
    {
        if (Caster == null || Dao == null) return;
        if (Caster.GetSlot(0) != null) return;

        var available = Dao.GetAvailableTechniques();
        if (available.Count > 0)
            Caster.Assign(available[0], 0);
    }

    private void HandleRealmBonuses(int qiBonus, int hpBonus, float regenBonus)
    {
        if (Qi != null)
        {
            Qi.SetMaxQi(baseMaxQi + qiBonus, true);
            Qi.RegenPerSecond = Config != null ? Config.QiRegenPerSecond + regenBonus : Qi.RegenPerSecond;
        }

        PlayerRealmBonusesChanged?.Invoke(qiBonus, hpBonus, regenBonus);
    }

    /// <summary>Связка культивации с инвентарём: материалы прорыва и расходники.</summary>
    private void WireInventory()
    {
        if (Cultivation == null) return;

        if (PlayerInventory == null)
            PlayerInventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        Cultivation.HasBreakthroughMaterials = HasBreakthroughMaterials;
        Cultivation.OnBreakthroughSucceeded -= HandleBreakthroughSucceeded;
        Cultivation.OnBreakthroughSucceeded += HandleBreakthroughSucceeded;

        if (PlayerInventory == null)
        {
            Debug.Log("[Cultivation] Inventory не найден — материалы прорыва и расходники отключены.");
            return;
        }

        var itemBridge = AddOrGet<CultivationItemBridge>(Player);
        itemBridge.Initialize(Qi, Cultivation, Dao, PlayerInventory);
    }

    private bool HasBreakthroughMaterials()
    {
        if (Cultivation == null) return false;

        // Инвентаря в сцене нет — материалы не требуем: иначе прорыв невозможен в принципе.
        if (PlayerInventory == null) return true;

        CultivationRealm realm = Cultivation.CurrentRealm;
        if (realm == null) return false;

        var materials = realm.RequiredMaterials;
        if (materials == null || materials.Count == 0) return true;

        return PlayerInventory != null && PlayerInventory.HasAllItemsWithTags(materials);
    }

    private void HandleBreakthroughSucceeded(CultivationRealm realm)
    {
        if (PlayerInventory == null || realm == null) return;

        var materials = realm.RequiredMaterials;
        if (materials == null || materials.Count == 0) return;

        int removed = PlayerInventory.RemoveItemsByTags(materials, 1);
        if (removed > 0)
            Debug.Log("[Cultivation] Материалы прорыва списаны: " + removed);
    }

    private void BuildUI()
    {
        if (CultivationUIRoot.Instance != null) return;

        UI = CultivationUIRoot.Create(Qi, Cultivation, Dao, Meditation, Breakthrough, Config,
            Caster, GetAssignHandler(), FindUiManager());
        SubscribeTechniqueEvents();
    }

    private void SubscribeTechniqueEvents()
    {
        if (UI == null || UI.Techniques == null) return;
        UI.Techniques.OnTechniqueAssignRequested -= HandleTechniqueAssignRequested;
        UI.Techniques.OnTechniqueAssignRequested += HandleTechniqueAssignRequested;
    }

    private void UnsubscribeTechniqueEvents()
    {
        if (UI == null || UI.Techniques == null) return;
        UI.Techniques.OnTechniqueAssignRequested -= HandleTechniqueAssignRequested;
    }

    private void HandleTechniqueAssignRequested(TechniqueData technique)
    {
        if (Bridge != null)
            Bridge.AssignTechnique(technique);
        else if (Caster != null)
            Caster.Assign(technique);
    }

    private Action<TechniqueData> GetAssignHandler()
    {
        if (Bridge != null) return technique => Bridge.AssignTechnique(technique);
        if (Caster != null) return technique => Caster.Assign(technique);
        return null;
    }

    private static UiManager FindUiManager()
    {
        return UnityEngine.Object.FindFirstObjectByType<UiManager>();
    }

    private void Persist()
    {
        if (Cultivation == null) return;

        CultivationStateCache.Capture(Cultivation, Dao, Qi);
        if (Config != null && Config.PersistBetweenSessions)
            CultivationStateCache.SaveToPrefs();
    }

    private void SubscribeStateCapture()
    {
        if (Cultivation != null)
        {
            Cultivation.OnRealmChanged += HandleRealmChangedCapture;
            Cultivation.OnStageChanged += HandleStageChangedCapture;
            Cultivation.OnExperienceQiChanged += HandleExperienceChangedCapture;
            Cultivation.OnComprehensionChanged += HandleComprehensionChangedCapture;
        }

        if (Dao != null)
        {
            Dao.OnDaoLevelChanged += HandleDaoLevelChangedCapture;
            Dao.OnDaoPointsChanged += HandleDaoPointsChangedCapture;
            Dao.OnActiveDaoChanged += HandleActiveDaoChangedCapture;
            Dao.OnDaoUnlocked += HandleDaoUnlockedCapture;
            Dao.OnTechniqueLearned += HandleTechniqueLearnedCapture;
        }

        if (Qi != null)
            Qi.OnQiChanged += HandleQiChangedCapture;
    }

    private void UnsubscribeStateCapture()
    {
        if (Cultivation != null)
        {
            Cultivation.OnRealmChanged -= HandleRealmChangedCapture;
            Cultivation.OnStageChanged -= HandleStageChangedCapture;
            Cultivation.OnExperienceQiChanged -= HandleExperienceChangedCapture;
            Cultivation.OnComprehensionChanged -= HandleComprehensionChangedCapture;
        }

        if (Dao != null)
        {
            Dao.OnDaoLevelChanged -= HandleDaoLevelChangedCapture;
            Dao.OnDaoPointsChanged -= HandleDaoPointsChangedCapture;
            Dao.OnActiveDaoChanged -= HandleActiveDaoChangedCapture;
            Dao.OnDaoUnlocked -= HandleDaoUnlockedCapture;
            Dao.OnTechniqueLearned -= HandleTechniqueLearnedCapture;
        }

        if (Qi != null)
            Qi.OnQiChanged -= HandleQiChangedCapture;
    }

    private void CaptureState()
    {
        CultivationStateCache.Capture(Cultivation, Dao, Qi);
    }

    private void HandleRealmChangedCapture(CultivationRealm realm) => CaptureState();
    private void HandleStageChangedCapture(CultivationRealm realm, int stage) => CaptureState();
    private void HandleExperienceChangedCapture(int current, int max) => CaptureState();
    private void HandleComprehensionChangedCapture(int value) => CaptureState();
    private void HandleDaoLevelChangedCapture(DaoType type, int level) => CaptureState();
    private void HandleDaoPointsChangedCapture(DaoType type, int points, int cost) => CaptureState();
    private void HandleActiveDaoChangedCapture(DaoType type) => CaptureState();
    private void HandleDaoUnlockedCapture(DaoType type) => CaptureState();
    private void HandleTechniqueLearnedCapture(TechniqueData technique) => CaptureState();
    private void HandleQiChangedCapture(int current, int max) => CaptureState();

    private static T AddOrGet<T>(GameObject target) where T : Component
    {
        var component = target.GetComponent<T>();
        if (component == null)
            component = target.AddComponent<T>();
        return component;
    }

    private static GameObject FindPlayer()
    {
        var controller = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (controller != null) return controller.gameObject;

        try
        {
            return GameObject.FindWithTag("Player");
        }
        catch (UnityException)
        {
            return null;
        }
    }
}
