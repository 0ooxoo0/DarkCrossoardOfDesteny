#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Интеграция Промта A + Промт B в игровую сцену. Только добавление объектов и ссылок:
/// данные культивации (ассеты), тестовый свиток, Inventory + InventoryUI + панель,
/// SoundManager и алтарь Дао. Ни один существующий скрипт не переписывается.
/// Меню: Tools → Cultivation → 3. Integrate Inventory &amp; Altar (Game).
/// </summary>
public static class CultivationGameSceneSetup
{
    private const string GameScenePath = "Assets/Scenes/Game.unity";
    private const string ConfigPath = "Assets/Resources/Cultivation/CultivationConfig.asset";
    private const string ItemPrefabPath = "Assets/Inventary/Prefabs/Item.prefab";
    private const string CombatItemsFolder = "Assets/Combat/Items";
    private const string ScrollPath = CombatItemsFolder + "/Scroll_SwordStrike.asset";

    private static readonly Color PanelColor = new Color(0.05f, 0.055f, 0.07f, 0.98f);
    private static readonly Color WindowColor = new Color(0.07f, 0.075f, 0.095f, 0.96f);
    private static readonly Color Gold = new Color(0.78f, 0.64f, 0.31f, 1f);
    private static readonly Color TextMain = new Color(0.93f, 0.92f, 0.88f, 1f);
    private static readonly Color TextMuted = new Color(0.62f, 0.62f, 0.60f, 1f);

    [MenuItem("Tools/Cultivation/3. Integrate Inventory & Altar (Game)", priority = 3)]
    public static void Integrate()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Cultivation",
                "Выйдите из Play mode: интеграция меняет сцену и ассеты.", "OK");
            return;
        }

        SetupCultivationData();
        EnsureScrollItem();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        EnsureSoundManager();
        Inventory inventory = EnsureInventory();
        EnsureInventoryCanvas(inventory);
        EnsureAltar();
        WireSaveSystem(inventory);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Cultivation] Интеграция завершена: инвентарь и алтарь добавлены в Game.unity, " +
                  "свиток техники создан, баланс Ци 100 / 2 в сек. " +
                  "Проверьте позицию DaoAltar (поставлен справа от точки спавна игрока).");
    }

    // ---------------------------------------------------------------- данные

    private static void SetupCultivationData()
    {
        // Создаёт Resources/Cultivation/CultivationConfig + Realm/Dao/Technique ассеты.
        CultivationSetup.SetupTestAssets();

        var config = AssetDatabase.LoadAssetAtPath<CultivationConfig>(ConfigPath);
        if (config == null)
        {
            Debug.LogError("[Cultivation] Не удалось создать " + ConfigPath);
            return;
        }

        var serialized = new SerializedObject(config);
        SetFloat(serialized, "maxQi", 100f);
        SetFloat(serialized, "qiRegenPerSecond", 2f);
        // Смена Дао — только у алтаря: Altar.cs вызывает DaoSystem.SetNearAltar.
        SetBool(serialized, "canSwitchAnywhere", false);
        SetBool(serialized, "persistBetweenSessions", true);
        SetBool(serialized, "autoBootstrap", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("[Cultivation] Конфиг: Ци 100, реген 2/сек, смена Дао только у алтаря.");
    }

    private static ItemData EnsureScrollItem()
    {
        var existing = AssetDatabase.LoadAssetAtPath<ItemData>(ScrollPath);
        if (existing != null) return existing;

        TechniqueData technique = FindTechnique("sword_1");
        if (technique == null)
        {
            Debug.LogWarning("[Cultivation] Техника sword_1 не найдена — свиток не создан.");
            return null;
        }

        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemId = "scroll_sword_1";
        item.itemName = "Свиток: Удар клинка";
        item.description = "Изучает технику «Удар клинка» (Путь Меча) раньше уровня Дао.";
        item.type = ItemType.Scroll;
        item.stackable = true;
        item.maxStackSize = 5;
        item.consumable = true;
        item.technique = technique;

        AssetDatabase.CreateAsset(item, ScrollPath);
        EditorUtility.SetDirty(item);
        AssetDatabase.SaveAssets();
        Debug.Log("[Cultivation] Создан свиток: " + ScrollPath);
        return item;
    }

    private static TechniqueData FindTechnique(string techniqueId)
    {
        const string dataFolder = "Assets/Scripts/Cultivation/Data";
        if (!AssetDatabase.IsValidFolder(dataFolder)) return null;

        string[] guids = AssetDatabase.FindAssets("t:TechniqueData", new[] { dataFolder });
        foreach (string guid in guids)
        {
            var technique = AssetDatabase.LoadAssetAtPath<TechniqueData>(AssetDatabase.GUIDToAssetPath(guid));
            if (technique != null && technique.TechniqueId == techniqueId) return technique;
        }
        return null;
    }

    // ---------------------------------------------------------------- сцена

    private static void EnsureSoundManager()
    {
        if (Object.FindFirstObjectByType<SoundManager>(FindObjectsInactive.Include) != null) return;

        // InventorySlotUI зовёт SoundManager.Instance без проверки на null.
        var go = new GameObject("SoundManager", typeof(AudioSource), typeof(SoundManager));
        go.name = "SoundManager";
        Debug.Log("[Cultivation] Добавлен SoundManager (пустой список звуков — только чтобы не падало на hover).");
    }

    private static Inventory EnsureInventory()
    {
        Inventory existing = Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (existing != null) return existing;

        var go = new GameObject("Inventory", typeof(Inventory));
        return go.GetComponent<Inventory>();
    }

    private static void EnsureInventoryCanvas(Inventory inventory)
    {
        if (FindSceneObject("InventoryCanvas") != null)
        {
            Debug.Log("[Cultivation] InventoryCanvas уже есть в сцене — пропускаю.");
            return;
        }

        GameObject slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefabPath);
        if (slotPrefab == null)
        {
            Debug.LogError("[Cultivation] Не найден " + ItemPrefabPath + " — панель инвентаря не создана.");
            return;
        }

        var canvasGo = new GameObject("InventoryCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject root = CreateRect("InventoryRoot", canvasGo.transform);
        Stretch(root.GetComponent<RectTransform>());

        Image dim = CreateImage("Dim", root.transform, new Color(0f, 0f, 0f, 0.65f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var window = CreateImage("Window", root.transform, PanelColor);
        Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(1000f, 660f));

        TextMeshProUGUI title = CreateText("Title", window.transform, "ИНВЕНТАРЬ", 30f, Gold,
            TextAlignmentOptions.TopLeft);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(30f, -26f), new Vector2(420f, 40f));

        Button close = CreateButton("Close", window.transform, "×", new Vector2(44f, 44f));
        Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-18f, -18f), new Vector2(44f, 44f));

        TextMeshProUGUI description = CreateText("Description", window.transform, string.Empty, 15f, TextMain,
            TextAlignmentOptions.TopLeft);
        Place(description.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(30f, 48f), new Vector2(940f, 40f));

        TextMeshProUGUI hint = CreateText("Hint", window.transform,
            "I — закрыть  ·  двойной клик — использовать  ·  перетаскивание — переместить", 14f, TextMuted,
            TextAlignmentOptions.BottomLeft);
        Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(30f, 16f), new Vector2(940f, 24f));

        // --- сетка слотов
        GameObject grid = CreateRect("SlotGrid", window.transform);
        var gridRect = grid.GetComponent<RectTransform>();
        Place(gridRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(30f, -80f), new Vector2(620f, 500f));

        var layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(112f, 124f);
        layout.spacing = new Vector2(10f, 10f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 5;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.padding = new RectOffset(0, 0, 0, 0);

        // Слоты создаются в рантайме без ссылки на строку описания — связываем их здесь.
        var descriptionLink = grid.AddComponent<InventoryDescriptionLink>();
        Assign(descriptionLink, so => so.FindProperty("descriptionLabel").objectReferenceValue = description);

        // --- тестовые предметы
        GameObject dev = CreateRect("DevItems", window.transform);
        Place(dev.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-30f, -80f), new Vector2(320f, 500f));

        var devLayout = dev.AddComponent<VerticalLayoutGroup>();
        devLayout.spacing = 4f;
        devLayout.padding = new RectOffset(4, 4, 4, 4);
        devLayout.childAlignment = TextAnchor.UpperLeft;
        devLayout.childControlWidth = true;
        devLayout.childControlHeight = false;
        devLayout.childForceExpandWidth = true;
        devLayout.childForceExpandHeight = false;

        CreateText("DevTitle", dev.transform, "DEV: выдать предмет", 16f, Gold, TextAlignmentOptions.TopLeft)
            .rectTransform.sizeDelta = new Vector2(310f, 26f);

        List<ItemData> items = CollectItemAssets();
        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (item == null || item.type == ItemType.Weapon) continue;

            Button button = CreateButton("Give_" + item.itemId, dev.transform,
                item.itemName + " (" + item.type + ")", new Vector2(310f, 34f));
            AddLayoutElement(button.gameObject, 34f);
            UnityEventTools.AddObjectPersistentListener(button.onClick, inventory.AddTestItem, item);
        }

        // InventoryUI живёт на всегда активном объекте, чтобы слоты построились сразу.
        InventoryUI inventoryUI = inventory.gameObject.AddComponent<InventoryUI>();
        Assign(inventoryUI, so =>
        {
            so.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
            so.FindProperty("slotParent").objectReferenceValue = gridRect;
            so.FindProperty("inventory").objectReferenceValue = inventory;
        });

        // DragIcon обязателен: InventorySlotUI ищет его по имени вверх по иерархии
        // (тег DragIcon в проекте не объявлен, поэтому объект нужен явно).
        Image dragIcon = CreateImage("DragIcon", canvasGo.transform, new Color(1f, 1f, 1f, 0.92f));
        dragIcon.raycastTarget = false;
        Place(dragIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(96f, 96f));

        Image dragIconSprite = CreateImage("Icon", dragIcon.transform, Color.white);
        Stretch(dragIconSprite.rectTransform);
        dragIconSprite.raycastTarget = false;

        dragIcon.gameObject.AddComponent<CanvasGroup>();
        dragIcon.gameObject.SetActive(false);

        var toggle = canvasGo.AddComponent<InventoryPanelToggle>();
        Assign(toggle, so => so.FindProperty("panel").objectReferenceValue = root);
        UnityEventTools.AddBoolPersistentListener(close.onClick, toggle.SetOpen, false);

        // Панель спрятана до нажатия I.
        root.SetActive(false);

        Debug.Log("[Cultivation] Создана панель инвентаря (InventoryCanvas) + InventoryUI + DEV-кнопки выдачи предметов.");
    }

    private static void EnsureAltar()
    {
        if (Object.FindFirstObjectByType<Altar>(FindObjectsInactive.Include) != null)
        {
            Debug.Log("[Cultivation] Алтарь уже есть в сцене — пропускаю.");
            return;
        }

        var player = Object.FindFirstObjectByType<PlayerController>();
        Vector3 position = player != null
            ? player.transform.position + player.transform.right * 3f
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

        Debug.Log("[Cultivation] Алтарь добавлен в " + position + " — при необходимости передвиньте DaoAltar.");
    }

    private static void WireSaveSystem(Inventory inventory)
    {
        if (inventory == null) return;

        InventorySaveSystem save = Object.FindFirstObjectByType<InventorySaveSystem>(FindObjectsInactive.Include);
        if (save == null)
        {
            var go = new GameObject("InventorySaveSystem", typeof(InventorySaveSystem));
            save = go.GetComponent<InventorySaveSystem>();
        }

        List<ItemData> items = CollectItemAssets();

        Assign(save, so =>
        {
            so.FindProperty("inventory").objectReferenceValue = inventory;

            SerializedProperty allItems = so.FindProperty("allItems");
            allItems.ClearArray();
            for (int i = 0; i < items.Count; i++)
            {
                allItems.InsertArrayElementAtIndex(i);
                allItems.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        });

        Debug.Log("[Cultivation] InventorySaveSystem получил ссылку на инвентарь и " + items.Count + " предмет(ов) в базе.");
    }

    private static List<ItemData> CollectItemAssets()
    {
        var result = new List<ItemData>();
        string[] folders = { CombatItemsFolder, "Assets/Inventary/Script/SO" };

        string[] guids = AssetDatabase.FindAssets("t:ItemData", folders);
        foreach (string guid in guids)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && !result.Contains(item)) result.Add(item);
        }
        return result;
    }

    // ---------------------------------------------------------------- UI-хелперы

    private static GameObject CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = CreateRect(name, parent);
        var image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color color,
        TextAlignmentOptions alignment)
    {
        GameObject go = CreateRect(name, parent);
        go.AddComponent<CanvasRenderer>();

        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;

        if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        return label;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 size)
    {
        Image background = CreateImage(name, parent, WindowColor);
        background.rectTransform.sizeDelta = size;

        var button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;

        TextMeshProUGUI text = CreateText("Label", background.transform, label, 15f, TextMain,
            TextAlignmentOptions.Center);
        Stretch(text.rectTransform);

        return button;
    }

    private static void AddLayoutElement(GameObject target, float height)
    {
        var element = target.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static GameObject FindSceneObject(string name)
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (GameObject candidate in objects)
        {
            if (candidate != null && candidate.name == name && candidate.scene.IsValid()) return candidate;
        }
        return null;
    }

    // ---------------------------------------------------------------- сериализация

    private delegate void SerializedAssignment(SerializedObject serializedObject);

    private static void Assign(Object target, SerializedAssignment assignment)
    {
        var serialized = new SerializedObject(target);
        assignment(serialized);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(SerializedObject serialized, string name, float value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.floatValue = value;
    }

    private static void SetBool(SerializedObject serialized, string name, bool value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.boolValue = value;
    }
}
#endif
