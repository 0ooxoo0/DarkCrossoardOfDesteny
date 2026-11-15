using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
    private const string TestDataFolder = "Assets/Inventary/TestData";
    private const string PrefabFolder = "Assets/Inventary/Prefabs";
    private const string ThemePath = TestDataFolder + "/InventoryTheme.asset";
    private const string SlotPrefabPath = PrefabFolder + "/InventorySlot.prefab";
    private const string EquipmentSlotPrefabPath = PrefabFolder + "/EquipmentSlot.prefab";

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
            AssetDatabase.LoadAssetAtPath<ItemData>(TestDataFolder + "/Sword_Ash.asset"),
            AssetDatabase.LoadAssetAtPath<ItemData>(TestDataFolder + "/Sword_Ember.asset"),
            AssetDatabase.LoadAssetAtPath<ItemData>(TestDataFolder + "/Potion_Healing.asset"),
            AssetDatabase.LoadAssetAtPath<ItemData>(TestDataFolder + "/Material_IronOre.asset")
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
        List<Sprite> sprites = LoadSprites("Assets/Inventary/Image/InventoryItems/Items1.png");

        ItemData swordAsh = CreateOrUpdateItem(
            TestDataFolder + "/Sword_Ash.asset",
            "test_sword_ash",
            "Пепельный клинок",
            "Тяжёлый меч из остывшего пепла. Пахнет костром.",
            ItemType.Weapon, ItemUseKind.None, 1, 12, sprites, 0);

        ItemData swordEmber = CreateOrUpdateItem(
            TestDataFolder + "/Sword_Ember.asset",
            "test_sword_ember",
            "Клинок алой зари",
            "Лёгкий клинок с багровым отблеском на кромке.",
            ItemType.Weapon, ItemUseKind.None, 1, 18, sprites, 1);

        ItemData potion = CreateOrUpdateItem(
            TestDataFolder + "/Potion_Healing.asset",
            "test_potion_healing",
            "Зелье лечения",
            "Тёплая мутная жидкость. Пока не лечит: ждёт систему здоровья.",
            ItemType.Consumable, ItemUseKind.Potion, 20, 4, sprites, 2);

        ItemData ore = CreateOrUpdateItem(
            TestDataFolder + "/Material_IronOre.asset",
            "test_material_iron_ore",
            "Железная руда",
            "Кусок руды с прожилками. Пригодится кузнецу.",
            ItemType.Misc, ItemUseKind.None, 50, 1, sprites, 3);

        return new List<ItemData> { swordAsh, swordEmber, potion, ore };
    }

    private static ItemData CreateOrUpdateItem(
        string assetPath, string itemId, string displayName, string description,
        ItemType type, ItemUseKind useKind, int maxStack, int sellPrice,
        List<Sprite> sprites, int spriteIndex)
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
        item.MarkMigrated(DataVersion);

        // Иконки берём из готового атласа, если он есть. Иначе останется [PLACEHOLDER].
        if (sprites != null && sprites.Count > 0)
        {
            item.icon = sprites[Mathf.Abs(spriteIndex) % sprites.Count];
        }

        EditorUtility.SetDirty(item);
        return item;
    }

    private static List<Sprite> LoadSprites(string texturePath)
    {
        List<Sprite> result = new List<Sprite>();
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite) result.Add(sprite);
        }

        return result;
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

        // Количества задаются явно и в том же порядке, что и тестовые предметы.
        int[] startingQuantities = { 1, 1, 5, 10 };

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
            EquipmentSlotType.Boots,
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
        Assign(confirmDialog, serialized =>
        {
            serialized.FindProperty("root").objectReferenceValue = confirmGo;
            serialized.FindProperty("canvasGroup").objectReferenceValue = confirmGroup;
            serialized.FindProperty("messageText").objectReferenceValue = confirmMessage;
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
            SerializedProperty starting = serialized.FindProperty("startingItems");
            starting.ClearArray();

            for (int i = 0; i < startingItems.Count; i++)
            {
                ItemData item = startingItems[i];
                if (item == null) continue;

                starting.InsertArrayElementAtIndex(starting.arraySize);
                SerializedProperty entry = starting.GetArrayElementAtIndex(starting.arraySize - 1);
                entry.FindPropertyRelative("item").objectReferenceValue = item;
                entry.FindPropertyRelative("quantity").intValue =
                    i < startingQuantities.Length ? startingQuantities[i] : 1;
            }
        });

        WireSaveSystem(inventory, startingItems);

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
