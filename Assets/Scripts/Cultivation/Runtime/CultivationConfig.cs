using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Единая точка настройки культивации. Setup-скрипт создаёт ассет в
/// Assets/Resources/Cultivation/CultivationConfig.asset, bootstrap грузит его в рантайме.
/// Без ассета поднимается временный конфиг с полным набором контента — можно играть сразу.
/// </summary>
[CreateAssetMenu(fileName = "CultivationConfig", menuName = "Cultivation/Cultivation Config", order = 10)]
public class CultivationConfig : ScriptableObject
{
    [Header("Контент")]
    [SerializeField] private List<CultivationRealm> realms = new List<CultivationRealm>();
    [SerializeField] private List<DaoPath> daoPaths = new List<DaoPath>();

    [Header("Старт")]
    [SerializeField] private int startRealmIndex = 0;
    [SerializeField, Min(1)] private int startStage = 1;

    [Header("Опыт Ци")]
    [SerializeField, Min(1)] private int baseExperienceQi = 100;
    [SerializeField, Min(1f)] private float experienceGrowth = 1.45f;
    [SerializeField, Range(0f, 1f)] private float baseBreakthroughChance = 0.35f;

    [Header("Ци")]
    [SerializeField, Min(1f)] private float maxQi = 100f;
    [SerializeField, Min(0f)] private float qiRegenPerSecond = 1f;

    [Header("Медитация")]
    [SerializeField, Min(0.1f)] private float meditationTickInterval = 1f;
    [SerializeField, Min(0)] private int meditationXpPerTick = 15;
    [SerializeField, Min(0)] private int meditationDaoPointsPerTick = 2;
    [SerializeField, Min(0f)] private float meditationQiRestorePerSecond = 6f;
    [SerializeField, Min(0f)] private float meditationComprehensionPerMinute = 3f;

    [Header("Прорыв")]
    [SerializeField, Min(0.5f)] private float breakthroughDuration = 3f;

    [Header("Дао")]
    [SerializeField] private List<DaoType> startingUnlockedDaos = new List<DaoType> { DaoType.Sword, DaoType.Fist };
    [SerializeField] private bool canSwitchAnywhere = false;
    [SerializeField, Min(1)] private int pointsPerLevel = 10;
    [SerializeField, Min(0)] private int startingDaoLevel = 1;

    [Header("Быстрый каст (пока нет AbilitySystem)")]
    [Tooltip("Клавиши 1–5 используют техники. Отключается автоматически при появлении AbilitySystem.")]
    [SerializeField] private bool enableDemoCasting = true;
    [SerializeField] private bool demoDealsDamage = true;

    [Header("Сохранение")]
    [Tooltip("Прогресс переживает смену сцен и перезапуск игры (PlayerPrefs).")]
    [SerializeField] private bool persistBetweenSessions = true;

    [Header("UI")]
    [Tooltip("Необязательный TMP-шрифт с CJK (для иероглифов). Без него показываются русские названия.")]
    [SerializeField] private TMP_FontAsset calligraphyFont;
    [SerializeField] private bool showChineseNames = false;

    [Header("Автозапуск")]
    [SerializeField] private bool autoBootstrap = true;

    private static CultivationConfig runtimeDefault;

    public IReadOnlyList<CultivationRealm> Realms => realms;
    public IReadOnlyList<DaoPath> DaoPaths => daoPaths;
    public int StartRealmIndex => startRealmIndex;
    public int StartStage => startStage;
    public int BaseExperienceQi => baseExperienceQi;
    public float ExperienceGrowth => experienceGrowth;
    public float BaseBreakthroughChance => baseBreakthroughChance;
    public float MaxQi => maxQi;
    public float QiRegenPerSecond => qiRegenPerSecond;
    public float MeditationTickInterval => meditationTickInterval;
    public int MeditationXpPerTick => meditationXpPerTick;
    public int MeditationDaoPointsPerTick => meditationDaoPointsPerTick;
    public float MeditationQiRestorePerSecond => meditationQiRestorePerSecond;
    public float MeditationComprehensionPerMinute => meditationComprehensionPerMinute;
    public float BreakthroughDuration => breakthroughDuration;
    public IReadOnlyList<DaoType> StartingUnlockedDaos => startingUnlockedDaos;
    public bool CanSwitchAnywhere => canSwitchAnywhere;
    public int PointsPerLevel => pointsPerLevel;
    public int StartingDaoLevel => startingDaoLevel;
    public bool EnableDemoCasting => enableDemoCasting;
    public bool DemoDealsDamage => demoDealsDamage;
    public bool PersistBetweenSessions => persistBetweenSessions;
    public TMP_FontAsset CalligraphyFont => calligraphyFont;
    public bool ShowChineseNames => showChineseNames && calligraphyFont != null;
    public bool AutoBootstrap => autoBootstrap;

    /// <summary>Загрузка ассета; если его нет — временный конфиг с полным контентом.</summary>
    public static CultivationConfig Load()
    {
        var asset = Resources.Load<CultivationConfig>("Cultivation/CultivationConfig");
        if (asset != null) return asset;

        if (runtimeDefault != null) return runtimeDefault;

        Debug.LogWarning("[Cultivation] Не найден Resources/Cultivation/CultivationConfig. " +
                         "Работаю на временном конфиге. Ассеты можно создать через Tools → Cultivation → Setup Test Assets.");
        runtimeDefault = CreateRuntimeDefault();
        return runtimeDefault;
    }

    public static CultivationConfig CreateRuntimeDefault()
    {
        var config = CreateInstance<CultivationConfig>();
        config.name = "CultivationConfig (Runtime Default)";
        config.hideFlags = HideFlags.DontSave;

        config.realms = CultivationDefaults.CreateRealms();
        config.daoPaths = CultivationDefaults.CreateDaoPaths();

        // Демо-режим: ассетов нет, алтаря ещё нет — открываем все пути и разрешаем
        // переключение из панели, чтобы систему можно было сразу попробовать в игре.
        // Setup-ассет создаётся с каноническими настройками (2 пути, только у алтаря).
        config.canSwitchAnywhere = true;
        config.startingUnlockedDaos = new List<DaoType>(DaoTypeInfo.All);
        return config;
    }
}
