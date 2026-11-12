#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Создаёт ассеты культивации из эталонного контента CultivationDefaults:
/// 4 реалма × 3 подстадии, 5 DaoPath, по 3 TechniqueData на путь, CultivationConfig в Resources.
/// Меню: Tools → Cultivation → Setup Test Assets.
/// </summary>
public static class CultivationSetup
{
    private const string DataFolder = "Assets/Scripts/Cultivation/Data";
    private const string RealmFolder = DataFolder + "/Realms";
    private const string DaoFolder = DataFolder + "/Dao";
    private const string TechniqueFolder = DataFolder + "/Techniques";
    private const string ResourcesFolder = "Assets/Resources/Cultivation";
    private const string ConfigPath = ResourcesFolder + "/CultivationConfig.asset";

    [MenuItem("Tools/Cultivation/Setup Test Assets", priority = 0)]
    public static void SetupTestAssets()
    {
        EnsureFolder(RealmFolder);
        EnsureFolder(DaoFolder);
        EnsureFolder(TechniqueFolder);
        EnsureFolder(ResourcesFolder);

        List<CultivationRealm> realms = CreateRealms();
        List<DaoPath> daoPaths = CreateDaoPaths();
        CultivationConfig config = CreateConfig(realms, daoPaths);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);

        Debug.Log("[Cultivation] Готово: " + realms.Count + " реалмов, " + daoPaths.Count +
                  " путей Дао, " + CountTechniques(daoPaths) + " техник. Конфиг: " + ConfigPath);
    }

    [MenuItem("Tools/Cultivation/Reset Saved Progress", priority = 20)]
    public static void ResetSavedProgress()
    {
        CultivationStateCache.Reset();
        Debug.Log("[Cultivation] Сохранённый прогресс сброшен (PlayerPrefs).");
    }

    [MenuItem("Tools/Cultivation/Validate Setup", priority = 1)]
    public static void ValidateSetup()
    {
        var config = AssetDatabase.LoadAssetAtPath<CultivationConfig>(ConfigPath);
        if (config == null)
        {
            Debug.LogError("[Cultivation] Конфиг не найден: " + ConfigPath + ". Запустите Setup Test Assets.");
            return;
        }

        int errors = 0;
        if (config.Realms == null || config.Realms.Count < 4)
        {
            Debug.LogError("[Cultivation] В конфиге меньше 4 реалмов.");
            errors++;
        }

        if (config.DaoPaths == null || config.DaoPaths.Count < 5)
        {
            Debug.LogError("[Cultivation] В конфиге меньше 5 путей Дао.");
            errors++;
        }

        foreach (var path in config.DaoPaths)
        {
            if (path == null) continue;
            if (path.Techniques == null || path.Techniques.Count < 3)
                Debug.LogWarning("[Cultivation] У пути " + path.DisplayName + " меньше 3 техник.");
        }

        Debug.Log(errors == 0 ? "[Cultivation] Проверка пройдена." : "[Cultivation] Проверка нашла проблемы: " + errors);
    }

    private static List<CultivationRealm> CreateRealms()
    {
        var realms = new List<CultivationRealm>();

        for (int i = 0; i < CultivationDefaults.Realms.Length; i++)
        {
            var blueprint = CultivationDefaults.Realms[i];
            string path = RealmFolder + "/Realm_" + (i + 1) + "_" + Sanitize(blueprint.Name) + ".asset";
            var realm = CreateOrLoad<CultivationRealm>(path);

            realm.Setup(blueprint.Name, blueprint.Chinese, blueprint.Description, i, 3,
                blueprint.QiBonus, blueprint.HpBonus, blueprint.RegenBonus, blueprint.Comprehension,
                blueprint.Chance, new List<string>(blueprint.Materials));

            EditorUtility.SetDirty(realm);
            realms.Add(realm);
        }

        return realms;
    }

    private static List<DaoPath> CreateDaoPaths()
    {
        var paths = new List<DaoPath>();

        foreach (DaoType type in DaoTypeInfo.All)
        {
            string path = DaoFolder + "/Dao_" + type + ".asset";
            var daoPath = CreateOrLoad<DaoPath>(path);
            var techniques = new List<TechniqueData>();

            List<CultivationDefaults.TechniqueBlueprint> blueprints = CultivationDefaults.GetTechniques(type);
            for (int i = 0; i < blueprints.Count; i++)
            {
                var blueprint = blueprints[i];
                string techniquePath = TechniqueFolder + "/Technique_" + blueprint.Id + ".asset";
                var technique = CreateOrLoad<TechniqueData>(techniquePath);

                technique.Setup(blueprint.Id, blueprint.Name, blueprint.Chinese,
                    blueprint.Name + " — " + blueprint.Kind + " техника пути " + DaoTypeInfo.GetDisplayName(type) + ".",
                    type, blueprint.Kind, blueprint.Rank, blueprint.RequiredDaoLevel, blueprint.RequiredRealmIndex,
                    blueprint.QiCost, blueprint.Cooldown, blueprint.Power, null, null, null);

                EditorUtility.SetDirty(technique);
                techniques.Add(technique);
            }

            daoPath.Setup(type, DaoTypeInfo.GetDisplayName(type), DaoTypeInfo.GetChineseName(type),
                "Путь " + type + ": техники открываются по уровню Дао и реалму.",
                DaoTypeInfo.GetColor(type), 10, null, techniques);

            EditorUtility.SetDirty(daoPath);
            paths.Add(daoPath);
        }

        return paths;
    }

    private static CultivationConfig CreateConfig(List<CultivationRealm> realms, List<DaoPath> daoPaths)
    {
        var config = CreateOrLoad<CultivationConfig>(ConfigPath);

        var serialized = new SerializedObject(config);
        SetObjectArray(serialized.FindProperty("realms"), realms);
        SetObjectArray(serialized.FindProperty("daoPaths"), daoPaths);

        SetInt(serialized, "startRealmIndex", 0);
        SetInt(serialized, "startStage", 1);
        SetInt(serialized, "baseExperienceQi", 100);
        SetFloat(serialized, "experienceGrowth", 1.45f);
        SetFloat(serialized, "baseBreakthroughChance", 0.35f);
        SetFloat(serialized, "maxQi", 100f);
        SetFloat(serialized, "qiRegenPerSecond", 1f);
        SetFloat(serialized, "meditationTickInterval", 1f);
        SetInt(serialized, "meditationXpPerTick", 15);
        SetInt(serialized, "meditationDaoPointsPerTick", 2);
        SetFloat(serialized, "meditationQiRestorePerSecond", 6f);
        SetFloat(serialized, "meditationComprehensionPerMinute", 3f);
        SetFloat(serialized, "breakthroughDuration", 3f);
        SetInt(serialized, "pointsPerLevel", 10);
        SetInt(serialized, "startingDaoLevel", 1);
        SetBool(serialized, "canSwitchAnywhere", false);
        SetBool(serialized, "enableDemoCasting", true);
        SetBool(serialized, "demoDealsDamage", true);
        SetBool(serialized, "persistBetweenSessions", true);
        SetBool(serialized, "showChineseNames", false);
        SetBool(serialized, "autoBootstrap", true);

        var startingDaos = serialized.FindProperty("startingUnlockedDaos");
        startingDaos.ClearArray();
        startingDaos.InsertArrayElementAtIndex(0);
        startingDaos.GetArrayElementAtIndex(0).enumValueIndex = (int)DaoType.Sword;
        startingDaos.InsertArrayElementAtIndex(1);
        startingDaos.GetArrayElementAtIndex(1).enumValueIndex = (int)DaoType.Fist;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        return config;
    }

    private static void SetObjectArray(SerializedProperty property, List<CultivationRealm> values)
    {
        if (property == null) return;
        property.ClearArray();
        for (int i = 0; i < values.Count; i++)
        {
            property.InsertArrayElementAtIndex(i);
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void SetObjectArray(SerializedProperty property, List<DaoPath> values)
    {
        if (property == null) return;
        property.ClearArray();
        for (int i = 0; i < values.Count; i++)
        {
            property.InsertArrayElementAtIndex(i);
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void SetInt(SerializedObject serialized, string name, int value)
    {
        var property = serialized.FindProperty(name);
        if (property != null) property.intValue = value;
    }

    private static void SetFloat(SerializedObject serialized, string name, float value)
    {
        var property = serialized.FindProperty(name);
        if (property != null) property.floatValue = value;
    }

    private static void SetBool(SerializedObject serialized, string name, bool value)
    {
        var property = serialized.FindProperty(name);
        if (property != null) property.boolValue = value;
    }

    private static T CreateOrLoad<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(parent)) return;
        parent = parent.Replace("\\", "/");

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static string Sanitize(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return value.Replace(' ', '_');
    }

    private static int CountTechniques(List<DaoPath> daoPaths)
    {
        int count = 0;
        for (int i = 0; i < daoPaths.Count; i++)
        {
            if (daoPaths[i] != null && daoPaths[i].Techniques != null)
                count += daoPaths[i].Techniques.Count;
        }
        return count;
    }
}
#endif
