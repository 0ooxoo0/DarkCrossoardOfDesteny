using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель пяти способностей под Canvas. Слоты строятся в рантайме из prefab
/// AbilitySlot.prefab, при его отсутствии — кодом (fallback).
/// </summary>
public class AbilityBarUI : MonoBehaviour
{
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Canvas targetCanvas;

    private const int SlotCount = AbilitySystem.SlotCount;

    private readonly AbilitySlotUI[] slots = new AbilitySlotUI[SlotCount];

    private void Start()
    {
        if (abilitySystem == null)
            abilitySystem = FindFirstObjectByType<AbilitySystem>();

        if (targetCanvas == null) targetCanvas = GetComponent<Canvas>();
        if (targetCanvas == null) targetCanvas = FindFirstObjectByType<Canvas>();

        if (targetCanvas == null)
        {
            Debug.LogWarning("[AbilityBarUI] Не найден Canvas.");
            return;
        }

        BuildPanel();

        if (abilitySystem != null)
        {
            abilitySystem.OnAbilitiesChanged += RefreshAbilities;
            RefreshAbilities();
        }
    }

    private void OnDestroy()
    {
        if (abilitySystem != null)
            abilitySystem.OnAbilitiesChanged -= RefreshAbilities;
    }

    private void Update()
    {
        if (abilitySystem == null) return;

        for (int i = 0; i < SlotCount; i++)
        {
            AbilitySlotUI slot = slots[i];
            if (slot == null) continue;

            AbilityData ability = abilitySystem.GetAbility(i);
            float total = ability != null ? ability.cooldown : 0f;

            slot.SetCooldown(abilitySystem.GetCooldownRemaining(i), total);
            slot.SetAvailable(abilitySystem.IsAbilityAvailable(i));
        }
    }

    private void RefreshAbilities()
    {
        if (abilitySystem == null) return;

        for (int i = 0; i < SlotCount; i++)
        {
            AbilitySlotUI slot = slots[i];
            if (slot == null) continue;

            AbilityData ability = abilitySystem.GetAbility(i);
            KeyCode hotkey = ability != null
                ? ability.hotkey
                : (KeyCode)((int)KeyCode.Alpha1 + i);

            slot.SetAbility(ability, i, hotkey);
        }
    }

    private void BuildPanel()
    {
        GameObject panel = new GameObject(
            "AbilityBar",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(ContentSizeFitter));

        panel.transform.SetParent(targetCanvas.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 24f);

        Image background = panel.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.35f);
        background.raycastTarget = false;

        HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < SlotCount; i++)
            slots[i] = CreateSlot(i, panel.transform);
    }

    private AbilitySlotUI CreateSlot(int index, Transform parent)
    {
        if (slotPrefab != null)
        {
            GameObject instance = Instantiate(slotPrefab, parent);
            instance.name = "AbilitySlot_" + (index + 1);

            AbilitySlotUI slotUi = instance.GetComponent<AbilitySlotUI>();
            if (slotUi == null) slotUi = instance.AddComponent<AbilitySlotUI>();
            return slotUi;
        }

        return BuildFallbackSlot(index, parent);
    }

    private static AbilitySlotUI BuildFallbackSlot(int index, Transform parent)
    {
        GameObject root = new GameObject(
            "AbilitySlot_" + (index + 1),
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(AbilitySlotUI));

        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(80f, 80f);

        Image background = root.GetComponent<Image>();
        background.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        background.raycastTarget = false;

        GameObject iconGo = new GameObject(
            "Icon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
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

        TMP_Text keyLabel = BuildText(
            root.transform,
            "KeyLabel",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(28f, 24f),
            new Vector2(8f, -6f),
            TextAlignmentOptions.TopLeft,
            18f,
            false);

        TMP_Text cooldownLabel = BuildText(
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
        return slotUi;
    }

    private static TMP_Text BuildText(
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
}
