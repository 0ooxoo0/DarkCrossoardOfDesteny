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

        int totalTechniques = 0;
        int totalPassives = 0;

        if (config.DaoPaths != null)
        {
            foreach (var path in config.DaoPaths)
            {
                if (path == null) continue;

                int pathPassives = 0;
                var techniques = path.Techniques;
                if (techniques != null)
                {
                    for (int i = 0; i < techniques.Count; i++)
                    {
                        var technique = techniques[i];
                        if (technique == null) continue;
                        totalTechniques++;

                        if (technique.Kind != TechniqueKind.Passive) continue;

                        pathPassives++;
                        totalPassives++;
                        if (technique.PassiveValue <= 0f)
                        {
                            Debug.LogError("[Cultivation] Пассив " + technique.DisplayName + " без эффекта (passiveValue = 0).");
                            errors++;
                        }
                    }
                }

                if (techniques == null || techniques.Count < 3)
                {
                    Debug.LogError("[Cultivation] У пути " + path.DisplayName + " меньше 3 техник.");
                    errors++;
                }

                if (pathPassives < 1)
                {
                    Debug.LogError("[Cultivation] У пути " + path.DisplayName + " нет ни одного пассива.");
                    errors++;
                }
            }
        }

        if (totalTechniques < 20)
        {
            Debug.LogError("[Cultivation] Всего техник " + totalTechniques + ", нужно ≥ 20. Запустите Setup Test Assets.");
            errors++;
        }

        Debug.Log(errors == 0
            ? "[Cultivation] Проверка пройдена: " + totalTechniques + " техник, из них пассивов " + totalPassives + "."
            : "[Cultivation] Проверка нашла проблемы: " + errors);
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
                    BuildDescription(blueprint),
                    type, blueprint.Kind, blueprint.Rank, blueprint.RequiredDaoLevel, blueprint.RequiredRealmIndex,
                    blueprint.QiCost, blueprint.Cooldown, blueprint.Power, null, null, null);
                technique.SetPassive(blueprint.PassiveEffect, blueprint.PassiveValue);

                EditorUtility.SetDirty(technique);
                techniques.Add(technique);
            }

            daoPath.Setup(type, DaoTypeInfo.GetDisplayName(type), DaoTypeInfo.GetChineseName(type),
                "Путь " + type + ": техники открываются по уровню Дао и реалму.",
                DaoTypeInfo.GetColor(type), HighestDaoRequirement(techniques), null, techniques);

            EditorUtility.SetDirty(daoPath);
            paths.Add(daoPath);
        }

        return paths;
    }

    /// <summary>
    /// Уровни Дао 5–10 не имеют техник ранга 4+, поэтому школа «осваивается» на последней
    /// требующейся ступени — иначе игрок видел бы 6 уровней без награды.
    /// </summary>
    private static int HighestDaoRequirement(List<TechniqueData> techniques)
    {
        int highest = 1;
        for (int i = 0; i < techniques.Count; i++)
        {
            if (techniques[i] != null) highest = Mathf.Max(highest, techniques[i].RequiredDaoLevel);
        }
        return highest;
    }

    /// <summary>Механическое описание вместо заглушки: урон, радиус, цена, КД или эффект пассива.</summary>
    private static string BuildDescription(CultivationDefaults.TechniqueBlueprint blueprint)
    {
        string school = DaoTypeInfo.GetDisplayName(blueprint.Dao);

        if (blueprint.Kind == TechniqueKind.Passive)
        {
            int percent = Mathf.RoundToInt(Mathf.Abs(blueprint.PassiveValue) * 100f);
            string sign = blueprint.PassiveValue >= 0f ? "+" : "−";
            return "Пассив школы «" + school + "»: " + sign + percent + "% (" +
                   DescribePassiveEffect(blueprint.PassiveEffect) + "). Активируется при Дао " +
                   blueprint.RequiredDaoLevel + " ур. и реалме " + (blueprint.RequiredRealmIndex + 1) + ".";
        }

        switch (blueprint.Kind)
        {
            case TechniqueKind.Block:
                return "Стойка школы «" + school + "» (механика в разработке). Ци " + blueprint.QiCost +
                       ", КД " + blueprint.Cooldown.ToString("0.#") + " с.";
            case TechniqueKind.Aura:
                return "Аура школы «" + school + "» (механика в разработке). Ци " + blueprint.QiCost +
                       ", КД " + blueprint.Cooldown.ToString("0.#") + " с.";
        }

        bool melee = blueprint.Dao == DaoType.Sword || blueprint.Dao == DaoType.Fist || blueprint.Dao == DaoType.Body;
        return (melee ? "Ближний бой" : "Дальний бой") + " школы «" + school + "»: " + blueprint.Power +
               " урона" + (melee ? " по цели в зоне оружия" : " по области 2 м") + ", Ци " + blueprint.QiCost +
               ", КД " + blueprint.Cooldown.ToString("0.#") + " с.";
    }

    private static string DescribePassiveEffect(PassiveEffectKind kind)
    {
        switch (kind)
        {
            case PassiveEffectKind.MaxHealthPercent: return "максимум HP";
            case PassiveEffectKind.QiRegenPercent: return "реген Ци";
            case PassiveEffectKind.MaxQiPercent: return "максимум Ци";
            case PassiveEffectKind.SpellPowerPercent: return "сила техник";
            case PassiveEffectKind.MeleeDamagePercent: return "урон оружием";
            case PassiveEffectKind.DamageReductionPercent: return "получаемый урон";
            case PassiveEffectKind.ComprehensionGainPercent: return "Прозрение";
            case PassiveEffectKind.AlchemySuccessPercent: return "успех алхимии";
            default: return kind.ToString();
        }
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
