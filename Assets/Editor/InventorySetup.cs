using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Сборка инвентаря: тестовые предметы, префабы слотов и панель в Game.unity.
/// Меню: Tools → Inventory → Setup Test Data.
/// </summary>
public static class InventorySetup
{
    private const string GameScenePath = "Assets/Scenes/Game.unity";
    private const string TestDataFolder = "Assets/Scripts/Inventary/TestData";
    private const string PrefabFolder = "Assets/Scripts/Inventary/Prefabs";
    private const string ThemePath = TestDataFolder + "/InventoryTheme.asset";
    private const string SlotPrefabPath = PrefabFolder + "/InventorySlot.prefab";
    private const string EquipmentSlotPrefabPath = PrefabFolder + "/EquipmentSlot.prefab";
    private const string LootRowPrefabPath = PrefabFolder + "/LootRow.prefab";
    private const string LootContainerPrefabPath = PrefabFolder + "/WorldLootContainer.prefab";
    private const string OutlineMaterialPath = "Assets/Scripts/Inventary/Materials/OutlineMaterial.mat";
    private const string ItemEffectPrefabPath = "Assets/Effects/ItemEffect.prefab";

    private const string CharacterName = "BasicHero_M";
    private const string VisualsRootName = "EquipmentVisuals";

    private const string SwordWeaponPath = "Assets/Combat/Weapons/Sword.asset";
    private const string FistsWeaponPath = "Assets/Combat/Weapons/Fists.asset";

    private const string ArmorIconPath = "Assets/Ideals/Menu/UI/GUI_Parts/Icons/armor_icon.png";
    private const string RingIconPath = "Assets/one-ring.jpg";
    private const string BarbarianSwordSpritePath =
        "Assets/barbarian-equipment-pictogram-suitable-illustrations-50863202_4.asset";
    private const string BarbarianHelmetSpritePath =
        "Assets/barbarian-equipment-pictogram-suitable-illustrations-50863202_0.asset";
    private const string BarbarianPantsSpritePath =
        "Assets/barbarian-equipment-pictogram-suitable-illustrations-50863202_2.asset";

    private const string StarterContainerName = "[Loot] Starter";
    private const string LootServiceName = "LootService";
    private const string LootPanelName = "LootPanel";

    private const float LootPanelWidth = 380f;
    private const float LootPanelHeight = 360f;
    private const float LootRowHeight = 52f;
    private const float StarterLootDistance = 3f;

    private const string PanelName = "InventoryPanel";
    private const string CanvasName = "Canvas";
    private const string BuiltinSpritePath = "UI/Skin/UISprite.psd";

    private const int GridWidth = 5;
    private const int GridHeight = 4;

    private const float FrameWidth = 1020f;
    private const float FrameHeight = 640f;
    private const float Margin = 24f;
    private const float EquipmentSlotSize = 96f;
    private const float EquipmentPanelWidth = 240f;
    private const float TextSizeTitle = 30f;
    private const float TextSizeBody = 16f;
    private const float TextSizeSmall = 14f;

    private const int DataVersion = 1;

    // ---------------------------------------------------------------- Меню

    /// <summary>Создаёт тестовые предметы, префабы, тему и собирает панель в Game.unity.</summary>
    [MenuItem("Tools/Inventory/Setup Test Data")]
    public static void SetupTestData()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Inventory", "Выйдите из Play mode перед запуском.", "OK");
            return;
        }

        int migrated = MigrateAllItemData();
        EnsureFolder(TestDataFolder);

        InventoryTheme theme = CreateOrLoadTheme();
        GameObject slotPrefab = CreateSlotPrefab(theme);
        GameObject equipmentPrefab = CreateEquipmentSlotPrefab(theme);

        List<ItemData> testItems = CreateTestItems();

        if (!BuildPanelInGameScene(theme, slotPrefab, equipmentPrefab, testItems))
        {
            AssetDatabase.SaveAssets();
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[InventorySetup] Готово. Мигрировано ассетов: {migrated}. " +
                  "Откройте Game.unity и нажмите I.");
        EditorUtility.DisplayDialog(
            "Inventory",
            $"Готово.\nМигрировано ассетов: {migrated}.\nПанель собрана в Game.unity.\nЗапустите сцену и нажмите I.",
            "OK");
    }

    /// <summary>Переносит старые значения ItemType на новую раскладку.</summary>
    [MenuItem("Tools/Inventory/Migrate Item Data")]
    public static void MigrateItemDataMenu()
    {
        int migrated = MigrateAllItemData();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Inventory", $"Перенесено ассетов: {migrated}.", "OK");
    }

    /// <summary>Пересобирает только панель в Game.unity, не трогая ассеты предметов.</summary>
    [MenuItem("Tools/Inventory/Rebuild Game Panel")]
    public static void RebuildGamePanelMenu()
    {
        InventoryTheme theme = AssetDatabase.LoadAssetAtPath<InventoryTheme>(ThemePath);
        GameObject slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
        GameObject equipmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EquipmentSlotPrefabPath);

        if (theme == null || slotPrefab == null || equipmentPrefab == null)
        {
            EditorUtility.DisplayDialog("Inventory", "Сначала выполните Tools → Inventory → Setup Test Data.", "OK");
            return;
        }

        List<ItemData> items = new List<ItemData>
        {
            AssetDatabase.LoadAssetAtPath<ItemData>(TestDataFolder + "/Weapon_BarbarianSword.asset"),
            AssetDatabase.LoadAssetAtPath<ItemData>(TestDataFolder + "/Armor_Leather.asset"),
            AssetDatabase.LoadAssetAtPath<ItemData>(TestDataFolder + "/Helmet_Mage.asset"),
            AssetDatabase.LoadAssetAtPath<ItemData>(TestDataFolder + "/Pants_Leather.asset"),
            AssetDatabase.LoadAssetAtPath<ItemData>(TestDataFolder + "/Accessory_Ring.asset")
        };

        BuildPanelInGameScene(theme, slotPrefab, equipmentPrefab, items);
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------- Миграция

    private static int MigrateAllItemData()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemData");
        int migrated = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null || item.DataVersion >= DataVersion) continue;

            SerializedObject serialized = new SerializedObject(item);
            SerializedProperty typeProperty = serialized.FindProperty("type");
            int oldType = typeProperty.intValue;

            typeProperty.intValue = (int)MapLegacyType(oldType);
            serialized.FindProperty("useKind").intValue = (int)MapLegacyUseKind(oldType);
            serialized.FindProperty("dataVersion").intValue = DataVersion;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(item);
            migrated++;
        }

        return migrated;
    }

    // Старые значения: 0 Weapon, 1 Potion, 2 Quest, 3 Pill, 4 Scroll,
    // 5 BreakthroughMaterial, 6 DaoSeed.
    private static ItemType MapLegacyType(int oldType)
    {
        switch (oldType)
        {
            case 0: return ItemType.Weapon;
            case 1: return ItemType.Consumable;
            case 2: return ItemType.Quest;
            case 3: return ItemType.Consumable;
            case 4: return ItemType.Consumable;
            case 5: return ItemType.Misc;
            case 6: return ItemType.Consumable;
            default: return ItemType.Misc;
        }
    }

    private static ItemUseKind MapLegacyUseKind(int oldType)
    {
        switch (oldType)
        {
            case 1: return ItemUseKind.Potion;
            case 3: return ItemUseKind.Pill;
            case 4: return ItemUseKind.Scroll;
            case 5: return ItemUseKind.BreakthroughMaterial;
            case 6: return ItemUseKind.DaoSeed;
            default: return ItemUseKind.None;
        }
    }

    // ------------------------------------------------------------ Тестовые данные

    private static List<ItemData> CreateTestItems()
    {
        Sprite armorIcon = LoadSprite(ArmorIconPath);
        Sprite ringIcon = LoadSprite(RingIconPath);
        Sprite swordIcon = LoadSprite(BarbarianSwordSpritePath);
        Sprite helmetIcon = LoadSprite(BarbarianHelmetSpritePath);
        Sprite pantsIcon = LoadSprite(BarbarianPantsSpritePath);

        // Меч переиспользует боевой Sword: анимации и хитбокс уже настроены под него.
        WeaponData sword = AssetDatabase.LoadAssetAtPath<WeaponData>(SwordWeaponPath);

        ItemData swordItem = CreateOrUpdateItem(
            TestDataFolder + "/Weapon_BarbarianSword.asset",
            "loot_sword_barbarian",
            "Меч варвара",
            "Широкий клинок варвара. Тяжёлый, но честный.",
            ItemType.Weapon, ItemUseKind.None, 1, 12,
            swordIcon, EquipmentSlotType.Weapon, string.Empty, sword);

        ItemData armorItem = CreateOrUpdateItem(
            TestDataFolder + "/Armor_Leather.asset",
            "loot_armor_leather",
            "Кожаная броня",
            "Плотная кожаная куртка со следами старых починок.",
            ItemType.Armor, ItemUseKind.None, 1, 20,
            armorIcon, EquipmentSlotType.Armor, "M_Swordsman_Top", null);

        ItemData helmetItem = CreateOrUpdateItem(
            TestDataFolder + "/Helmet_Mage.asset",
            "loot_helmet_mage",
            "Шапка мага",
            "Мягкая шапка с вышитыми знаками. Пахнет травами.",
            ItemType.Armor, ItemUseKind.None, 1, 15,
            helmetIcon, EquipmentSlotType.Helmet, "M_Mage_Headwear", null);

        ItemData pantsItem = CreateOrUpdateItem(
            TestDataFolder + "/Pants_Leather.asset",
            "loot_pants_leather",
            "Кожаные штаны",
            "Штаны, удобные для долгой дороги.",
            ItemType.Armor, ItemUseKind.None, 1, 10,
            pantsIcon, EquipmentSlotType.Pants, "M_Swordsman_Bottom", null);

        ItemData ringItem = CreateOrUpdateItem(
            TestDataFolder + "/Accessory_Ring.asset",
            "loot_accessory_ring",
            "Кольцо странника",
            "Простое кольцо. На поясе висит сумка, с которой удобнее в пути.",
            ItemType.Armor, ItemUseKind.None, 1, 25,
            ringIcon, EquipmentSlotType.Accessory, "M_SlingBag", null);

        return new List<ItemData> { swordItem, armorItem, helmetItem, pantsItem, ringItem };
    }

    private static ItemData CreateOrUpdateItem(
        string assetPath, string itemId, string displayName, string description,
        ItemType type, ItemUseKind useKind, int maxStack, int sellPrice,
        Sprite icon, EquipmentSlotType equipSlot, string equipVisualId, WeaponData weaponData)
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
        item.type = type;
        item.useKind = useKind;
        item.stackable = maxStack > 1;
        item.maxStackSize = Mathf.Max(1, maxStack);
        item.sellPrice = Mathf.Max(0, sellPrice);
        item.icon = icon;
        item.equipSlot = equipSlot;
        item.equipVisualId = equipVisualId;
        item.weaponData = weaponData;
        item.MarkMigrated(DataVersion);

        EditorUtility.SetDirty(item);
        return item;
    }

    private static Sprite LoadSprite(string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
            Debug.LogWarning($"[InventorySetup] Не найден спрайт: {assetPath}. Останется [PLACEHOLDER].");

        return sprite;
    }

    private static InventoryTheme CreateOrLoadTheme()
    {
        InventoryTheme theme = AssetDatabase.LoadAssetAtPath<InventoryTheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<InventoryTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        theme.font = TMP_Settings.defaultFontAsset;
        EditorUtility.SetDirty(theme);
        return theme;
    }

    // -------------------------------------------------------------- Префабы

    private static GameObject CreateSlotPrefab(InventoryTheme theme)
    {
        AssetDatabase.DeleteAsset(SlotPrefabPath);

        GameObject root = NewUiObject("InventorySlot", null,
            typeof(Image), typeof(CanvasGroup), typeof(InventorySlotUI));

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = theme.slotSize;

        Image background = root.GetComponent<Image>();
        ConfigureImage(background, theme.slotBackground, true, true);

        Image border = CreateImage("Border", root.transform, theme.borderColor, true, false);
        Stretch(border.rectTransform);

        Image icon = CreateImage("Icon", root.transform, Color.white, false, false);
        Stretch(icon.rectTransform, 10f);

        Image placeholder = CreateImage("Placeholder", root.transform, theme.placeholderColor, true, false);
        Stretch(placeholder.rectTransform, 22f);
        placeholder.enabled = false;

        TextMeshProUGUI placeholderLabel = CreateText("PlaceholderLabel", root.transform,
            "[PLACEHOLDER]", TextSizeSmall - 4f, TextAlignmentOptions.Center, theme.textColor);
        RectTransform labelRect = placeholderLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 3f);
        labelRect.sizeDelta = new Vector2(0f, 14f);
        placeholderLabel.enabled = false;

        TextMeshProUGUI quantity = CreateText("Quantity", root.transform, string.Empty,
            TextSizeBody, TextAlignmentOptions.BottomRight, theme.textColor);
        RectTransform quantityRect = quantity.rectTransform;
        quantityRect.anchorMin = new Vector2(1f, 0f);
        quantityRect.anchorMax = new Vector2(1f, 0f);
        quantityRect.pivot = new Vector2(1f, 0f);
        quantityRect.anchoredPosition = new Vector2(-6f, 4f);
        quantityRect.sizeDelta = new Vector2(48f, 22f);
        quantity.enabled = false;

        InventorySlotUI slotUi = root.GetComponent<InventorySlotUI>();
        Assign(slotUi, serialized =>
        {
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("background").objectReferenceValue = background;
            serialized.FindProperty("border").objectReferenceValue = border;
            serialized.FindProperty("placeholder").objectReferenceValue = placeholder;
            serialized.FindProperty("quantityText").objectReferenceValue = quantity;
            serialized.FindProperty("placeholderText").objectReferenceValue = placeholderLabel;
        });

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateEquipmentSlotPrefab(InventoryTheme theme)
    {
        AssetDatabase.DeleteAsset(EquipmentSlotPrefabPath);

        GameObject root = NewUiObject("EquipmentSlot", null,
            typeof(Image), typeof(CanvasGroup), typeof(EquipmentSlotUI));

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(EquipmentSlotSize, EquipmentSlotSize);

        Image background = root.GetComponent<Image>();
        ConfigureImage(background, theme.slotBackground, true, true);

        Image border = CreateImage("Border", root.transform, theme.borderColor, true, false);
        Stretch(border.rectTransform);

        Image icon = CreateImage("Icon", root.transform, Color.white, false, false);
        Stretch(icon.rectTransform, 10f);

        Image placeholder = CreateImage("Placeholder", root.transform, theme.placeholderColor, true, false);
        Stretch(placeholder.rectTransform, 22f);
        placeholder.enabled = false;

        TextMeshProUGUI label = CreateText("TypeLabel", root.transform, "Слот",
            TextSizeSmall - 2f, TextAlignmentOptions.Bottom, theme.mutedTextColor);
        Stretch(label.rectTransform, 4f);
        label.alignment = TextAlignmentOptions.Bottom;
        label.raycastTarget = false;

        EquipmentSlotUI slotUi = root.GetComponent<EquipmentSlotUI>();
        Assign(slotUi, serialized =>
        {
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("background").objectReferenceValue = background;
            serialized.FindProperty("border").objectReferenceValue = border;
            serialized.FindProperty("placeholder").objectReferenceValue = placeholder;
            serialized.FindProperty("typeLabel").objectReferenceValue = label;
        });

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EquipmentSlotPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ---------------------------------------------------------- Панель в сцене

    private static bool BuildPanelInGameScene(InventoryTheme theme, GameObject slotPrefab,
        GameObject equipmentPrefab, List<ItemData> startingItems)
    {
        if (!OpenSceneIfNeeded(GameScenePath)) return false;

        // После смены сцены Unity выгружает ассеты — перечитываем по путям.
        theme = AssetDatabase.LoadAssetAtPath<InventoryTheme>(ThemePath);
        slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
        equipmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EquipmentSlotPrefabPath);
        startingItems = ReloadItems(startingItems);

        if (theme == null || slotPrefab == null || equipmentPrefab == null)
        {
            Debug.LogError("[InventorySetup] Не удалось перезагрузить ассеты после открытия Game.unity.");
            return false;
        }

        GameObject canvasGo = FindSceneGameObject(CanvasName);
        if (canvasGo == null)
        {
            Debug.LogError("[InventorySetup] В Game.unity не найден Canvas.");
            return false;
        }

        GameObject oldPanel = FindSceneGameObject(PanelName);
        if (oldPanel != null) Object.DestroyImmediate(oldPanel);

        Scene scene = EditorSceneManager.GetActiveScene();
        WeaponManager weaponManager = Object.FindFirstObjectByType<WeaponManager>(FindObjectsInactive.Include);
        PlayerController playerController = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        PlayerInteraction playerInteraction = Object.FindFirstObjectByType<PlayerInteraction>(FindObjectsInactive.Include);

        GameObject panelGo = NewUiObject(PanelName, canvasGo.transform);
        Stretch(panelGo.GetComponent<RectTransform>());

        Inventory inventory = panelGo.AddComponent<Inventory>();
        Assign(inventory, serialized =>
        {
            serialized.FindProperty("width").intValue = GridWidth;
            serialized.FindProperty("height").intValue = GridHeight;
        });

        InventoryUI gridView = panelGo.AddComponent<InventoryUI>();
        InventoryDragController dragController = panelGo.AddComponent<InventoryDragController>();
        InventoryPanelUI panel = panelGo.AddComponent<InventoryPanelUI>();

        // --- Окно ---
        GameObject window = NewUiObject("Window", panelGo.transform);
        Stretch(window.GetComponent<RectTransform>());
        window.SetActive(false);

        Image dim = CreateImage("Dim", window.transform, theme.dimBackground, false, true);
        Stretch(dim.rectTransform);

        Image frame = CreateImage("Frame", window.transform, theme.windowBackground, true, true);
        RectTransform frameRect = frame.rectTransform;
        frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.anchoredPosition = Vector2.zero;
        frameRect.sizeDelta = new Vector2(FrameWidth, FrameHeight);

        TextMeshProUGUI title = CreateText("Title", frame.transform, "Инвентарь",
            TextSizeTitle, TextAlignmentOptions.Left, theme.textColor);
        PlaceTopLeft(title.rectTransform, Margin, -18f, new Vector2(420f, 40f));

        // --- Сетка ---
        GameObject gridGo = NewUiObject("Grid", frame.transform);
        RectTransform gridRect = gridGo.GetComponent<RectTransform>();
        float gridWidth = GridWidth * theme.slotSize.x + (GridWidth - 1) * theme.slotSpacing.x;
        float gridHeight = GridHeight * theme.slotSize.y + (GridHeight - 1) * theme.slotSpacing.y;
        PlaceTopLeft(gridRect, Margin, -72f, new Vector2(gridWidth, gridHeight));

        GridLayoutGroup layout = gridGo.AddComponent<GridLayoutGroup>();
        layout.cellSize = theme.slotSize;
        layout.spacing = theme.slotSpacing;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = GridWidth;

        // --- Экипировка ---
        GameObject equipmentGo = NewUiObject("Equipment", frame.transform);
        RectTransform equipmentRect = equipmentGo.GetComponent<RectTransform>();
        PlaceTopRight(equipmentRect, -Margin, -72f, new Vector2(EquipmentPanelWidth, FrameHeight - 160f));

        VerticalLayoutGroup equipmentLayout = equipmentGo.AddComponent<VerticalLayoutGroup>();
        equipmentLayout.spacing = 8f;
        equipmentLayout.childAlignment = TextAnchor.UpperCenter;
        equipmentLayout.childControlWidth = false;
        equipmentLayout.childControlHeight = false;
        equipmentLayout.childForceExpandWidth = false;
        equipmentLayout.childForceExpandHeight = false;

        TextMeshProUGUI equipmentTitle = CreateText("EquipmentTitle", equipmentGo.transform,
            "Экипировка", TextSizeBody, TextAlignmentOptions.Center, theme.mutedTextColor);
        LayoutElement equipmentTitleLayout = equipmentTitle.gameObject.AddComponent<LayoutElement>();
        equipmentTitleLayout.preferredHeight = 24f;
        equipmentTitleLayout.preferredWidth = EquipmentPanelWidth;

        List<EquipmentSlotUI> equipmentSlots = new List<EquipmentSlotUI>();
        EquipmentSlotType[] slotTypes =
        {
            EquipmentSlotType.Weapon,
            EquipmentSlotType.Armor,
            EquipmentSlotType.Helmet,
            EquipmentSlotType.Pants,
            EquipmentSlotType.Accessory
        };

        for (int i = 0; i < slotTypes.Length; i++)
        {
            GameObject slotGo = (GameObject)PrefabUtility.InstantiatePrefab(equipmentPrefab, equipmentGo.transform);
            EquipmentSlotUI slot = slotGo.GetComponent<EquipmentSlotUI>();
            if (slot == null) continue;

            LayoutElement slotLayout = slotGo.GetComponent<LayoutElement>();
            if (slotLayout == null) slotLayout = slotGo.AddComponent<LayoutElement>();
            slotLayout.preferredWidth = EquipmentSlotSize;
            slotLayout.preferredHeight = EquipmentSlotSize;

            EquipmentSlotType slotType = slotTypes[i];
            Assign(slot, serialized =>
            {
                serialized.FindProperty("slotType").intValue = (int)slotType;
                serialized.FindProperty("inventory").objectReferenceValue = inventory;
                serialized.FindProperty("dragController").objectReferenceValue = dragController;
                serialized.FindProperty("theme").objectReferenceValue = theme;
                serialized.FindProperty("weaponManager").objectReferenceValue =
                    slotType == EquipmentSlotType.Weapon ? weaponManager : null;
            });

            equipmentSlots.Add(slot);
        }

        // --- Кнопки ---
        GameObject buttonsGo = NewUiObject("Buttons", frame.transform);
        RectTransform buttonsRect = buttonsGo.GetComponent<RectTransform>();
        buttonsRect.anchorMin = new Vector2(0f, 0f);
        buttonsRect.anchorMax = new Vector2(0f, 0f);
        buttonsRect.pivot = new Vector2(0f, 0f);
        buttonsRect.anchoredPosition = new Vector2(Margin, Margin);
        buttonsRect.sizeDelta = new Vector2(gridWidth, 44f);

        HorizontalLayoutGroup buttonsLayout = buttonsGo.AddComponent<HorizontalLayoutGroup>();
        buttonsLayout.spacing = 8f;
        buttonsLayout.childAlignment = TextAnchor.MiddleLeft;
        buttonsLayout.childControlWidth = false;
        buttonsLayout.childControlHeight = false;
        buttonsLayout.childForceExpandWidth = false;
        buttonsLayout.childForceExpandHeight = false;

        Button sortButton = CreateButton(buttonsGo.transform, "SortButton", "Сортировать", theme);
        Button dropButton = CreateButton(buttonsGo.transform, "DropButton", "Выбросить", theme);
        Button deleteButton = CreateButton(buttonsGo.transform, "DeleteButton", "Удалить", theme);
        Button useButton = CreateButton(buttonsGo.transform, "UseButton", "Использовать", theme);

        TextMeshProUGUI footer = CreateText("Footer", frame.transform, string.Empty,
            TextSizeSmall, TextAlignmentOptions.Left, theme.mutedTextColor);
        RectTransform footerRect = footer.rectTransform;
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(0f, 0f);
        footerRect.pivot = new Vector2(0f, 0f);
        footerRect.anchoredPosition = new Vector2(Margin, Margin + 52f);
        footerRect.sizeDelta = new Vector2(gridWidth, 24f);

        Button closeButton = CreateButton(frame.transform, "CloseButton", "Закрыть", theme);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-Margin, -18f);
        closeRect.sizeDelta = new Vector2(120f, 40f);

        // --- Тултип ---
        GameObject tooltipGo = NewUiObject("Tooltip", window.transform,
            typeof(Image), typeof(CanvasGroup), typeof(InventoryTooltipUI));
        RectTransform tooltipRect = tooltipGo.GetComponent<RectTransform>();
        tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = new Vector2(0f, 1f);
        tooltipRect.sizeDelta = new Vector2(340f, 190f);
        tooltipRect.anchoredPosition = Vector2.zero;

        Image tooltipBackground = tooltipGo.GetComponent<Image>();
        ConfigureImage(tooltipBackground, theme.windowBackground, true, false);

        Image tooltipBorder = CreateImage("Border", tooltipGo.transform, theme.borderColor, true, false);
        Stretch(tooltipBorder.rectTransform);

        TextMeshProUGUI tooltipName = CreateText("Name", tooltipGo.transform, string.Empty,
            TextSizeBody + 2f, TextAlignmentOptions.TopLeft, theme.textColor);
        PlaceTopLeft(tooltipName.rectTransform, 16f, -12f, new Vector2(308f, 26f));

        TextMeshProUGUI tooltipType = CreateText("Type", tooltipGo.transform, string.Empty,
            TextSizeSmall, TextAlignmentOptions.TopLeft, theme.highlightColor);
        PlaceTopLeft(tooltipType.rectTransform, 16f, -40f, new Vector2(308f, 20f));

        TextMeshProUGUI tooltipDescription = CreateText("Description", tooltipGo.transform, string.Empty,
            TextSizeSmall, TextAlignmentOptions.TopLeft, theme.mutedTextColor);
        PlaceTopLeft(tooltipDescription.rectTransform, 16f, -66f, new Vector2(308f, 84f));

        TextMeshProUGUI tooltipPrice = CreateText("Price", tooltipGo.transform, string.Empty,
            TextSizeSmall, TextAlignmentOptions.BottomLeft, theme.highlightColor);
        RectTransform tooltipPriceRect = tooltipPrice.rectTransform;
        tooltipPriceRect.anchorMin = new Vector2(0f, 0f);
        tooltipPriceRect.anchorMax = new Vector2(0f, 0f);
        tooltipPriceRect.pivot = new Vector2(0f, 0f);
        tooltipPriceRect.anchoredPosition = new Vector2(16f, 12f);
        tooltipPriceRect.sizeDelta = new Vector2(200f, 22f);

        CanvasGroup tooltipGroup = tooltipGo.GetComponent<CanvasGroup>();
        tooltipGroup.alpha = 0f;
        tooltipGroup.blocksRaycasts = false;
        tooltipGroup.interactable = false;

        InventoryTooltipUI tooltip = tooltipGo.GetComponent<InventoryTooltipUI>();
        Assign(tooltip, serialized =>
        {
            serialized.FindProperty("root").objectReferenceValue = tooltipRect;
            serialized.FindProperty("canvasGroup").objectReferenceValue = tooltipGroup;
            serialized.FindProperty("nameText").objectReferenceValue = tooltipName;
            serialized.FindProperty("typeText").objectReferenceValue = tooltipType;
            serialized.FindProperty("descriptionText").objectReferenceValue = tooltipDescription;
            serialized.FindProperty("priceText").objectReferenceValue = tooltipPrice;
            serialized.FindProperty("background").objectReferenceValue = tooltipBackground;
            serialized.FindProperty("border").objectReferenceValue = tooltipBorder;
            serialized.FindProperty("theme").objectReferenceValue = theme;
        });

        // --- Подтверждение выброса ---
        GameObject confirmGo = NewUiObject("ConfirmDialog", window.transform,
            typeof(Image), typeof(CanvasGroup), typeof(InventoryConfirmDialog));
        Stretch(confirmGo.GetComponent<RectTransform>());

        Image confirmDim = confirmGo.GetComponent<Image>();
        ConfigureImage(confirmDim, new Color(0f, 0f, 0f, 0.65f), false, true);

        Image confirmFrame = CreateImage("Frame", confirmGo.transform, theme.windowBackground, true, true);
        RectTransform confirmFrameRect = confirmFrame.rectTransform;
        confirmFrameRect.anchorMin = new Vector2(0.5f, 0.5f);
        confirmFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
        confirmFrameRect.pivot = new Vector2(0.5f, 0.5f);
        confirmFrameRect.sizeDelta = new Vector2(460f, 200f);
        confirmFrameRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI confirmMessage = CreateText("Message", confirmFrame.transform, string.Empty,
            TextSizeBody, TextAlignmentOptions.Center, theme.textColor);
        RectTransform confirmMessageRect = confirmMessage.rectTransform;
        confirmMessageRect.anchorMin = new Vector2(0f, 1f);
        confirmMessageRect.anchorMax = new Vector2(1f, 1f);
        confirmMessageRect.pivot = new Vector2(0.5f, 1f);
        confirmMessageRect.anchoredPosition = new Vector2(0f, -24f);
        confirmMessageRect.sizeDelta = new Vector2(-48f, 80f);

        Button confirmButton = CreateButton(confirmFrame.transform, "ConfirmButton", "Выбросить", theme);
        RectTransform confirmButtonRect = confirmButton.GetComponent<RectTransform>();
        confirmButtonRect.anchorMin = new Vector2(0.5f, 0f);
        confirmButtonRect.anchorMax = new Vector2(0.5f, 0f);
        confirmButtonRect.pivot = new Vector2(1f, 0f);
        confirmButtonRect.anchoredPosition = new Vector2(-8f, 20f);
        confirmButtonRect.sizeDelta = new Vector2(170f, 44f);

        Button cancelButton = CreateButton(confirmFrame.transform, "CancelButton", "Отмена", theme);
        RectTransform cancelButtonRect = cancelButton.GetComponent<RectTransform>();
        cancelButtonRect.anchorMin = new Vector2(0.5f, 0f);
        cancelButtonRect.anchorMax = new Vector2(0.5f, 0f);
        cancelButtonRect.pivot = new Vector2(0f, 0f);
        cancelButtonRect.anchoredPosition = new Vector2(8f, 20f);
        cancelButtonRect.sizeDelta = new Vector2(170f, 44f);

        CanvasGroup confirmGroup = confirmGo.GetComponent<CanvasGroup>();
        confirmGroup.alpha = 0f;
        confirmGroup.blocksRaycasts = false;
        confirmGroup.interactable = false;

        InventoryConfirmDialog confirmDialog = confirmGo.GetComponent<InventoryConfirmDialog>();
        TextMeshProUGUI confirmLabel = confirmButton.GetComponentInChildren<TextMeshProUGUI>(true);
        Assign(confirmDialog, serialized =>
        {
            serialized.FindProperty("root").objectReferenceValue = confirmGo;
            serialized.FindProperty("canvasGroup").objectReferenceValue = confirmGroup;
            serialized.FindProperty("messageText").objectReferenceValue = confirmMessage;
            serialized.FindProperty("confirmLabelText").objectReferenceValue = confirmLabel;
            serialized.FindProperty("confirmButton").objectReferenceValue = confirmButton;
            serialized.FindProperty("cancelButton").objectReferenceValue = cancelButton;
            serialized.FindProperty("theme").objectReferenceValue = theme;
        });

        // --- Слой перетаскивания ---
        GameObject dragLayer = NewUiObject("DragLayer", window.transform);
        Stretch(dragLayer.GetComponent<RectTransform>());

        Image dragIcon = CreateImage("DragIcon", dragLayer.transform, Color.white, true, false);
        RectTransform dragIconRect = dragIcon.rectTransform;
        dragIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        dragIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        dragIconRect.pivot = new Vector2(0.5f, 0.5f);
        dragIconRect.sizeDelta = theme.slotSize;
        dragIcon.enabled = false;

        Assign(dragController, serialized =>
        {
            serialized.FindProperty("dragLayer").objectReferenceValue = dragLayer.GetComponent<RectTransform>();
            serialized.FindProperty("dragIcon").objectReferenceValue = dragIcon;
            serialized.FindProperty("canvas").objectReferenceValue = canvasGo.GetComponent<Canvas>();
            serialized.FindProperty("tooltip").objectReferenceValue = tooltip;
            serialized.FindProperty("theme").objectReferenceValue = theme;
        });

        Assign(gridView, serialized =>
        {
            serialized.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
            serialized.FindProperty("slotParent").objectReferenceValue = gridGo.transform;
            serialized.FindProperty("inventory").objectReferenceValue = inventory;
            serialized.FindProperty("dragController").objectReferenceValue = dragController;
            serialized.FindProperty("tooltip").objectReferenceValue = tooltip;
            serialized.FindProperty("panel").objectReferenceValue = panel;
            serialized.FindProperty("theme").objectReferenceValue = theme;
        });

        Assign(panel, serialized =>
        {
            serialized.FindProperty("windowRoot").objectReferenceValue = window;
            serialized.FindProperty("inventoryUI").objectReferenceValue = gridView;
            serialized.FindProperty("inventory").objectReferenceValue = inventory;
            serialized.FindProperty("tooltip").objectReferenceValue = tooltip;
            serialized.FindProperty("dragController").objectReferenceValue = dragController;
            serialized.FindProperty("confirmDialog").objectReferenceValue = confirmDialog;
            serialized.FindProperty("theme").objectReferenceValue = theme;

            SerializedProperty slotsProperty = serialized.FindProperty("equipmentSlots");
            slotsProperty.ClearArray();
            for (int i = 0; i < equipmentSlots.Count; i++)
            {
                slotsProperty.InsertArrayElementAtIndex(i);
                slotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = equipmentSlots[i];
            }

            serialized.FindProperty("sortButton").objectReferenceValue = sortButton;
            serialized.FindProperty("dropButton").objectReferenceValue = dropButton;
            serialized.FindProperty("deleteButton").objectReferenceValue = deleteButton;
            serialized.FindProperty("useButton").objectReferenceValue = useButton;
            serialized.FindProperty("closeButton").objectReferenceValue = closeButton;
            serialized.FindProperty("footerText").objectReferenceValue = footer;
            serialized.FindProperty("toggleKey").intValue = (int)Key.I;

            SerializedProperty disableList = serialized.FindProperty("disableWhileOpen");
            disableList.ClearArray();

            AddObjectToList(disableList, playerController);
            AddObjectToList(disableList, playerInteraction);
        });

        Assign(inventory, serialized =>
        {
            // Стартовый инвентарь пуст: всё тестовое лежит в контейнере перед игроком.
            SerializedProperty starting = serialized.FindProperty("startingItems");
            starting.ClearArray();
        });

        WireSaveSystem(inventory, startingItems);

        SetupCharacterVisuals(inventory);
        SetupLoot(inventory, panel, deleteButton, playerController, startingItems);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    private static void WireSaveSystem(Inventory inventory, List<ItemData> items)
    {
        InventorySaveSystem saveSystem =
            Object.FindFirstObjectByType<InventorySaveSystem>(FindObjectsInactive.Include);
        if (saveSystem == null) return;

        Assign(saveSystem, serialized =>
        {
            serialized.FindProperty("inventory").objectReferenceValue = inventory;

            SerializedProperty allItems = serialized.FindProperty("allItems");
            for (int i = 0; i < items.Count; i++)
            {
                EnsureObjectInList(allItems, items[i]);
            }
        });
    }

    private static List<ItemData> ReloadItems(List<ItemData> items)
    {
        List<ItemData> reloaded = new List<ItemData>();
        if (items == null) return reloaded;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null) continue;

            string path = AssetDatabase.GetAssetPath(items[i]);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null) reloaded.Add(item);
        }

        return reloaded;
    }

    // ------------------------------------------------- Визуальная экипировка

    private struct VisualBinding
    {
        public EquipmentSlotType slot;
        public string visualId;

        public VisualBinding(EquipmentSlotType slot, string visualId)
        {
            this.slot = slot;
            this.visualId = visualId;
        }
    }

    /// <summary>
    /// Раскладывает существующую одежду BasicHero_M по папкам слотов и регистрирует
    /// её в CharacterEquipmentVisuals. Второй скелет и новые меши не создаются.
    /// </summary>
    private static void SetupCharacterVisuals(Inventory inventory)
    {
        GameObject character = FindSceneGameObject(CharacterName);
        if (character == null)
        {
            Debug.LogError($"[InventorySetup] В сцене не найден {CharacterName}.");
            return;
        }

        Transform visualsRoot = FindOrCreateChild(character.transform, VisualsRootName);
        Transform armorFolder = FindOrCreateChild(visualsRoot, "Armor");
        Transform helmetFolder = FindOrCreateChild(visualsRoot, "Helmet");
        Transform pantsFolder = FindOrCreateChild(visualsRoot, "Pants");
        Transform accessoryFolder = FindOrCreateChild(visualsRoot, "Accessory");

        VisualBinding[] bindings =
        {
            new VisualBinding(EquipmentSlotType.Armor, "M_Swordsman_Top"),
            new VisualBinding(EquipmentSlotType.Armor, "M_Archer_Top"),
            new VisualBinding(EquipmentSlotType.Armor, "M_Mage_Top"),
            new VisualBinding(EquipmentSlotType.Armor, "M_Fighter_Top"),
            new VisualBinding(EquipmentSlotType.Armor, "M_Rogue_Top"),
            new VisualBinding(EquipmentSlotType.Armor, "M_Warlock_Top"),
            new VisualBinding(EquipmentSlotType.Armor, "M_Warlock_TopCape"),

            new VisualBinding(EquipmentSlotType.Helmet, "M_Mage_Headwear"),
            new VisualBinding(EquipmentSlotType.Helmet, "M_Warlock_Hood"),

            new VisualBinding(EquipmentSlotType.Pants, "M_Swordsman_Bottom"),
            new VisualBinding(EquipmentSlotType.Pants, "M_Archer_Bottom"),
            new VisualBinding(EquipmentSlotType.Pants, "M_Mage_Bottom"),
            new VisualBinding(EquipmentSlotType.Pants, "M_Fighter_Bottom"),
            new VisualBinding(EquipmentSlotType.Pants, "M_Rogue_Bottom"),
            new VisualBinding(EquipmentSlotType.Pants, "M_Warlock_Bottom"),

            new VisualBinding(EquipmentSlotType.Accessory, "M_SlingBag"),
            new VisualBinding(EquipmentSlotType.Accessory, "M_ArrowQuiver"),
            new VisualBinding(EquipmentSlotType.Accessory, "M_Archer_Cape"),
            new VisualBinding(EquipmentSlotType.Accessory, "M_Warlock_Cape")
        };

        List<GameObject> resolved = new List<GameObject>();
        List<VisualBinding> resolvedBindings = new List<VisualBinding>();

        for (int i = 0; i < bindings.Length; i++)
        {
            VisualBinding binding = bindings[i];
            Transform folder = GetSlotFolder(binding.slot, armorFolder, helmetFolder, pantsFolder, accessoryFolder);

            GameObject visual = binding.slot == EquipmentSlotType.Armor && binding.visualId == "M_Archer_Top"
                ? FindArcherTop(character.transform, armorFolder)
                : ResolveVisual(character.transform, folder, binding.visualId);

            if (visual == null)
            {
                Debug.LogError($"[InventorySetup] Не найден объект одежды {binding.visualId}.");
                continue;
            }

            if (visual.transform.parent != folder) visual.transform.SetParent(folder, true);

            resolved.Add(visual);
            resolvedBindings.Add(binding);
        }

        // Стрелы — часть колчана: переносим их под него, чтобы включались вместе.
        GameObject quiver = FindResolved(resolved, "M_ArrowQuiver");
        GameObject arrow = ResolveVisual(character.transform, accessoryFolder, "M_Arrow");
        if (quiver != null && arrow != null && arrow.transform.parent != quiver.transform)
            arrow.transform.SetParent(quiver.transform, true);

        // В модели есть парные копии (ArcherCapeTopBody): оставляем по одной.
        DisableDuplicates(character.transform, "M_Archer_Top", FindResolved(resolved, "M_Archer_Top"));
        DisableDuplicates(character.transform, "M_ArrowQuiver", quiver);
        DisableDuplicates(character.transform, "M_Arrow", arrow);

        CharacterEquipmentVisuals visuals = character.GetComponent<CharacterEquipmentVisuals>();
        if (visuals == null) visuals = character.AddComponent<CharacterEquipmentVisuals>();

        Assign(visuals, serialized =>
        {
            SerializedProperty list = serialized.FindProperty("visuals");
            list.ClearArray();

            for (int i = 0; i < resolved.Count; i++)
            {
                VisualBinding binding = resolvedBindings[i];

                list.InsertArrayElementAtIndex(i);
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("slot").intValue = (int)binding.slot;
                entry.FindPropertyRelative("visualId").stringValue = binding.visualId;
                entry.FindPropertyRelative("visual").objectReferenceValue = resolved[i];
            }
        });

        // В сцене персонаж стоит в базовой одежде: слоты пусты, пока ничего не надето.
        for (int i = 0; i < resolved.Count; i++)
        {
            if (resolved[i] != null) resolved[i].SetActive(false);
        }

        PlayerController playerController =
            Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (playerController == null)
        {
            Debug.LogError("[InventorySetup] В сцене не найден PlayerController — мост экипировки не создан.");
            return;
        }

        PlayerEquipmentBridge bridge = playerController.GetComponent<PlayerEquipmentBridge>();
        if (bridge == null) bridge = playerController.gameObject.AddComponent<PlayerEquipmentBridge>();

        Assign(bridge, serialized =>
        {
            serialized.FindProperty("inventory").objectReferenceValue = inventory;
            serialized.FindProperty("visuals").objectReferenceValue = visuals;
        });
    }

    /// <summary>
    /// Арчерский верх ищем сначала в ArcherTopBody, затем в папке брони:
    /// после первого запуска объект уже перенесён и старого пути нет.
    /// </summary>
    private static GameObject FindArcherTop(Transform character, Transform armorFolder)
    {
        // Сначала папка брони: после первого запуска объект уже перенесён,
        // а в ArcherTopBody/ArcherCapeTopBody остаются только дубли.
        GameObject moved = FindChild(armorFolder, "M_Archer_Top");
        if (moved != null) return moved;

        Transform body = FindChildTransform(character, "ArcherTopBody");
        if (body != null)
        {
            GameObject direct = FindChild(body, "M_Archer_Top");
            if (direct != null) return direct;
        }

        return null;
    }

    /// <summary>
    /// Ищет вещь сначала в папке слота, затем где угодно у персонажа.
    /// Так повторный запуск сетапа не хватает дубли из выключенных обёрток.
    /// </summary>
    private static GameObject ResolveVisual(Transform character, Transform folder, string visualId)
    {
        GameObject inFolder = FindChild(folder, visualId);
        if (inFolder != null) return inFolder;

        return FindChild(character, visualId);
    }

    private static GameObject FindResolved(List<GameObject> resolved, string name)
    {
        for (int i = 0; i < resolved.Count; i++)
        {
            if (resolved[i] != null && resolved[i].name == name) return resolved[i];
        }

        return null;
    }

    /// <summary>Выключает все копии объекта с этим именем, кроме выбранной.</summary>
    private static void DisableDuplicates(Transform root, string name, GameObject keep)
    {
        List<GameObject> found = new List<GameObject>();
        CollectChildren(root, name, found);

        for (int i = 0; i < found.Count; i++)
        {
            if (found[i] == null || found[i] == keep) continue;
            found[i].SetActive(false);
        }
    }

    private static void CollectChildren(Transform root, string name, List<GameObject> result)
    {
        if (root == null) return;
        if (root.name == name) result.Add(root.gameObject);

        for (int i = 0; i < root.childCount; i++)
        {
            CollectChildren(root.GetChild(i), name, result);
        }
    }

    private static Transform GetSlotFolder(EquipmentSlotType slot, Transform armor, Transform helmet,
        Transform pants, Transform accessory)
    {
        switch (slot)
        {
            case EquipmentSlotType.Helmet: return helmet;
            case EquipmentSlotType.Pants: return pants;
            case EquipmentSlotType.Accessory: return accessory;
            default: return armor;
        }
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform existing = FindChildTransform(parent, name);
        if (existing != null) return existing;

        GameObject created = new GameObject(name);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    // ---------------------------------------------------------------- Лут

    /// <summary>
    /// Собирает всё, что нужно луту: материал контура, префаб контейнера, сервис,
    /// стартовый контейнер, панель и компоненты игрока.
    /// </summary>
    private static void SetupLoot(Inventory inventory, InventoryPanelUI panel, Button deleteButton,
        PlayerController playerController, List<ItemData> items)
    {
        Material outline = EnsureOutlineMaterial();
        if (outline == null) return;

        GameObject containerPrefab = CreateLootContainerPrefab(outline);
        if (containerPrefab == null) return;

        WorldLootService service = EnsureLootService(containerPrefab);
        CreateStarterContainer(service, playerController, items);

        InventoryTheme theme = AssetDatabase.LoadAssetAtPath<InventoryTheme>(ThemePath);
        GameObject rowPrefab = CreateLootRowPrefab(theme);

        LootPickupController pickupController = EnsurePickupController(playerController, inventory);
        LootPanelUI lootPanel = BuildLootPanel(theme, inventory, pickupController, rowPrefab);
        EnsurePointerController(playerController, lootPanel, panel, pickupController);

        Assign(panel, serialized =>
        {
            serialized.FindProperty("lootService").objectReferenceValue = service;
            serialized.FindProperty("dropOrigin").objectReferenceValue =
                playerController != null ? playerController.transform : null;
            serialized.FindProperty("deleteButton").objectReferenceValue = deleteButton;

            // Пока инвентарь открыт, подход к луту должен остановиться.
            SerializedProperty disableList = serialized.FindProperty("disableWhileOpen");
            if (pickupController != null) EnsureObjectInList(disableList, pickupController);
        });

        // Без надетого оружия в руках должны остаться кулаки.
        WeaponData fists = AssetDatabase.LoadAssetAtPath<WeaponData>(FistsWeaponPath);
        if (fists == null)
        {
            Debug.LogWarning($"[InventorySetup] Не найден {FistsWeaponPath} — снятие оружия оставит прежнюю модель.");
            return;
        }

        EquipmentSlotUI weaponSlot = FindEquipmentSlot(EquipmentSlotType.Weapon);
        if (weaponSlot == null) return;

        Assign(weaponSlot, serialized =>
            serialized.FindProperty("unarmedWeapon").objectReferenceValue = fists);
    }

    private static Material EnsureOutlineMaterial()
    {
        Shader shader = Shader.Find("Inventory/Outline");
        if (shader == null)
        {
            Debug.LogError("[InventorySetup] Шейдер Inventory/Outline не найден. Переимпортируйте Assets/Scripts/Inventary/Shaders.");
            return null;
        }

        EnsureFolder("Assets/Scripts/Inventary/Materials");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, OutlineMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.SetColor("_OutlineColor", new Color(1f, 0.82f, 0.3f, 1f));
        material.SetFloat("_OutlineWidth", 0.06f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreateLootContainerPrefab(Material outline)
    {
        EnsureFolder(PrefabFolder);

        GameObject root = new GameObject("WorldLootContainer",
            typeof(WorldLootContainer), typeof(OutlineTarget), typeof(SphereCollider));

        SphereCollider collider = root.GetComponent<SphereCollider>();
        collider.center = new Vector3(0f, 0.4f, 0f);
        collider.radius = 0.7f;

        GameObject effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemEffectPrefabPath);
        if (effectPrefab != null)
        {
            GameObject effect = (GameObject)PrefabUtility.InstantiatePrefab(effectPrefab, root.transform);
            effect.name = "ItemEffect";
            effect.transform.localPosition = Vector3.zero;
        }
        else
        {
            Debug.LogWarning($"[InventorySetup] Не найден {ItemEffectPrefabPath} — контейнер будет без визуала.");
        }

        OutlineTarget target = root.GetComponent<OutlineTarget>();
        Assign(target, serialized =>
            serialized.FindProperty("outlineMaterial").objectReferenceValue = outline);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, LootContainerPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static WorldLootService EnsureLootService(GameObject containerPrefab)
    {
        GameObject serviceGo = FindSceneGameObject(LootServiceName);

        if (serviceGo == null)
        {
            serviceGo = new GameObject(LootServiceName);
            serviceGo.AddComponent<WorldLootService>();
        }

        WorldLootService service = serviceGo.GetComponent<WorldLootService>();
        if (service == null) service = serviceGo.AddComponent<WorldLootService>();

        Assign(service, serialized =>
        {
            serialized.FindProperty("containerPrefab").objectReferenceValue = containerPrefab;
            serialized.FindProperty("dropDistance").floatValue = 1.5f;
            serialized.FindProperty("mergeRadius").floatValue = 2.5f;
            serialized.FindProperty("groundRayHeight").floatValue = 5f;
            serialized.FindProperty("groundMask").intValue = ~0;
        });

        return service;
    }

    private static void CreateStarterContainer(WorldLootService service, PlayerController playerController,
        List<ItemData> items)
    {
        // Повторный запуск сетапа не должен плодить контейнеры.
        GameObject old = FindSceneGameObject(StarterContainerName);
        if (old != null) Object.DestroyImmediate(old);

        if (playerController == null)
        {
            Debug.LogError("[InventorySetup] Нет PlayerController — стартовый контейнер не создан.");
            return;
        }

        Vector3 point = playerController.transform.position +
                        playerController.transform.forward * StarterLootDistance;

        WorldLootContainer container = service.SpawnContainer(point);
        if (container == null) return;

        container.name = StarterContainerName;

        if (items == null) return;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null) container.Add(items[i], 1);
        }
    }

    private static GameObject CreateLootRowPrefab(InventoryTheme theme)
    {
        EnsureFolder(PrefabFolder);

        GameObject root = NewUiObject("LootRow",
            null, typeof(Image), typeof(LayoutElement), typeof(LootRowUI));

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(0f, LootRowHeight);

        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredHeight = LootRowHeight;
        layout.minHeight = LootRowHeight;

        ConfigureImage(root.GetComponent<Image>(), theme.windowBackground, true, true);

        Image icon = CreateImage("Icon", root.transform, Color.white, false, false);
        PlaceLeftMiddle(icon.rectTransform, 8f, 40f, 40f);

        Image placeholder = CreateImage("Placeholder", root.transform, theme.placeholderColor, true, false);
        PlaceLeftMiddle(placeholder.rectTransform, 8f, 40f, 40f);

        TextMeshProUGUI nameText = CreateText("Name", root.transform, string.Empty,
            TextSizeBody, TextAlignmentOptions.Left, theme.textColor);
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 0.5f);
        nameRect.anchorMax = new Vector2(1f, 0.5f);
        nameRect.pivot = new Vector2(0f, 0.5f);
        nameRect.offsetMin = new Vector2(56f, -14f);
        nameRect.offsetMax = new Vector2(-150f, 14f);

        TextMeshProUGUI countText = CreateText("Count", root.transform, string.Empty,
            TextSizeBody, TextAlignmentOptions.Right, theme.mutedTextColor);
        RectTransform countRect = countText.rectTransform;
        countRect.anchorMin = new Vector2(1f, 0.5f);
        countRect.anchorMax = new Vector2(1f, 0.5f);
        countRect.pivot = new Vector2(1f, 0.5f);
        countRect.anchoredPosition = new Vector2(-104f, 0f);
        countRect.sizeDelta = new Vector2(44f, 24f);

        Button takeButton = CreateButton(root.transform, "TakeButton", "Забрать", theme);
        RectTransform takeRect = takeButton.GetComponent<RectTransform>();
        takeRect.anchorMin = new Vector2(1f, 0.5f);
        takeRect.anchorMax = new Vector2(1f, 0.5f);
        takeRect.pivot = new Vector2(1f, 0.5f);
        takeRect.anchoredPosition = new Vector2(-8f, 0f);
        takeRect.sizeDelta = new Vector2(92f, 38f);

        LootRowUI row = root.GetComponent<LootRowUI>();
        Assign(row, serialized =>
        {
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("placeholder").objectReferenceValue = placeholder;
            serialized.FindProperty("nameText").objectReferenceValue = nameText;
            serialized.FindProperty("countText").objectReferenceValue = countText;
            serialized.FindProperty("takeButton").objectReferenceValue = takeButton;
        });

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, LootRowPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static LootPickupController EnsurePickupController(PlayerController playerController,
        Inventory inventory)
    {
        if (playerController == null) return null;

        GameObject playerGo = playerController.gameObject;
        LootPickupController pickup = playerGo.GetComponent<LootPickupController>();
        if (pickup == null) pickup = playerGo.AddComponent<LootPickupController>();

        Assign(pickup, serialized =>
        {
            serialized.FindProperty("inventory").objectReferenceValue = inventory;
            serialized.FindProperty("agent").objectReferenceValue = playerGo.GetComponent<NavMeshAgent>();
            serialized.FindProperty("animator").objectReferenceValue = playerGo.GetComponent<Animator>();
            serialized.FindProperty("speedParameter").stringValue = "Speed";
            serialized.FindProperty("pickupRange").floatValue = 2.5f;
            serialized.FindProperty("approachTimeout").floatValue = 8f;
            serialized.FindProperty("noProgressTimeout").floatValue = 2f;
        });

        return pickup;
    }

    private static LootPanelUI BuildLootPanel(InventoryTheme theme, Inventory inventory,
        LootPickupController pickupController, GameObject rowPrefab)
    {
        GameObject canvasGo = FindSceneGameObject(CanvasName);
        if (canvasGo == null)
        {
            Debug.LogError("[InventorySetup] Нет Canvas — панель лута не создана.");
            return null;
        }

        GameObject old = FindSceneGameObject(LootPanelName);
        if (old != null) Object.DestroyImmediate(old);

        GameObject panelGo = NewUiObject(LootPanelName, canvasGo.transform,
            typeof(Image), typeof(CanvasGroup), typeof(LootPanelUI));

        RectTransform panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.sizeDelta = new Vector2(LootPanelWidth, LootPanelHeight);
        panelRect.anchoredPosition = Vector2.zero;

        ConfigureImage(panelGo.GetComponent<Image>(), theme.windowBackground, true, true);

        Image border = CreateImage("Border", panelGo.transform, theme.borderColor, true, false);
        Stretch(border.rectTransform);

        TextMeshProUGUI title = CreateText("Title", panelGo.transform, "Содержимое",
            TextSizeBody + 2f, TextAlignmentOptions.Left, theme.textColor);
        PlaceTopLeft(title.rectTransform, 16f, -12f, new Vector2(LootPanelWidth - 32f, 28f));

        GameObject listGo = NewUiObject("List", panelGo.transform,
            typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        RectTransform listRect = listGo.GetComponent<RectTransform>();
        PlaceTopLeft(listRect, 16f, -48f, new Vector2(LootPanelWidth - 32f, LootPanelHeight - 48f - 68f));

        Image listImage = listGo.GetComponent<Image>();
        listImage.color = new Color(0f, 0f, 0f, 0.25f);
        listImage.raycastTarget = true;

        GameObject contentGo = NewUiObject("Content", listGo.transform);
        RectTransform contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 4f;
        contentLayout.padding = new RectOffset(4, 4, 4, 4);
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = listGo.GetComponent<ScrollRect>();
        scroll.viewport = listRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;

        Button takeAllButton = CreateButton(panelGo.transform, "TakeAllButton", "Забрать всё", theme);
        RectTransform takeAllRect = takeAllButton.GetComponent<RectTransform>();
        takeAllRect.anchorMin = new Vector2(1f, 0f);
        takeAllRect.anchorMax = new Vector2(1f, 0f);
        takeAllRect.pivot = new Vector2(1f, 0f);
        takeAllRect.anchoredPosition = new Vector2(-16f, 16f);
        takeAllRect.sizeDelta = new Vector2(150f, 40f);

        TextMeshProUGUI message = CreateText("Message", panelGo.transform, string.Empty,
            TextSizeSmall, TextAlignmentOptions.Left, theme.highlightColor);
        RectTransform messageRect = message.rectTransform;
        messageRect.anchorMin = new Vector2(0f, 0f);
        messageRect.anchorMax = new Vector2(0f, 0f);
        messageRect.pivot = new Vector2(0f, 0f);
        messageRect.anchoredPosition = new Vector2(16f, 16f);
        messageRect.sizeDelta = new Vector2(LootPanelWidth - 190f, 40f);
        message.gameObject.SetActive(false);

        CanvasGroup group = panelGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        LootPanelUI panel = panelGo.GetComponent<LootPanelUI>();
        Assign(panel, serialized =>
        {
            serialized.FindProperty("root").objectReferenceValue = panelGo;
            serialized.FindProperty("canvasGroup").objectReferenceValue = group;
            serialized.FindProperty("panelRect").objectReferenceValue = panelRect;
            serialized.FindProperty("listContent").objectReferenceValue = contentRect;
            serialized.FindProperty("rowPrefab").objectReferenceValue =
                rowPrefab != null ? rowPrefab.GetComponent<LootRowUI>() : null;
            serialized.FindProperty("takeAllButton").objectReferenceValue = takeAllButton;
            serialized.FindProperty("titleText").objectReferenceValue = title;
            serialized.FindProperty("messageText").objectReferenceValue = message;
            serialized.FindProperty("inventory").objectReferenceValue = inventory;
            serialized.FindProperty("pickupController").objectReferenceValue = pickupController;
        });

        // В сцене панель не нужна: показывается только при наведении на лут.
        panelGo.SetActive(false);
        return panel;
    }

    private static void EnsurePointerController(PlayerController playerController,
        LootPanelUI lootPanel, InventoryPanelUI inventoryPanel, LootPickupController pickupController)
    {
        if (playerController == null) return;

        GameObject playerGo = playerController.gameObject;
        LootPointerController pointer = playerGo.GetComponent<LootPointerController>();
        if (pointer == null) pointer = playerGo.AddComponent<LootPointerController>();

        Camera camera = Camera.main;
        if (camera == null) camera = playerController.GetComponentInChildren<Camera>(true);

        Assign(pointer, serialized =>
        {
            serialized.FindProperty("playerCamera").objectReferenceValue = camera;
            serialized.FindProperty("panel").objectReferenceValue = lootPanel;
            serialized.FindProperty("inventoryPanel").objectReferenceValue = inventoryPanel;
            serialized.FindProperty("pickupController").objectReferenceValue = pickupController;
            serialized.FindProperty("rayMask").intValue = ~0;
            serialized.FindProperty("maxDistance").floatValue = 100f;
            serialized.FindProperty("hideDelay").floatValue = 0.2f;
        });
    }

    private static EquipmentSlotUI FindEquipmentSlot(EquipmentSlotType slotType)
    {
        EquipmentSlotUI[] slots = Object.FindObjectsByType<EquipmentSlotUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].SlotType == slotType) return slots[i];
        }

        return null;
    }

    // -------------------------------------------------------------- UI-хелперы

    private static GameObject NewUiObject(string name, Transform parent, params System.Type[] components)
    {
        List<System.Type> types = new List<System.Type> { typeof(RectTransform) };
        if (components != null) types.AddRange(components);

        GameObject go = new GameObject(name, types.ToArray());
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private static Image CreateImage(string name, Transform parent, Color color, bool sliced, bool raycast)
    {
        GameObject go = NewUiObject(name, parent, typeof(Image));
        Image image = go.GetComponent<Image>();
        ConfigureImage(image, color, sliced, raycast);
        return image;
    }

    private static void ConfigureImage(Image image, Color color, bool sliced, bool raycast)
    {
        image.color = color;
        image.raycastTarget = raycast;

        if (!sliced) return;

        Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltinSpritePath);
        if (sprite == null) return;

        image.sprite = sprite;
        image.type = Image.Type.Sliced;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text,
        float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = NewUiObject(name, parent, typeof(TextMeshProUGUI));
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;

        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    private static Button CreateButton(Transform parent, string name, string label, InventoryTheme theme)
    {
        GameObject go = NewUiObject(name, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
        Image image = go.GetComponent<Image>();
        ConfigureImage(image, new Color(0.16f, 0.13f, 0.10f, 1f), true, true);

        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.preferredWidth = 156f;
        layout.preferredHeight = 44f;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = CreateText("Label", go.transform, label,
            TextSizeBody, TextAlignmentOptions.Center, theme.textColor);
        Stretch(text.rectTransform);

        return button;
    }

    private static void Stretch(RectTransform rect, float padding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    private static void PlaceTopLeft(RectTransform rect, float x, float y, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = size;
    }

    private static void PlaceTopRight(RectTransform rect, float x, float y, Vector2 size)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = size;
    }

    private static void PlaceLeftMiddle(RectTransform rect, float x, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, height);
    }

    /// <summary>Ищет объект по имени в глубину, включая выключенные.</summary>
    private static GameObject FindChild(Transform root, string name)
    {
        Transform found = FindChildTransform(root, name);
        return found != null ? found.gameObject : null;
    }

    private static Transform FindChildTransform(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildTransform(root.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }

    // ------------------------------------------------------------- Служебное

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(folder);

        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static bool OpenSceneIfNeeded(string path)
    {
        Scene active = EditorSceneManager.GetActiveScene();
        if (active.path == path) return true;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        return true;
    }

    private static GameObject FindSceneGameObject(string name)
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate != null && candidate.name == name && candidate.scene.IsValid())
                return candidate;
        }

        return null;
    }

    private static void AddObjectToList(SerializedProperty property, Object value)
    {
        if (value == null) return;

        int index = property.arraySize;
        property.InsertArrayElementAtIndex(index);
        property.GetArrayElementAtIndex(index).objectReferenceValue = value;
    }

    private static void EnsureObjectInList(SerializedProperty property, Object value)
    {        if (value == null) return;

        for (int i = 0; i < property.arraySize; i++)
        {
            if (property.GetArrayElementAtIndex(i).objectReferenceValue == value) return;
        }

        int index = property.arraySize;
        property.InsertArrayElementAtIndex(index);
        property.GetArrayElementAtIndex(index).objectReferenceValue = value;
    }

    private delegate void SerializedAssignment(SerializedObject serializedObject);

    private static void Assign(Object target, SerializedAssignment assignment)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        assignment(serializedObject);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
