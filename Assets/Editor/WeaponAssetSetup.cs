using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Шаг 1 setup-а: создаёт data-ассеты, AnimatorOverrideController, prefab слота UI
/// и расставляет AnimationEvent в клипах атаки. Не трогает сцену и Player.prefab.
/// </summary>
public static class WeaponAssetSetup
{
    private const string CombatRoot = "Assets/Combat";
    private const string WeaponsDir = CombatRoot + "/Weapons";
    private const string AbilitiesDir = CombatRoot + "/Abilities";
    private const string OverridesDir = CombatRoot + "/Overrides";
    private const string UiDir = CombatRoot + "/UI";

    private const string BaseControllerPath = "Assets/PlayerAnimations/PlayerController.controller";

    private const string FistsIdlePath =
        "Assets/Unity Technologies/Combat animations - Kickboxing and Muay Thai V1/Animations/AmineSAADI@FIGHTING_SWAY.fbx";
    private const string FistsAttackPath =
        "Assets/Unity Technologies/Combat animations - Kickboxing and Muay Thai V1/Animations/AmineSAADI@JAB 01.fbx";
    private const string SwordIdlePath =
        "Assets/Unity Technologies/Sword_sheath_AnimSet/Animation/Humanoid/idle.fbx";
    private const string SwordRunPath =
        "Assets/Unity Technologies/Sword_sheath_AnimSet/Animation/Humanoid/run02_front.fbx";
    private const string SwordAttackPath =
        "Assets/Unity Technologies/Sword_sheath_AnimSet/Animation/Humanoid/attack_01.fbx";
    private const string SwordModelPath =
        "Assets/Assets/DarkFantasy/PolygonDarkFantasy/Prefabs/Weapons/SM_Wep_Sword_01.prefab";

    private const string FistsWeaponPath = WeaponsDir + "/Fists.asset";
    private const string SwordWeaponPath = WeaponsDir + "/Sword.asset";
    private const string FistsJabPath = AbilitiesDir + "/FistsJab.asset";
    private const string SwordSlashPath = AbilitiesDir + "/SwordSlash.asset";
    private const string MagicBurstPath = AbilitiesDir + "/MagicBurst.asset";
    private const string FistsOverridePath = OverridesDir + "/Fists.overrideController";
    private const string SwordOverridePath = OverridesDir + "/Sword.overrideController";
    private const string AbilitySlotPrefabPath = UiDir + "/AbilitySlot.prefab";

    [MenuItem("Tools/Combat/1. Create Weapon Assets")]
    public static void CreateAssets()
    {
        EnsureFolder(CombatRoot);
        EnsureFolder(WeaponsDir);
        EnsureFolder(AbilitiesDir);
        EnsureFolder(OverridesDir);
        EnsureFolder(UiDir);

        RuntimeAnimatorController baseController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BaseControllerPath);
        if (baseController == null)
        {
            Debug.LogError("[WeaponAssetSetup] Не найден базовый контроллер: " + BaseControllerPath);
            return;
        }

        // Импорт клипов: циклы и AnimationEvent. Сначала импорт, потом загрузка
        // клипов — чтобы override-контроллеры ссылались на финальные ассеты.
        if (!ConfigureClip(FistsIdlePath, "FIGHTING SWAY", true, false)) return;
        if (!ConfigureClip(FistsAttackPath, "JAB", false, true)) return;
        if (!ConfigureClip(SwordIdlePath, "idle", true, false)) return;
        if (!ConfigureClip(SwordRunPath, "run02_front", true, false)) return;
        if (!ConfigureClip(SwordAttackPath, "attack_01", false, true)) return;

        AnimationClip fistsIdle = LoadClip(FistsIdlePath, "FIGHTING SWAY");
        AnimationClip fistsAttack = LoadClip(FistsAttackPath, "JAB");
        AnimationClip swordIdle = LoadClip(SwordIdlePath, "idle");
        AnimationClip swordRun = LoadClip(SwordRunPath, "run02_front");
        AnimationClip swordAttack = LoadClip(SwordAttackPath, "attack_01");

        if (fistsIdle == null || fistsAttack == null || swordIdle == null ||
            swordRun == null || swordAttack == null)
        {
            Debug.LogError("[WeaponAssetSetup] Не удалось загрузить один или несколько клипов.");
            return;
        }

        AnimatorOverrideController fistsOverride = CreateOrUpdateOverride(
            FistsOverridePath,
            baseController,
            new Dictionary<string, AnimationClip>
            {
                { "Idle", fistsIdle },
                { "PunchRight", fistsAttack }
            });

        AnimatorOverrideController swordOverride = CreateOrUpdateOverride(
            SwordOverridePath,
            baseController,
            new Dictionary<string, AnimationClip>
            {
                { "Idle", swordIdle },
                { "Sprint", swordRun },
                { "PunchRight", swordAttack }
            });

        PlayerAbilityAssets abilities = CreateAbilityAssets();
        CreateWeaponAssets(fistsOverride, swordOverride, abilities);
        CreateAbilitySlotPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[WeaponAssetSetup] Готово. Созданы/обновлены ассеты в " + CombatRoot +
                  ". Проверьте JAB/attack_01: по 2 AnimationEvent (EnableHitbox/DisableHitbox).");
    }

    private static bool ConfigureClip(string fbxPath, string clipName, bool loopTime, bool addHitboxEvents)
    {
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("[WeaponAssetSetup] Не найден ModelImporter: " + fbxPath);
            return false;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        if (clips == null || clips.Length == 0)
        {
            Debug.LogError("[WeaponAssetSetup] У FBX нет клипов: " + fbxPath);
            return false;
        }

        int index = Array.FindIndex(clips, c => c.name == clipName);
        if (index < 0)
            index = 0;

        ModelImporterClipAnimation clip = clips[index];
        clip.loopTime = loopTime;

        List<AnimationEvent> events = new List<AnimationEvent>();
        if (clip.events != null)
        {
            foreach (AnimationEvent existing in clip.events)
            {
                if (existing != null &&
                    existing.functionName != "EnableHitbox" &&
                    existing.functionName != "DisableHitbox")
                {
                    events.Add(existing);
                }
            }
        }

        if (addHitboxEvents)
        {
            AnimationClip importedClip = LoadClip(fbxPath, clip.name);
            if (importedClip == null)
            {
                Debug.LogError("[WeaponAssetSetup] Не найден AnimationClip для событий: " + fbxPath);
                return false;
            }

            // 15% и 55% длины клипа — те же значения, что у PlayerController по умолчанию.
            events.Add(new AnimationEvent
            {
                functionName = "EnableHitbox",
                time = importedClip.length * 0.15f
            });
            events.Add(new AnimationEvent
            {
                functionName = "DisableHitbox",
                time = importedClip.length * 0.55f
            });
        }

        clip.events = events.ToArray();
        clips[index] = clip;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        return true;
    }

    private static AnimatorOverrideController CreateOrUpdateOverride(
        string assetPath,
        RuntimeAnimatorController baseController,
        Dictionary<string, AnimationClip> replacements)
    {
        AnimatorOverrideController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(assetPath);

        if (controller == null)
        {
            controller = new AnimatorOverrideController(baseController);
            AssetDatabase.CreateAsset(controller, assetPath);
        }
        else if (controller.runtimeAnimatorController != baseController)
        {
            controller.runtimeAnimatorController = baseController;
        }

        foreach (KeyValuePair<string, AnimationClip> pair in replacements)
        {
            AnimationClip original = baseController.animationClips
                .FirstOrDefault(c => c != null && c.name == pair.Key);

            if (original == null)
            {
                Debug.LogError("[WeaponAssetSetup] В базовом контроллере нет клипа: " + pair.Key);
                continue;
            }

            if (pair.Value == null)
            {
                Debug.LogError("[WeaponAssetSetup] Не назначен replacement для: " + pair.Key);
                continue;
            }

            controller[original] = pair.Value;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static PlayerAbilityAssets CreateAbilityAssets()
    {
        AbilityData fistsJab = CreateOrLoadAsset<AbilityData>(FistsJabPath);
        fistsJab.abilityId = "fists_jab";
        fistsJab.displayName = "Джеб";
        fistsJab.type = AbilityType.Melee;
        fistsJab.cooldown = 0.8f;
        fistsJab.hotkey = KeyCode.Alpha1;
        fistsJab.requiredWeapon = WeaponType.Fists;
        fistsJab.damageTags = new List<string> { "DammageObject" };
        EditorUtility.SetDirty(fistsJab);

        AbilityData swordSlash = CreateOrLoadAsset<AbilityData>(SwordSlashPath);
        swordSlash.abilityId = "sword_slash";
        swordSlash.displayName = "Удар мечом";
        swordSlash.type = AbilityType.Melee;
        swordSlash.cooldown = 1.2f;
        swordSlash.hotkey = KeyCode.Alpha1;
        swordSlash.requiredWeapon = WeaponType.Sword;
        swordSlash.damageTags = new List<string> { "DammageObject" };
        EditorUtility.SetDirty(swordSlash);

        AbilityData magicBurst = CreateOrLoadAsset<AbilityData>(MagicBurstPath);
        magicBurst.abilityId = "magic_burst";
        magicBurst.displayName = "Магический взрыв";
        magicBurst.type = AbilityType.Magic;
        magicBurst.cooldown = 3f;
        magicBurst.hotkey = KeyCode.Alpha2;
        magicBurst.requiredWeapon = WeaponType.Any;
        magicBurst.damageTags = new List<string> { "DammageObject" };
        magicBurst.damageLayers = ~0;
        magicBurst.magicRange = 2.5f;
        magicBurst.magicRadius = 2f;
        magicBurst.magicSpawnDistance = 1.5f;
        magicBurst.vfxDelay = 0.2f;
        magicBurst.vfxLifetime = 2f;
        magicBurst.magicHitboxLifetime = 0.2f;
        EditorUtility.SetDirty(magicBurst);

        return new PlayerAbilityAssets(fistsJab, swordSlash, magicBurst);
    }

    private static void CreateWeaponAssets(
        AnimatorOverrideController fistsOverride,
        AnimatorOverrideController swordOverride,
        PlayerAbilityAssets abilities)
    {
        WeaponData fists = CreateOrLoadAsset<WeaponData>(FistsWeaponPath);
        fists.weaponId = "fists";
        fists.displayName = "Кулаки";
        fists.type = WeaponType.Fists;
        fists.animatorOverride = fistsOverride;
        fists.useAnimationEvents = true;
        fists.damageTags = new List<string> { "DammageObject" };
        fists.abilities = new List<AbilityData> { abilities.FistsJab };
        fists.weaponModelPrefab = null;
        EditorUtility.SetDirty(fists);

        WeaponData sword = CreateOrLoadAsset<WeaponData>(SwordWeaponPath);
        sword.weaponId = "sword";
        sword.displayName = "Меч";
        sword.type = WeaponType.Sword;
        sword.animatorOverride = swordOverride;
        sword.useAnimationEvents = true;
        sword.damageTags = new List<string> { "DammageObject" };
        sword.abilities = new List<AbilityData> { abilities.SwordSlash, abilities.MagicBurst };
        sword.weaponModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordModelPath);
        sword.weaponLocalPosition = Vector3.zero;
        sword.weaponLocalEulerAngles = Vector3.zero;
        sword.weaponLocalScale = Vector3.one;
        sword.hitboxLocalPosition = Vector3.zero;
        sword.hitboxLocalEulerAngles = Vector3.zero;
        sword.hitboxSize = new Vector3(0.15f, 0.15f, 1.2f);

        if (sword.weaponModelPrefab == null)
            Debug.LogWarning("[WeaponAssetSetup] Не найдена модель меча: " + SwordModelPath);

        EditorUtility.SetDirty(sword);
    }

    private static void CreateAbilitySlotPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(AbilitySlotPrefabPath) != null)
            return;

        GameObject root = new GameObject(
            "AbilitySlot",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(AbilitySlotUI));

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(80f, 80f);

        Image background = root.GetComponent<Image>();
        background.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(root.transform, false);
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(6f, 6f);
        iconRect.offsetMax = new Vector2(-6f, -6f);
        Image iconImage = iconGo.GetComponent<Image>();
        iconImage.raycastTarget = false;

        GameObject overlayGo = new GameObject(
            "CooldownOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        overlayGo.transform.SetParent(root.transform, false);
        RectTransform overlayRect = overlayGo.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        Image overlayImage = overlayGo.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.65f);
        overlayImage.type = Image.Type.Filled;
        overlayImage.fillMethod = Image.FillMethod.Radial360;
        overlayImage.fillOrigin = (int)Image.Origin360.Top;
        overlayImage.fillClockwise = false;
        overlayImage.fillAmount = 0f;
        overlayImage.raycastTarget = false;

        TMP_Text keyLabel = CreateText(
            root.transform,
            "KeyLabel",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(28f, 24f),
            new Vector2(8f, -6f),
            TextAlignmentOptions.TopLeft,
            18f,
            false);

        TMP_Text cooldownLabel = CreateText(
            root.transform,
            "CooldownLabel",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(70f, 30f),
            Vector2.zero,
            TextAlignmentOptions.Center,
            22f,
            true);

        AbilitySlotUI slotUi = root.GetComponent<AbilitySlotUI>();
        slotUi.SetVisualReferences(iconImage, overlayImage, keyLabel, cooldownLabel);

        PrefabUtility.SaveAsPrefabAsset(root, AbilitySlotPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 size,
        Vector2 anchoredPosition,
        TextAlignmentOptions alignment,
        float fontSize,
        bool bold)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = name == "KeyLabel" ? "1" : string.Empty;
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.color = Color.white;
        text.raycastTarget = false;

        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        return text;
    }

    private static AnimationClip LoadClip(string fbxPath, string expectedName)
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .Where(c => c != null && !c.name.StartsWith("__preview__", StringComparison.Ordinal))
            .ToArray();

        AnimationClip clip = clips.FirstOrDefault(c => c.name == expectedName);
        if (clip == null)
            clip = clips.FirstOrDefault();

        if (clip == null)
            Debug.LogError("[WeaponAssetSetup] Не найден AnimationClip в " + fbxPath);

        return clip;
    }

    private static T CreateOrLoadAsset<T>(string assetPath) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, assetPath);
        return asset;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath);
        if (string.IsNullOrEmpty(parent))
            return;

        parent = parent.Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
    }

    private readonly struct PlayerAbilityAssets
    {
        public readonly AbilityData FistsJab;
        public readonly AbilityData SwordSlash;
        public readonly AbilityData MagicBurst;

        public PlayerAbilityAssets(AbilityData fistsJab, AbilityData swordSlash, AbilityData magicBurst)
        {
            FistsJab = fistsJab;
            SwordSlash = swordSlash;
            MagicBurst = magicBurst;
        }
    }
}
