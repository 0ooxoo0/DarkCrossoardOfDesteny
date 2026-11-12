using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Шаг 2 setup-а: добавляет боевые компоненты на Player.prefab, AbilityBarUI на
/// Canvas и панель выбора оружия в GarderobCanvas. Требует выполнения шага 1.
/// </summary>
public static class WeaponSceneSetup
{
    private const string PlayerPrefabPath = "Assets/Player.prefab";
    private const string GameScenePath = "Assets/Scenes/Game.unity";
    private const string SampleScenePath = "Assets/Inventary/Scenes/SampleScene.unity";
    private const string BaseControllerPath = "Assets/PlayerAnimations/PlayerController.controller";
    private const string FistsWeaponPath = "Assets/Combat/Weapons/Fists.asset";
    private const string SwordWeaponPath = "Assets/Combat/Weapons/Sword.asset";
    private const string SwordItemPath = "Assets/Combat/Items/SwordItem.asset";
    private const string FistsItemPath = "Assets/Combat/Items/FistsItem.asset";
    private const string MagicBurstPath = "Assets/Combat/Abilities/MagicBurst.asset";
    private const string AbilitySlotPrefabPath = "Assets/Combat/UI/AbilitySlot.prefab";
    private const string WeaponSlotPrefabPath = "Assets/Combat/UI/WeaponSlot.prefab";

    [MenuItem("Tools/Combat/2. Wire Player & Scene")]
    public static void WireAll()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Combat Setup", "Выйдите из Play mode перед запуском.", "OK");
            return;
        }

        WeaponData fists = AssetDatabase.LoadAssetAtPath<WeaponData>(FistsWeaponPath);
        WeaponData sword = AssetDatabase.LoadAssetAtPath<WeaponData>(SwordWeaponPath);
        ItemData swordItem = AssetDatabase.LoadAssetAtPath<ItemData>(SwordItemPath);
        ItemData fistsItem = AssetDatabase.LoadAssetAtPath<ItemData>(FistsItemPath);
        AbilityData magicBurst = AssetDatabase.LoadAssetAtPath<AbilityData>(MagicBurstPath);
        GameObject slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AbilitySlotPrefabPath);
        GameObject weaponSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponSlotPrefabPath);

        if (fists == null || sword == null || swordItem == null || fistsItem == null ||
            magicBurst == null || slotPrefab == null || weaponSlotPrefab == null)
        {
            EditorUtility.DisplayDialog(
                "Combat Setup",
                "Не найдены ассеты шага 1. Сначала выполните Tools/Combat/1. Create Weapon Assets.",
                "OK");
            return;
        }

        if (!WirePlayerPrefab(fists, magicBurst)) return;
        if (!WireGameScene(fists, sword, slotPrefab, swordItem, fistsItem, weaponSlotPrefab)) return;
        if (!WireSampleScene(fists, sword, swordItem, fistsItem, weaponSlotPrefab)) return;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[WeaponSceneSetup] Готово: Player.prefab обвязан, HUD-слот добавлен в Game.unity, drop-слот и кнопка «Меч» — в SampleScene.");
    }

    private static bool WirePlayerPrefab(WeaponData fists, AbilityData magicBurst)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        try
        {
            Animator animator = contents.GetComponent<Animator>();
            PlayerController playerController = contents.GetComponent<PlayerController>();

            if (animator == null || playerController == null)
            {
                Debug.LogError("[WeaponSceneSetup] В Player.prefab нет Animator или PlayerController.");
                return false;
            }

            WeaponManager weaponManager = contents.GetComponent<WeaponManager>();
            if (weaponManager == null) weaponManager = contents.AddComponent<WeaponManager>();

            AbilitySystem abilitySystem = contents.GetComponent<AbilitySystem>();
            if (abilitySystem == null) abilitySystem = contents.AddComponent<AbilitySystem>();

            AnimationEventRelay relay = contents.GetComponent<AnimationEventRelay>();
            if (relay == null) relay = contents.AddComponent<AnimationEventRelay>();

            RuntimeAnimatorController baseController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BaseControllerPath);
            AnimationClip baseAttackClip = FindClip(baseController, "PunchRight");
            Weapon fistWeapon = FindFistWeapon(contents);

            Assign(weaponManager, so =>
            {
                so.FindProperty("animator").objectReferenceValue = animator;
                so.FindProperty("playerController").objectReferenceValue = playerController;
                so.FindProperty("abilitySystem").objectReferenceValue = abilitySystem;
                so.FindProperty("fistWeapon").objectReferenceValue = fistWeapon;
                so.FindProperty("baseController").objectReferenceValue = baseController;
                so.FindProperty("baseAttackClip").objectReferenceValue = baseAttackClip;
                so.FindProperty("rightHand").objectReferenceValue =
                    animator.GetBoneTransform(HumanBodyBones.RightHand);
                so.FindProperty("startingWeapon").objectReferenceValue = fists;
            });

            Assign(abilitySystem, so =>
            {
                so.FindProperty("weaponManager").objectReferenceValue = weaponManager;
                so.FindProperty("playerController").objectReferenceValue = playerController;

                SerializedProperty global = so.FindProperty("globalAbilities");
                global.ClearArray();
                global.InsertArrayElementAtIndex(0);
                global.GetArrayElementAtIndex(0).objectReferenceValue = magicBurst;
            });

            Assign(relay, so =>
                so.FindProperty("weaponManager").objectReferenceValue = weaponManager);

            Assign(playerController, so =>
                so.FindProperty("weaponManager").objectReferenceValue = weaponManager);

            // Rigidbody на Cube не добавляется здесь: WeaponManager сделает это
            // в рантайме, чтобы не менять существующий Rigidbody в Game.unity.
            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static bool WireGameScene(
        WeaponData fists,
        WeaponData sword,
        GameObject slotPrefab,
        ItemData swordItem,
        ItemData fistsItem,
        GameObject weaponSlotPrefab)
    {
        if (!OpenSceneIfNeeded(GameScenePath)) return false;

        // После смены сцены Unity может выгрузить ScriptableObject-ассеты,
        // поэтому ссылки перечитываются уже в активной сцене.
        fists = AssetDatabase.LoadAssetAtPath<WeaponData>(FistsWeaponPath);
        sword = AssetDatabase.LoadAssetAtPath<WeaponData>(SwordWeaponPath);
        swordItem = AssetDatabase.LoadAssetAtPath<ItemData>(SwordItemPath);
        fistsItem = AssetDatabase.LoadAssetAtPath<ItemData>(FistsItemPath);
        slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AbilitySlotPrefabPath);
        weaponSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponSlotPrefabPath);

        if (fists == null || sword == null || swordItem == null || fistsItem == null ||
            slotPrefab == null || weaponSlotPrefab == null)
        {
            Debug.LogError("[WeaponSceneSetup] Не удалось перезагрузить ассеты боя в Game.unity.");
            return false;
        }

        Scene scene = EditorSceneManager.GetActiveScene();

        GameObject canvasGo = FindSceneGameObject("Canvas");
        GameObject garderobCanvasGo = FindSceneGameObject("GarderobCanvas");

        if (canvasGo == null || garderobCanvasGo == null)
        {
            Debug.LogError("[WeaponSceneSetup] В Game.unity не найдены Canvas или GarderobCanvas.");
            return false;
        }

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        AbilityBarUI bar = canvasGo.GetComponent<AbilityBarUI>();
        if (bar == null) bar = canvasGo.AddComponent<AbilityBarUI>();

        AbilitySystem abilitySystem =
            Object.FindFirstObjectByType<AbilitySystem>(FindObjectsInactive.Include);

        Assign(bar, so =>
        {
            so.FindProperty("abilitySystem").objectReferenceValue = abilitySystem;
            so.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
            so.FindProperty("targetCanvas").objectReferenceValue = canvas;
        });

        Garderob garderob = FindGarderob("BasicHero_M");
        if (garderob == null)
            garderob = Object.FindFirstObjectByType<Garderob>(FindObjectsInactive.Include);

        if (garderob == null)
        {
            Debug.LogError("[WeaponSceneSetup] В сцене не найден Garderob.");
            return false;
        }

        RebuildWeaponPanel(garderobCanvasGo.transform, garderob, fists, sword);

        WeaponManager weaponManager =
            Object.FindFirstObjectByType<WeaponManager>(FindObjectsInactive.Include);
        if (weaponManager == null)
        {
            Debug.LogError("[WeaponSceneSetup] В Game.unity не найден WeaponManager.");
            return false;
        }

        InventorySaveSystem saveSystem =
            Object.FindFirstObjectByType<InventorySaveSystem>(FindObjectsInactive.Include);
        if (saveSystem == null)
        {
            GameObject saveGo = new GameObject("WeaponSaveSystem", typeof(InventorySaveSystem));
            saveSystem = saveGo.GetComponent<InventorySaveSystem>();
        }

        WeaponSlotUI weaponSlot = CreateWeaponSlotPanel(
            canvasGo.transform,
            "WeaponSlotPanel",
            weaponSlotPrefab,
            null,
            weaponManager,
            saveSystem,
            new List<ItemData> { swordItem, fistsItem },
            true);

        Assign(saveSystem, so =>
        {
            so.FindProperty("inventory").objectReferenceValue = null;
            so.FindProperty("weaponSlot").objectReferenceValue = weaponSlot;
            so.FindProperty("weaponManager").objectReferenceValue = weaponManager;
            EnsureObjectInList(so.FindProperty("allItems"), swordItem);
            EnsureObjectInList(so.FindProperty("allItems"), fistsItem);
            EnsureObjectInList(so.FindProperty("allWeapons"), fists);
            EnsureObjectInList(so.FindProperty("allWeapons"), sword);
            so.FindProperty("loadOnStart").boolValue = true;
        });

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    private static bool WireSampleScene(
        WeaponData fists,
        WeaponData sword,
        ItemData swordItem,
        ItemData fistsItem,
        GameObject weaponSlotPrefab)
    {
        if (!OpenSceneIfNeeded(SampleScenePath)) return false;

        // То же, что и для Game.unity: после смены сцены перечитываем ассеты.
        fists = AssetDatabase.LoadAssetAtPath<WeaponData>(FistsWeaponPath);
        sword = AssetDatabase.LoadAssetAtPath<WeaponData>(SwordWeaponPath);
        swordItem = AssetDatabase.LoadAssetAtPath<ItemData>(SwordItemPath);
        fistsItem = AssetDatabase.LoadAssetAtPath<ItemData>(FistsItemPath);
        weaponSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponSlotPrefabPath);

        if (fists == null || sword == null || swordItem == null ||
            fistsItem == null || weaponSlotPrefab == null)
        {
            Debug.LogError("[WeaponSceneSetup] Не удалось перезагрузить ассеты боя в SampleScene.");
            return false;
        }

        Scene scene = EditorSceneManager.GetActiveScene();

        Inventory inventory = Object.FindFirstObjectByType<Inventory>();
        GameObject inventoryCanvasGo = FindSceneGameObject("InventoryCanvas");

        if (inventory == null || inventoryCanvasGo == null)
        {
            Debug.LogError("[WeaponSceneSetup] В SampleScene не найдены Inventory или InventoryCanvas.");
            return false;
        }

        InventorySaveSystem saveSystem =
            Object.FindFirstObjectByType<InventorySaveSystem>(FindObjectsInactive.Include);
        if (saveSystem == null)
        {
            GameObject saveGo = new GameObject("InventarySaveSystem", typeof(InventorySaveSystem));
            saveSystem = saveGo.GetComponent<InventorySaveSystem>();
        }

        WeaponSlotUI weaponSlot = CreateWeaponSlotPanel(
            inventoryCanvasGo.transform,
            "WeaponSlotPanel",
            weaponSlotPrefab,
            inventory,
            null,
            saveSystem,
            new List<ItemData> { swordItem, fistsItem },
            false);

        Assign(saveSystem, so =>
        {
            so.FindProperty("inventory").objectReferenceValue = inventory;
            so.FindProperty("weaponSlot").objectReferenceValue = weaponSlot;
            so.FindProperty("weaponManager").objectReferenceValue = null;
            EnsureObjectInList(so.FindProperty("allItems"), swordItem);
            EnsureObjectInList(so.FindProperty("allItems"), fistsItem);
            SetObjectList(so.FindProperty("allWeapons"), new List<WeaponData> { fists, sword });
            so.FindProperty("loadOnStart").boolValue = false;
        });

        CreateTestSwordButton(inventory, swordItem);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        return OpenSceneIfNeeded(GameScenePath);
    }

    private static void RebuildWeaponPanel(
        Transform parent,
        Garderob garderob,
        WeaponData fists,
        WeaponData sword)
    {
        Transform existing = parent.Find("WeaponPanel");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        GameObject panel = new GameObject(
            "WeaponPanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));

        panel.transform.SetParent(parent, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(24f, -24f);
        panelRect.sizeDelta = new Vector2(200f, 116f);

        Image background = panel.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.45f);
        background.raycastTarget = false;

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateWeaponButton(panel.transform, "FistsButton", "Кулаки", garderob, fists);
        CreateWeaponButton(panel.transform, "SwordButton", "Меч", garderob, sword);
    }

    private static void CreateWeaponButton(
        Transform parent,
        string name,
        string label,
        Garderob garderob,
        WeaponData weapon)
    {
        GameObject buttonGo = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement),
            typeof(WeaponEquipButton));

        buttonGo.transform.SetParent(parent, false);

        LayoutElement layoutElement = buttonGo.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 176f;
        layoutElement.preferredHeight = 44f;

        Image image = buttonGo.GetComponent<Image>();
        image.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        Button button = buttonGo.GetComponent<Button>();
        button.targetGraphic = image;

        WeaponEquipButton equip = buttonGo.GetComponent<WeaponEquipButton>();
        Assign(equip, so =>
        {
            so.FindProperty("garderob").objectReferenceValue = garderob;
            so.FindProperty("weapon").objectReferenceValue = weapon;
        });

        UnityEventTools.AddPersistentListener(button.onClick, equip.Equip);

        GameObject labelGo = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        labelGo.transform.SetParent(buttonGo.transform, false);

        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = labelGo.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 20f;
        text.color = Color.white;
        text.raycastTarget = false;

        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
    }

    private static WeaponSlotUI CreateWeaponSlotPanel(
        Transform parent,
        string name,
        GameObject weaponSlotPrefab,
        Inventory inventory,
        WeaponManager weaponManager,
        InventorySaveSystem saveSystem,
        List<ItemData> weaponItems,
        bool displayOnly)
    {
        Transform existing = parent.Find(name);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        GameObject panel = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter),
            typeof(LayoutElement));

        panel.transform.SetParent(parent, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-24f, -24f);
        panelRect.sizeDelta = new Vector2(120f, 152f);

        Image background = panel.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.45f);
        background.raycastTarget = false;

        LayoutElement panelLayout = panel.GetComponent<LayoutElement>();
        panelLayout.ignoreLayout = true;

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreatePanelHeader(panel.transform, "Оружие");

        GameObject slotGo = (GameObject)PrefabUtility.InstantiatePrefab(weaponSlotPrefab, panel.transform);
        WeaponSlotUI slot = slotGo.GetComponent<WeaponSlotUI>();

        if (slot == null)
        {
            Debug.LogError("[WeaponSceneSetup] В WeaponSlot.prefab нет WeaponSlotUI.");
            return null;
        }

        LayoutElement slotLayout = slotGo.GetComponent<LayoutElement>();
        if (slotLayout == null) slotLayout = slotGo.AddComponent<LayoutElement>();
        slotLayout.preferredWidth = 96f;
        slotLayout.preferredHeight = 96f;

        Assign(slot, so =>
        {
            so.FindProperty("inventory").objectReferenceValue = inventory;
            so.FindProperty("weaponManager").objectReferenceValue = weaponManager;
            so.FindProperty("saveSystem").objectReferenceValue = saveSystem;
            so.FindProperty("displayOnly").boolValue = displayOnly;
            SetObjectList(so.FindProperty("weaponItems"), weaponItems);
        });

        return slot;
    }

    private static void CreatePanelHeader(Transform parent, string label)
    {
        GameObject headerGo = new GameObject(
            "Title",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));

        headerGo.transform.SetParent(parent, false);

        LayoutElement layout = headerGo.GetComponent<LayoutElement>();
        layout.preferredHeight = 22f;

        TextMeshProUGUI text = headerGo.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 16f;
        text.color = Color.white;
        text.raycastTarget = false;

        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
    }

    private static void CreateTestSwordButton(Inventory inventory, ItemData swordItem)
    {
        GameObject buttons = FindSceneGameObject("Buttons");
        if (buttons == null)
        {
            Debug.LogError("[WeaponSceneSetup] В SampleScene не найдена панель Buttons.");
            return;
        }

        Transform existing = buttons.transform.Find("SwordItemButton");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        GameObject buttonGo = new GameObject(
            "SwordItemButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));

        buttonGo.transform.SetParent(buttons.transform, false);
        buttonGo.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 80f);

        Image image = buttonGo.GetComponent<Image>();
        image.color = new Color(0.14978641f, 0.20754719f, 0.16002274f, 1f);
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;

        Button button = buttonGo.GetComponent<Button>();
        button.targetGraphic = image;
        AddItemTestListener(button, inventory, swordItem);

        GameObject labelGo = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        labelGo.transform.SetParent(buttonGo.transform, false);

        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = labelGo.GetComponent<TextMeshProUGUI>();
        text.text = "Меч";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 20f;
        text.color = Color.white;
        text.raycastTarget = false;

        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
    }

    private static void AddItemTestListener(Button button, Inventory inventory, ItemData item)
    {
        SerializedObject so = new SerializedObject(button);
        SerializedProperty calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");

        calls.ClearArray();
        calls.InsertArrayElementAtIndex(0);

        SerializedProperty call = calls.GetArrayElementAtIndex(0);
        call.FindPropertyRelative("m_Target").objectReferenceValue = inventory;
        call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
            "Inventory, Assembly-CSharp";
        call.FindPropertyRelative("m_MethodName").stringValue = "AddTestItem";
        call.FindPropertyRelative("m_Mode").enumValueIndex = 2;

        SerializedProperty arguments = call.FindPropertyRelative("m_Arguments");
        arguments.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = item;
        arguments.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue =
            "ItemData, Assembly-CSharp";
        arguments.FindPropertyRelative("m_IntArgument").intValue = 0;
        arguments.FindPropertyRelative("m_FloatArgument").floatValue = 0f;
        arguments.FindPropertyRelative("m_StringArgument").stringValue = string.Empty;
        arguments.FindPropertyRelative("m_BoolArgument").boolValue = false;

        call.FindPropertyRelative("m_CallState").enumValueIndex = 2;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool OpenSceneIfNeeded(string path)
    {
        Scene active = EditorSceneManager.GetActiveScene();
        if (active.path == path) return true;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        return true;
    }

    private static void SetObjectList<T>(SerializedProperty property, IList<T> values)
        where T : Object
    {
        property.ClearArray();
        if (values == null) return;

        for (int i = 0; i < values.Count; i++)
        {
            property.InsertArrayElementAtIndex(i);
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void EnsureObjectInList(SerializedProperty property, Object value)
    {
        if (value == null) return;

        for (int i = 0; i < property.arraySize; i++)
        {
            if (property.GetArrayElementAtIndex(i).objectReferenceValue == value)
                return;
        }

        int index = property.arraySize;
        property.InsertArrayElementAtIndex(index);
        property.GetArrayElementAtIndex(index).objectReferenceValue = value;
    }

    private static Weapon FindFistWeapon(GameObject root)
    {
        Weapon fallback = null;

        foreach (Weapon candidate in root.GetComponentsInChildren<Weapon>(true))
        {
            if (candidate == null) continue;
            if (fallback == null) fallback = candidate;
            if (candidate.CompareTag("DammageObject")) return candidate;
        }

        return fallback;
    }

    private static AnimationClip FindClip(RuntimeAnimatorController controller, string clipName)
    {
        if (controller == null) return null;

        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip != null && clip.name == clipName) return clip;
        }

        return null;
    }

    private static GameObject FindSceneGameObject(string name)
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (GameObject candidate in objects)
        {
            if (candidate != null && candidate.name == name && candidate.scene.IsValid())
                return candidate;
        }

        return null;
    }

    private static Garderob FindGarderob(string name)
    {
        Garderob fallback = null;
        Garderob[] garderobs = Object.FindObjectsByType<Garderob>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Garderob candidate in garderobs)
        {
            if (candidate == null || !candidate.gameObject.scene.IsValid()) continue;
            if (fallback == null) fallback = candidate;
            if (candidate.gameObject.name == name) return candidate;
        }

        return fallback;
    }

    private delegate void SerializedAssignment(SerializedObject serializedObject);

    private static void Assign(Object target, SerializedAssignment assignment)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        assignment(serializedObject);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
