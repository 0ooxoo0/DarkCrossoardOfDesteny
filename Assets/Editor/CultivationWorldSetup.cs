using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Контент мира культивации: ресурсы и пилюли в Assets/Scripts/Inventary/Data/Content.
/// Меню: Tools → Cultivation → 4. Setup World Content (Game).
/// Повторный запуск обновляет существующие ассеты, а не создаёт дубли.
/// </summary>
public static class CultivationWorldSetup
{
    private const string ContentFolder = "Assets/Scripts/Inventary/Data/Content";
    private const string PillsFolder = ContentFolder + "/Pills";
    private const string LootFolder = "Assets/Scripts/Inventary/Data/Loot";
    private const string ResourcesFolder = "Assets/Resources/Cultivation";
    private const string AlchemyConfigPath = ResourcesFolder + "/AlchemyConfig.asset";
    private const string AlchemyRecipesFolder = ResourcesFolder + "/Alchemy";
    private const string RiskConfigPath = ResourcesFolder + "/MeditationRiskConfig.asset";
    private const string VfxConfigPath = ResourcesFolder + "/CultivationVfxConfig.asset";
    private const string GameScenePath = "Assets/Scenes/Game.unity";
    private const string AnimalPrefabFolder = "Assets/Prefabs/Animals";
    private const string ProximityTriggerName = "CultivationProximity";
    private const float ProximityRadius = 0.6f;
    private const float ProximityHeight = 1f;
    private const string CraftingTablePath =
        "Assets/Unity Technologies/Daniel Mistage/Stylized Fantasy Armory/Prefabs/" +
        "Extra Content/Fantasy Workshops and Crafting Vol2/SFWC2_Crafting_Table.prefab";
    private const string MagicFolder = "Assets/Magic/";

    private const int AnimalMaxHealth = 30;
    private const int AnimalCount = 3;
    private const float StationOffset = 4f;
    private const float GroundProbeHeight = 30f;
    private const float GroundProbeDepth = 80f;
    private const string IconsFolder =
        "Assets/Ideals/Menu/MenuSaves/Brackeys/2D Mega Pack/Items & Icons/";

    private const int DataVersion = 2;

    // [PLACEHOLDER] Иконки взяты из готового набора ассетов; часть названий не совпадает
    // с лором (цветы заменены сердцем, трава — древесиной). Заменить на финальные арты.
    private const string WoodIconPath = IconsFolder + "Pixel Art/Wood.png";
    private const string SpiritIconPath = IconsFolder + "Pixel Art/MagicEssence.png";
    private const string HeartIconPath = IconsFolder + "Pixel Art/Heart.png";
    private const string IronIconPath = IconsFolder + "Pixel Art/Iron.png";
    private const string DiamondIconPath = IconsFolder + "Pixel Art/Diamond.png";
    private const string BeastCoreIconPath = IconsFolder + "Pixel Art/Magic Egg.png";
    private const string GoldenEssenceIconPath = IconsFolder + "Gothic/SoulFragment.png";
    private const string PeltIconPath = IconsFolder + "Pixel Art/Wool.png";
    private const string MeatIconPath = IconsFolder + "Pixel Art/Meat.png";
    private const string PillIconPath = IconsFolder + "Pixel Art/Potion.png";

    private static readonly string[] QualitySuffixes = { "Normal", "Veins", "Cloud" };
    private static readonly string[] QualityNames = { "обычная", "с жилками", "облачная" };

    /// <summary>Создаёт ресурсы и пилюли. Идемпотентно.</summary>
    [MenuItem("Tools/Cultivation/4. Setup World Content (Game)", priority = 4)]
    public static void SetupWorldContent()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Cultivation", "Выйдите из Play mode перед запуском.", "OK");
            return;
        }

        EnsureFolder(PillsFolder);
        EnsureFolder(LootFolder);
        EnsureItems();
        EnsureLootTables();
        EnsureAlchemy();

        if (!OpenGameScene()) return;

        EnsureWorldObjects();
        BindLootTables();
        EnsurePauseMenuButtons();

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CultivationSetup] Контент мира и сцена Game обновлены.");
    }

    /// <summary>Открывает Game.unity, если она ещё не открыта.</summary>
    private static bool OpenGameScene()
    {
        if (SceneManager.GetActiveScene().path == GameScenePath) return true;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        return true;
    }

    private static void EnsureItems()
    {
        Sprite wood = LoadSprite(WoodIconPath);
        Sprite spirit = LoadSprite(SpiritIconPath);
        Sprite heart = LoadSprite(HeartIconPath);
        Sprite iron = LoadSprite(IronIconPath);
        Sprite diamond = LoadSprite(DiamondIconPath);
        Sprite beastCore = LoadSprite(BeastCoreIconPath);
        Sprite goldenEssence = LoadSprite(GoldenEssenceIconPath);
        Sprite pelt = LoadSprite(PeltIconPath);
        Sprite meat = LoadSprite(MeatIconPath);
        Sprite pill = LoadSprite(PillIconPath);

        CreateResource("Herb_Mortal", "res.herb.mortal", "Трава смертного",
            "Обычная горная трава. Основа большинства пилюль.",
            wood, 99, 2, "herb.mortal");
        CreateResource("Herb_Spirit", "res.herb.spirit", "Духовная трава",
            "Трава, впитавшая духовную энергию. Ценится алхимиками.",
            spirit, 99, 6, "herb.spirit");
        CreateResource("Herb_Blood", "res.herb.blood", "Кровавый цветок",
            "Алый цветок с терпким запахом. Ускоряет кровоток.",
            heart, 99, 8, "herb.blood");
        CreateResource("Ore_Iron", "res.ore.iron", "Железная руда",
            "Тяжёлый кусок руды. Годится для ковки.",
            iron, 99, 3, "ore.iron");
        CreateResource("Ore_Jade", "res.ore.jade", "Нефритовая руда",
            "Зеленоватый камень с мягким свечением.",
            diamond, 99, 15, "ore.jade");
        CreateResource("Core_Beast", "res.core.beast", "Ядро зверя",
            "Сгусток силы убитого зверя. Пульсирует теплом.",
            beastCore, 99, 20, "core.beast");
        CreateResource("Essence_Golden", "res.essence.golden", "Золотая эссенция",
            "Редкая субстанция, сияющая золотом. Нужна для великих прорывов.",
            goldenEssence, 99, 60, "essence.golden");
        CreateResource("Wood_Spirit", "res.wood.spirit", "Духовная древесина",
            "Древесина древнего дерева. Не гниёт и не горит.",
            wood, 99, 5);
        CreateResource("Pelt", "res.pelt", "Шкура",
            "Выделанная шкура животного. Пахнет дымом.",
            pelt, 99, 4);
        CreateResource("Meat_Raw", "res.meat.raw", "Сырое мясо",
            "Свежее мясо. Можно съесть, а можно пустить в пилюли.",
            meat, 99, 2);

        CreatePillSet("Pill_QiReturn", "pill.qi.return", "Пилюля возврата Ци",
            "Восстанавливает запас Ци.",
            pill, new[] { 40, 70, 110 }, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }, new[] { 0, 0, 0 });
        CreatePillSet("Pill_BoneMarrow", "pill.bone.marrow", "Пилюля очищения костного мозга",
            "Прибавляет опыт Ци текущей ступени.",
            pill, new[] { 0, 0, 0 }, new[] { 60, 110, 180 }, new[] { 0, 0, 0 }, new[] { 0, 0, 0 });
        CreatePillSet("Pill_Enlightenment", "pill.enlightenment", "Пилюля озарения",
            "Дарует Прозрение.",
            pill, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }, new[] { 1, 2, 3 }, new[] { 0, 0, 0 });
        CreatePillSet("Pill_Healing", "pill.healing", "Пилюля лечения",
            "Закрывает раны и возвращает здоровье.",
            pill, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }, new[] { 40, 70, 110 });

        CreateBreakthroughPill(pill);
    }

    private static void EnsureLootTables()
    {
        ItemData woodSpirit = LoadItem("Wood_Spirit");
        ItemData oreIron = LoadItem("Ore_Iron");
        ItemData oreJade = LoadItem("Ore_Jade");
        ItemData herbMortal = LoadItem("Herb_Mortal");
        ItemData herbSpirit = LoadItem("Herb_Spirit");
        ItemData goldenEssence = LoadItem("Essence_Golden");
        ItemData meat = LoadItem("Meat_Raw");
        ItemData pelt = LoadItem("Pelt");

        LootTable tree = CreateOrLoadTable("Loot_Tree");
        FillTable(tree, new List<LootRoll>
        {
            Roll(woodSpirit, 1, 3, 1f, true)
        });

        LootTable rock = CreateOrLoadTable("Loot_Rock");
        FillTable(rock, new List<LootRoll>
        {
            Roll(oreIron, 1, 2, 1f, true),
            Roll(oreJade, 1, 1, 0.1f)
        });

        LootTable plant = CreateOrLoadTable("Loot_Plant");
        FillTable(plant, new List<LootRoll>
        {
            Roll(herbMortal, 1, 2, 1f, true),
            Roll(herbSpirit, 1, 1, 0.2f)
        });

        LootTable enemy = CreateOrLoadTable("Loot_Enemy");
        FillTable(enemy, new List<LootRoll>
        {
            Roll(oreIron, 1, 2, 0.6f),
            Roll(herbMortal, 1, 2, 0.4f),
            Roll(goldenEssence, 1, 1, 0.05f)
        });

        LootTable animal = CreateOrLoadTable("Loot_Animal");
        FillTable(animal, new List<LootRoll>
        {
            Roll(meat, 1, 2, 1f, true),
            Roll(pelt, 1, 1, 0.6f)
        });

        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Привязывает таблицы к объектам открытой сцены. Деревья, камни и растения
    /// могут использовать старый <see cref="DestructibleObject"/> — для них
    /// отсутствие <see cref="HarvestableObject"/> не предупреждение, а заметка.
    /// </summary>
    private static void BindLootTables()
    {
        BindLootTable("Tree", "Loot_Tree", false);
        BindLootTable("Rock", "Loot_Rock", false);
        BindLootTable("Plant", "Loot_Plant", false);
        BindLootTable("Enemy", "Loot_Enemy");
        BindLootTable(AnimalObjectNames, "Loot_Animal");
    }

    /// <summary>Животные: в сцене встречаются и «Horse», и созданный сетапом «Animal_Horse».</summary>
    private static readonly string[] AnimalObjectNames =
    {
        "Horse", "Pig", "Sheep",
        "Animal_Horse", "Animal_Pig", "Animal_Sheep"
    };

    private static void BindLootTable(string objectName, string tableFileName, bool required = true)
    {
        BindLootTable(new[] { objectName }, tableFileName, required);
    }

    private static void BindLootTable(string[] objectNames, string tableFileName, bool required = true)
    {
        LootTable table = AssetDatabase.LoadAssetAtPath<LootTable>(LootFolder + "/" + tableFileName + ".asset");
        if (table == null)
        {
            Debug.LogWarning("[CultivationSetup] Нет таблицы лута " + tableFileName + " — привязка пропущена.");
            return;
        }

        HarvestableObject[] targets = UnityEngine.Object.FindObjectsByType<HarvestableObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        bool bound = false;
        for (int i = 0; i < targets.Length; i++)
        {
            HarvestableObject target = targets[i];
            if (target == null) continue;

            bool matches = false;
            for (int n = 0; n < objectNames.Length; n++)
            {
                if (!MatchesName(target.gameObject.name, objectNames[n])) continue;
                matches = true;
                break;
            }

            if (!matches) continue;

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty("lootTable");
            if (property == null)
            {
                Debug.LogWarning("[CultivationSetup] У " + target.name + " нет поля lootTable.");
                continue;
            }

            property.objectReferenceValue = table;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            bound = true;
        }

        if (bound) return;

        string missing = "[CultivationSetup] В сцене нет объекта " + string.Join("/", objectNames) +
                         " — таблица " + tableFileName + " не привязана.";

        if (required) Debug.LogWarning(missing);
        else Debug.Log(missing);
    }

    private static bool MatchesName(string candidate, string target)
    {
        if (string.Equals(candidate, target, System.StringComparison.Ordinal)) return true;
        if (candidate.StartsWith(target + " ", System.StringComparison.Ordinal)) return true;
        return candidate.StartsWith(target + "(", System.StringComparison.Ordinal);
    }

    private static LootRoll Roll(ItemData item, int min, int max, float chance, bool guaranteed = false)
    {
        if (item == null)
            Debug.LogWarning("[CultivationSetup] Строка лута без предмета пропущена: сначала выполните создание предметов.");

        return new LootRoll
        {
            item = item,
            min = Mathf.Max(1, min),
            max = Mathf.Max(min, max),
            chance = Mathf.Clamp01(chance),
            guaranteed = guaranteed
        };
    }

    private static readonly string[] PauseMenuButtonNames =
    {
        "Культивация", "Техники", "Крафт", "Журнал"
    };

    /// <summary>Добавляет кнопки в PauseMenu/Кнопки, не создавая дубли.</summary>
    private static void EnsurePauseMenuButtons()
    {
        Transform container = FindPauseButtonsContainer();
        if (container == null)
        {
            Debug.LogWarning("[CultivationSetup] Не найдена панель PauseMenu/Кнопки — кнопки меню не созданы.");
            return;
        }

        var template = FindTemplateButton(container);
        if (template == null)
        {
            Debug.LogWarning("[CultivationSetup] В PauseMenu/Кнопки нет кнопки-образца.");
            return;
        }

        var templateRect = (RectTransform)template.transform;
        float step = templateRect.rect.height + 12f;

        // Новые кнопки ставим ниже самой нижней существующей.
        float lowestY = templateRect.anchoredPosition.y;
        for (int i = 0; i < container.childCount; i++)
        {
            if (container.GetChild(i) is RectTransform child && child.GetComponent<Button>() != null)
                lowestY = Mathf.Min(lowestY, child.anchoredPosition.y);
        }

        for (int i = 0; i < PauseMenuButtonNames.Length; i++)
        {
            string buttonName = PauseMenuButtonNames[i];
            if (container.Find(buttonName) != null) continue;

            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, container);
            clone.name = buttonName;

            var rect = (RectTransform)clone.transform;
            rect.anchoredPosition = new Vector2(templateRect.anchoredPosition.x, lowestY - step * (i + 1));

            TextMeshProUGUI label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = buttonName;

            EditorUtility.SetDirty(clone);
            EditorSceneManager.MarkSceneDirty(clone.scene);
        }
    }

    private static Transform FindPauseButtonsContainer()
    {
        Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate.name != "Кнопки" || !candidate.gameObject.scene.IsValid()) continue;

            Transform parent = candidate.parent;
            while (parent != null)
            {
                if (parent.name == "PauseMenu") return candidate;
                parent = parent.parent;
            }
        }

        return null;
    }

    private static Button FindTemplateButton(Transform container)
    {
        Button[] buttons = container.GetComponentsInChildren<Button>(true);
        return buttons.Length > 0 ? buttons[0] : null;
    }

    private static LootTable CreateOrLoadTable(string fileName)
    {
        string path = LootFolder + "/" + fileName + ".asset";
        LootTable table = AssetDatabase.LoadAssetAtPath<LootTable>(path);
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<LootTable>();
            AssetDatabase.CreateAsset(table, path);
        }

        return table;
    }

    private static void FillTable(LootTable table, List<LootRoll> rolls)
    {
        var serialized = new SerializedObject(table);
        SerializedProperty list = serialized.FindProperty("rolls");
        if (list == null)
        {
            Debug.LogWarning("[CultivationSetup] У таблицы " + table.name + " нет поля rolls.");
            return;
        }

        list.ClearArray();
        int count = 0;
        for (int i = 0; i < rolls.Count; i++)
        {
            // Строки без предмета не пишем: иначе таблица навсегда останется с пустыми ссылками.
            if (rolls[i] == null || rolls[i].item == null) continue;

            list.InsertArrayElementAtIndex(count);
            SerializedProperty element = list.GetArrayElementAtIndex(count);
            element.FindPropertyRelative("item").objectReferenceValue = rolls[i].item;
            element.FindPropertyRelative("min").intValue = rolls[i].min;
            element.FindPropertyRelative("max").intValue = rolls[i].max;
            element.FindPropertyRelative("chance").floatValue = rolls[i].chance;
            element.FindPropertyRelative("guaranteed").boolValue = rolls[i].guaranteed;
            count++;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(table);
    }

    private static ItemData LoadItem(string fileName)
    {
        string path = ContentFolder + "/" + fileName + ".asset";
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
            Debug.LogWarning("[CultivationSetup] Не найден предмет " + path + " — строка лута будет пропущена.");
        return item;
    }

    private static void EnsureAlchemy()
    {
        EnsureFolder(AlchemyRecipesFolder);

        AlchemyConfig config = AssetDatabase.LoadAssetAtPath<AlchemyConfig>(AlchemyConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<AlchemyConfig>();
            AssetDatabase.CreateAsset(config, AlchemyConfigPath);
        }

        EditorUtility.SetDirty(config);

        var recipes = new List<AlchemyRecipe>
        {
            CreateRecipe("Recipe_QiReturn", "recipe.qi.return", "Пилюля возврата Ци",
                "Простая пилюля, возвращающая запас Ци.",
                "Pill_QiReturn", AlchemyGradeName.Common, AlchemySubRank.Low,
                1, 0, 0.6f, true, 5f, 5,
                Ingredient("Herb_Mortal", 2), Ingredient("Herb_Spirit", 1)),

            CreateRecipe("Recipe_BoneMarrow", "recipe.bone.marrow", "Пилюля очищения костного мозга",
                "Прочищает каналы и прибавляет опыт Ци.",
                "Pill_BoneMarrow", AlchemyGradeName.Common, AlchemySubRank.Middle,
                2, 0, 0.5f, false, 8f, 8,
                Ingredient("Herb_Mortal", 2), Ingredient("Core_Beast", 1)),

            CreateRecipe("Recipe_Enlightenment", "recipe.enlightenment", "Пилюля озарения",
                "Дарует Прозрение. Требует духовных трав.",
                "Pill_Enlightenment", AlchemyGradeName.Earth, AlchemySubRank.Low,
                3, 1, 0.45f, false, 10f, 12,
                Ingredient("Herb_Spirit", 1), Ingredient("Essence_Golden", 1)),

            CreateRecipe("Recipe_Healing", "recipe.healing", "Пилюля лечения",
                "Закрывает раны. Варится даже в походе.",
                "Pill_Healing", AlchemyGradeName.Common, AlchemySubRank.Low,
                1, 0, 0.6f, true, 5f, 5,
                Ingredient("Herb_Mortal", 1), Ingredient("Meat_Raw", 1)),

            CreateRecipe("Recipe_Breakthrough", "recipe.breakthrough", "Пилюля прорыва",
                "Заменяет ядро зверя при прорыве.",
                "Pill_Breakthrough", AlchemyGradeName.Earth, AlchemySubRank.Middle,
                4, 1, 0.4f, false, 12f, 15,
                Ingredient("Herb_Spirit", 1), Ingredient("Ore_Jade", 1), Ingredient("Core_Beast", 1))
        };

        AssignAlchemyToConfig(config, recipes);
        AssetDatabase.SaveAssets();
    }

    private static void AssignAlchemyToConfig(AlchemyConfig config, List<AlchemyRecipe> recipes)
    {
        CultivationConfig cultivationConfig = AssetDatabase.LoadAssetAtPath<CultivationConfig>(
            ResourcesFolder + "/CultivationConfig.asset");

        if (cultivationConfig == null)
        {
            Debug.LogWarning("[CultivationSetup] Нет CultivationConfig — рецепты не прописаны в конфиг.");
            return;
        }

        var serialized = new SerializedObject(cultivationConfig);
        serialized.FindProperty("alchemyConfig").objectReferenceValue = config;

        SerializedProperty list = serialized.FindProperty("alchemyRecipes");
        if (list != null)
        {
            list.ClearArray();
            for (int i = 0; i < recipes.Count; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cultivationConfig);
    }

    private static AlchemyRecipe CreateRecipe(string fileName, string recipeId, string displayName,
        string description, string outputBaseName, AlchemyGradeName grade, AlchemySubRank subRank,
        int requiredDaoLevel, int requiredRealmIndex, float baseChance, bool portable,
        float craftSeconds, int masteryReward, params IngredientSpec[] ingredients)
    {
        string path = AlchemyRecipesFolder + "/" + fileName + ".asset";
        AlchemyRecipe recipe = AssetDatabase.LoadAssetAtPath<AlchemyRecipe>(path);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<AlchemyRecipe>();
            AssetDatabase.CreateAsset(recipe, path);
        }

        var serialized = new SerializedObject(recipe);
        serialized.FindProperty("recipeId").stringValue = recipeId;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("output").objectReferenceValue = LoadPillVariant(outputBaseName, "_Normal");
        serialized.FindProperty("veinsOutput").objectReferenceValue = LoadPillVariant(outputBaseName, "_Veins");
        serialized.FindProperty("cloudOutput").objectReferenceValue = LoadPillVariant(outputBaseName, "_Cloud");
        serialized.FindProperty("outputCount").intValue = 1;
        serialized.FindProperty("requiredGrade.gradeName").enumValueIndex = (int)grade;
        serialized.FindProperty("requiredGrade.subRank").enumValueIndex = (int)subRank;
        serialized.FindProperty("requiredDaoLevel").intValue = Mathf.Max(0, requiredDaoLevel);
        serialized.FindProperty("requiredRealmIndex").intValue = Mathf.Max(0, requiredRealmIndex);
        serialized.FindProperty("baseSuccessChance").floatValue = Mathf.Clamp01(baseChance);
        serialized.FindProperty("portableAllowed").boolValue = portable;
        serialized.FindProperty("craftSeconds").floatValue = Mathf.Max(0f, craftSeconds);
        serialized.FindProperty("masteryReward").intValue = Mathf.Max(0, masteryReward);

        SerializedProperty list = serialized.FindProperty("ingredients");
        if (list != null)
        {
            list.ClearArray();
            for (int i = 0; i < ingredients.Length; i++)
            {
                list.InsertArrayElementAtIndex(i);
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("item").objectReferenceValue = LoadItem(ingredients[i].FileName);
                element.FindPropertyRelative("count").intValue = Mathf.Max(1, ingredients[i].Count);
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);
        return recipe;
    }

    private static ItemData LoadPillVariant(string baseName, string suffix)
    {
        return AssetDatabase.LoadAssetAtPath<ItemData>(PillsFolder + "/" + baseName + suffix + ".asset");
    }

    private static IngredientSpec Ingredient(string fileName, int count)
    {
        return new IngredientSpec(fileName, count);
    }

    private readonly struct IngredientSpec
    {
        public readonly string FileName;
        public readonly int Count;

        public IngredientSpec(string fileName, int count)
        {
            FileName = fileName;
            Count = count;
        }
    }

    // ------------------------------------------------------------- Мир и сцена

    /// <summary>Наполняет сцену: алтарь, враги, животные, печь и конфиги VFX/риска.</summary>
    private static void EnsureWorldObjects()
    {
        EnsureAltar();
        EnsureEnemyAi();
        EnsureAnimals();
        EnsureCraftingStation();
        EnsurePlayerProximityTrigger();
        EnsureRiskAndVfxConfigs();
    }

    /// <summary>
    /// У игрока все коллайдеры выключены до атаки, поэтому триггеры алтаря и зоны
    /// медитации не срабатывают. Отдельный дочерний триггер решает это, ничего
    /// не включая в боевых коллайдерах.
    /// </summary>
    private static void EnsurePlayerProximityTrigger()
    {
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("[CultivationSetup] Нет PlayerController — триггер близости не создан.");
            return;
        }

        Transform existing = player.transform.Find(ProximityTriggerName);
        if (existing != null) return;

        var trigger = new GameObject(ProximityTriggerName);
        trigger.transform.SetParent(player.transform, false);
        trigger.transform.localPosition = new Vector3(0f, ProximityHeight, 0f);

        var sphere = trigger.AddComponent<SphereCollider>();
        sphere.radius = ProximityRadius;
        sphere.isTrigger = true;
        sphere.center = Vector3.zero;

        EditorUtility.SetDirty(trigger);
    }

    private static void EnsureAltar()
    {
        if (Object.FindFirstObjectByType<Altar>(FindObjectsInactive.Include) != null) return;

        var player = Object.FindFirstObjectByType<PlayerController>();
        Vector3 position = player != null
            ? GroundPoint(player.transform.position + player.transform.right * 3f)
            : new Vector3(3f, 0f, 3f);

        var altarGo = new GameObject("DaoAltar", typeof(BoxCollider), typeof(Altar));
        altarGo.transform.position = position;

        var trigger = altarGo.GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1f, 0f);
        trigger.size = new Vector3(1.8f, 2f, 1.8f);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "AltarVisual";
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(altarGo.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        visual.transform.localScale = new Vector3(0.7f, 0.6f, 0.7f);

        Debug.Log("[CultivationSetup] Создан DaoAltar в " + position + ".");
    }

    private static void EnsureEnemyAi()
    {
        HarvestableObject[] objects = Object.FindObjectsByType<HarvestableObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int added = 0;
        for (int i = 0; i < objects.Length; i++)
        {
            HarvestableObject candidate = objects[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid()) continue;
            if (!MatchesName(candidate.gameObject.name, "Enemy")) continue;
            if (candidate.GetComponent<EnemyAI>() != null) continue;

            candidate.gameObject.AddComponent<EnemyAI>();
            EditorUtility.SetDirty(candidate.gameObject);
            added++;
        }

        if (added == 0)
            Debug.Log("[CultivationSetup] EnemyAI уже стоит на врагах или врагов нет в сцене.");
    }

    private static void EnsureAnimals()
    {
        LootTable loot = AssetDatabase.LoadAssetAtPath<LootTable>(LootFolder + "/Loot_Animal.asset");
        if (loot == null)
        {
            Debug.LogWarning("[CultivationSetup] Нет Loot_Animal — животные останутся без добычи.");
        }

        string[] prefabNames = { "Horse Variant", "Pig Variant", "Sheep Variant" };
        string[] instanceNames = { "Animal_Horse", "Animal_Pig", "Animal_Sheep" };
        Vector3[] offsets =
        {
            new Vector3(8f, 0f, 3f),
            new Vector3(-7f, 0f, 5f),
            new Vector3(3f, 0f, -9f)
        };

        var player = Object.FindFirstObjectByType<PlayerController>();
        Vector3 center = player != null ? player.transform.position : Vector3.zero;

        for (int i = 0; i < prefabNames.Length && i < AnimalCount; i++)
        {
            string prefabPath = AnimalPrefabFolder + "/" + prefabNames[i] + ".prefab";
            PrepareAnimalPrefab(prefabPath, loot);

            if (FindSceneObject(instanceNames[i]) != null) continue;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[CultivationSetup] Не найден префаб " + prefabPath);
                continue;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = instanceNames[i];
            instance.transform.position = GroundPoint(center + offsets[i]);
            EditorUtility.SetDirty(instance);
        }
    }

    /// <summary>Готовит префаб животного: коллайдер, здоровье, добыча и побег.</summary>
    private static void PrepareAnimalPrefab(string path, LootTable loot)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        if (contents == null)
        {
            Debug.LogWarning("[CultivationSetup] Не удалось открыть префаб " + path);
            return;
        }

        try
        {
            if (contents.GetComponentInChildren<Collider>(true) == null &&
                TryComputeLocalBounds(contents, out Bounds bounds))
            {
                var box = contents.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }

            var harvestable = contents.GetComponent<HarvestableObject>();
            if (harvestable == null) harvestable = contents.AddComponent<HarvestableObject>();

            var serialized = new SerializedObject(harvestable);
            SerializedProperty health = serialized.FindProperty("maxHealth");
            if (health != null) health.floatValue = AnimalMaxHealth;

            SerializedProperty table = serialized.FindProperty("lootTable");
            if (table != null && loot != null) table.objectReferenceValue = loot;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (contents.GetComponent<AnimalAI>() == null)
                contents.AddComponent<AnimalAI>();

            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static bool TryComputeLocalBounds(GameObject root, out Bounds localBounds)
    {
        localBounds = new Bounds(Vector3.zero, Vector3.zero);

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds worldBounds = new Bounds(root.transform.position, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled) continue;

            if (!hasBounds)
            {
                worldBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                worldBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds) return false;

        Vector3 scale = root.transform.lossyScale;
        scale = new Vector3(Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            Mathf.Max(0.0001f, Mathf.Abs(scale.z)));

        localBounds = new Bounds(root.transform.InverseTransformPoint(worldBounds.center),
            new Vector3(worldBounds.size.x / scale.x, worldBounds.size.y / scale.y,
                worldBounds.size.z / scale.z));
        return true;
    }

    private static void EnsureCraftingStation()
    {
        GameObject existing = FindSceneObject("SFWC2_Crafting_Table");

        if (existing == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CraftingTablePath);
            if (prefab == null)
            {
                Debug.LogWarning("[CultivationSetup] Не найден префаб печи: " + CraftingTablePath);
                return;
            }

            existing = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            existing.name = "SFWC2_Crafting_Table";
            existing.transform.position = FindStationPosition();
            EditorUtility.SetDirty(existing);
        }

        var station = existing.GetComponent<AlchemyStation>();
        if (station == null) station = existing.AddComponent<AlchemyStation>();

        AlchemyConfig config = AssetDatabase.LoadAssetAtPath<AlchemyConfig>(AlchemyConfigPath);
        CultivationConfig cultivationConfig = AssetDatabase.LoadAssetAtPath<CultivationConfig>(
            ResourcesFolder + "/CultivationConfig.asset");

        var serialized = new SerializedObject(station);
        SerializedProperty configProperty = serialized.FindProperty("config");
        if (configProperty != null) configProperty.objectReferenceValue = config;

        SerializedProperty recipesProperty = serialized.FindProperty("recipes");
        if (recipesProperty != null && cultivationConfig != null)
        {
            recipesProperty.ClearArray();
            IReadOnlyList<AlchemyRecipe> recipes = cultivationConfig.AlchemyRecipes;
            for (int i = 0; i < recipes.Count; i++)
            {
                recipesProperty.InsertArrayElementAtIndex(i);
                recipesProperty.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(station);
    }

    private static Vector3 FindStationPosition()
    {
        GameObject smithy = FindSceneObject("Smithy");

        if (smithy != null)
        {
            Vector3 position = smithy.transform.position;
            Vector3 right = smithy.transform.right;
            Vector3 forward = smithy.transform.forward;

            for (int i = 0; i < 4; i++)
            {
                Vector3 direction = i == 0 ? right : i == 1 ? forward : i == 2 ? -right : -forward;
                Vector3 candidate = GroundPoint(position + direction * StationOffset, smithy.transform);
                if (Mathf.Abs(candidate.y - position.y) < StationOffset * 2f) return candidate;
            }

            return position;
        }

        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
            return GroundPoint(player.transform.position + player.transform.right * StationOffset);

        return Vector3.zero;
    }

    /// <summary>Создаёт конфиги риска медитации и VFX и прописывает их в CultivationConfig.</summary>
    private static void EnsureRiskAndVfxConfigs()
    {
        CultivationConfig cultivationConfig = AssetDatabase.LoadAssetAtPath<CultivationConfig>(
            ResourcesFolder + "/CultivationConfig.asset");

        if (cultivationConfig == null)
        {
            Debug.LogWarning("[CultivationSetup] Нет CultivationConfig — конфиги риска и VFX не прописаны.");
            return;
        }

        MeditationRiskConfig risk = LoadOrCreateAsset<MeditationRiskConfig>(RiskConfigPath);
        CultivationVfxConfig vfx = LoadOrCreateAsset<CultivationVfxConfig>(VfxConfigPath);

        EditorUtility.SetDirty(risk);
        EditorUtility.SetDirty(vfx);

        AssignVfx(vfx);

        var serialized = new SerializedObject(cultivationConfig);
        serialized.FindProperty("meditationRisk").objectReferenceValue = risk;
        serialized.FindProperty("vfxConfig").objectReferenceValue = vfx;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cultivationConfig);
    }

    private static void AssignVfx(CultivationVfxConfig vfx)
    {
        var serialized = new SerializedObject(vfx);

        AssignPrefab(serialized, "auraPrefab", MagicFolder + "PersonAura/Holy Aura.prefab");
        AssignPrefab(serialized, "meditationLoopPrefab", MagicFolder + "otherAura/Green Area.prefab");
        AssignPrefab(serialized, "breakthroughStartPrefab",
            MagicFolder + "otherAura/Blue Area + Distort Sphere.prefab");
        AssignPrefab(serialized, "breakthroughSuccessPrefab", MagicFolder + "2Dammage/Explosion Galaxy.prefab");
        AssignPrefab(serialized, "breakthroughFailPrefab", MagicFolder + "Thick Smoke.prefab");
        AssignPrefab(serialized, "qiSurgePrefab", MagicFolder + "1Dammage/Blue Tornado.prefab");
        AssignPrefab(serialized, "qiDeviationPrefab", MagicFolder + "PersonAura/Dark Magic Orb.prefab");
        AssignPrefab(serialized, "severingPrefab", MagicFolder + "otherAura/Red Area.prefab");
        AssignPrefab(serialized, "deathPrefab", MagicFolder + "Thick Smoke.prefab");

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignPrefab(SerializedObject serialized, string propertyName, string assetPath)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null) return;

        property.objectReferenceValue = LoadVfxPrefab(assetPath);
    }

    private static GameObject LoadVfxPrefab(string assetPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
            Debug.LogWarning("[CultivationSetup] Не найден VFX: " + assetPath);

        return prefab;
    }

    private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static Vector3 GroundPoint(Vector3 origin)
    {
        return GroundPoint(origin, null);
    }

    /// <summary>Ищет землю под точкой, пропуская коллайдеры указанного объекта.</summary>
    private static Vector3 GroundPoint(Vector3 origin, Transform avoid)
    {
        Vector3 from = new Vector3(origin.x, origin.y + GroundProbeHeight, origin.z);
        RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, GroundProbeDepth, ~0,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float lowest = float.MaxValue;
        Vector3 point = origin;

        for (int i = 0; i < hits.Length; i++)
        {
            if (avoid != null && hits[i].transform != null && hits[i].transform.IsChildOf(avoid)) continue;

            if (!found || hits[i].point.y < lowest)
            {
                lowest = hits[i].point.y;
                point = hits[i].point;
                found = true;
            }
        }

        return found ? point : origin;
    }

    private static GameObject FindSceneObject(string name)
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate != null && candidate.name == name && candidate.scene.IsValid()) return candidate;
        }

        return null;
    }

    private static void CreateResource(string fileName, string itemId, string displayName, string description,
        Sprite icon, int maxStack, int sellPrice, params string[] tags)
    {
        ItemData item = CreateOrUpdate(ContentFolder + "/" + fileName + ".asset", itemId, displayName, description);
        item.type = ItemType.Misc;
        item.useKind = ItemUseKind.None;
        item.stackable = maxStack > 1;
        item.maxStackSize = Mathf.Max(1, maxStack);
        item.sellPrice = Mathf.Max(0, sellPrice);
        item.icon = icon;
        item.breakthroughTags = new List<string>(tags ?? new string[0]);
        MarkDirty(item);
    }

    /// <summary>Три варианта качества: обычная, с жилками, облачная.</summary>
    private static void CreatePillSet(string fileName, string itemId, string displayName, string description,
        Sprite icon, int[] qiValues, int[] experienceValues, int[] comprehensionValues, int[] healValues)
    {
        for (int quality = 0; quality < QualitySuffixes.Length; quality++)
        {
            string suffix = QualitySuffixes[quality];
            ItemData item = CreateOrUpdate(
                PillsFolder + "/" + fileName + "_" + suffix + ".asset",
                itemId + "." + suffix.ToLowerInvariant(),
                displayName + " (" + QualityNames[quality] + ")",
                description);

            item.type = ItemType.Consumable;
            item.useKind = ItemUseKind.Pill;
            item.stackable = true;
            item.maxStackSize = 20;
            item.sellPrice = 5 + quality * 15;
            item.icon = icon;
            item.breakthroughTags = new List<string>();
            item.qiRestore = ValueAt(qiValues, quality);
            item.experienceQiGain = ValueAt(experienceValues, quality);
            item.comprehensionGain = ValueAt(comprehensionValues, quality);
            item.healAmount = ValueAt(healValues, quality);
            MarkDirty(item);
        }
    }

    private static void CreateBreakthroughPill(Sprite icon)
    {
        for (int quality = 0; quality < QualitySuffixes.Length; quality++)
        {
            string suffix = QualitySuffixes[quality];
            ItemData item = CreateOrUpdate(
                PillsFolder + "/Pill_Breakthrough_" + suffix + ".asset",
                "pill.breakthrough." + suffix.ToLowerInvariant(),
                "Пилюля прорыва (" + QualityNames[quality] + ")",
                "Считается материалом прорыва: заменяет ядро зверя.");

            item.type = ItemType.Consumable;
            item.useKind = ItemUseKind.BreakthroughMaterial;
            item.stackable = true;
            item.maxStackSize = 10;
            item.sellPrice = 40 + quality * 30;
            item.icon = icon;
            item.breakthroughTags = new List<string> { "core.beast" };
            item.qiRestore = 0;
            item.experienceQiGain = 0;
            item.comprehensionGain = 0;
            item.healAmount = 0;
            MarkDirty(item);
        }
    }

    private static ItemData CreateOrUpdate(string assetPath, string itemId, string displayName, string description)
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, assetPath);
        }

        item.itemId = itemId;
        item.itemName = displayName;
        item.description = description;
        return item;
    }

    private static void MarkDirty(ItemData item)
    {
        item.MarkMigrated(DataVersion);
        EditorUtility.SetDirty(item);
    }

    private static int ValueAt(int[] values, int index)
    {
        if (values == null || index < 0 || index >= values.Length) return 0;
        return Mathf.Max(0, values[index]);
    }

    private static Sprite LoadSprite(string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
            Debug.LogWarning("[CultivationSetup] Не найден спрайт: " + assetPath + ". Останется [PLACEHOLDER].");
        return sprite;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(folder);

        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
