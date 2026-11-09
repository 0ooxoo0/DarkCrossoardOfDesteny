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
    private const string BaseControllerPath = "Assets/PlayerAnimations/PlayerController.controller";
    private const string FistsWeaponPath = "Assets/Combat/Weapons/Fists.asset";
    private const string SwordWeaponPath = "Assets/Combat/Weapons/Sword.asset";
    private const string AbilitySlotPrefabPath = "Assets/Combat/UI/AbilitySlot.prefab";

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
        GameObject slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AbilitySlotPrefabPath);

        if (fists == null || sword == null || slotPrefab == null)
        {
            EditorUtility.DisplayDialog(
                "Combat Setup",
                "Не найдены ассеты шага 1. Сначала выполните Tools/Combat/1. Create Weapon Assets.",
                "OK");
            return;
        }

        if (!WirePlayerPrefab(fists)) return;
        if (!WireScene(fists, sword, slotPrefab)) return;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[WeaponSceneSetup] Готово: Player.prefab обвязан, AbilityBarUI и WeaponPanel добавлены в Game.unity.");
    }

    private static bool WirePlayerPrefab(WeaponData fists)
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

    private static bool WireScene(WeaponData fists, WeaponData sword, GameObject slotPrefab)
    {
        Scene active = EditorSceneManager.GetActiveScene();

        if (active.path != GameScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
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

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
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
