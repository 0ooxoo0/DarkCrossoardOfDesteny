using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AbilitySlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private TMP_Text keyLabel;
    [SerializeField] private TMP_Text cooldownLabel;
    [SerializeField] private Image daoBadge;
    [SerializeField] private TMP_Text qiCostLabel;

    private Color iconBaseColor = Color.white;
    private int slotIndex;
    private string blockedReason = string.Empty;
    private string tooltip = string.Empty;
    private TMP_Text runeLabel;
    private TMP_Text nameLabel;
    private AbilityData currentAbility;
    private DaoSystem daoSystem;

    public string BlockedReason => blockedReason;
    public string Tooltip => tooltip;

    /// <summary>Наведение на слот: AbilityBarUI показывает подсказку.</summary>
    public event Action<AbilitySlotUI, bool> HoverChanged;

    public void OnPointerEnter(PointerEventData eventData) => HoverChanged?.Invoke(this, true);
    public void OnPointerExit(PointerEventData eventData) => HoverChanged?.Invoke(this, false);

    // Используется и setup-скриптом (prefab), и рантайм-fallback в AbilityBarUI.
    public void SetVisualReferences(Image iconImage, Image overlayImage, TMP_Text keyText, TMP_Text cooldownText)
    {
        icon = iconImage;
        cooldownOverlay = overlayImage;
        keyLabel = keyText;
        cooldownLabel = cooldownText;
    }

    public void SetAbility(AbilityData ability, int slotIndex, KeyCode hotkey)
    {
        this.slotIndex = slotIndex;
        currentAbility = ability;

        if (icon != null)
        {
            if (ability != null && ability.icon != null)
            {
                icon.sprite = ability.icon;
                iconBaseColor = Color.white;
            }
            else
            {
                // Плейсхолдер: цветной прямоугольник по типу способности.
                icon.sprite = null;
                if (ability == null)
                    iconBaseColor = new Color(0.15f, 0.15f, 0.15f, 1f);
                else if (ability.type == AbilityType.Melee)
                    iconBaseColor = new Color(0.75f, 0.2f, 0.2f, 1f);
                else
                    iconBaseColor = new Color(0.2f, 0.35f, 0.9f, 1f);
            }

            icon.color = iconBaseColor;
            icon.enabled = ability != null;
        }

        SetRune(ability);
        SetShortName(ability);

        if (keyLabel != null)
            keyLabel.text = GetKeyLabel(hotkey, slotIndex);

        if (cooldownOverlay != null)
            cooldownOverlay.fillAmount = 0f;

        if (cooldownLabel != null)
            cooldownLabel.text = string.Empty;

        SetCultivationBadges(ability, daoSystem);
        BuildTooltip(ability);
    }

    /// <summary>Процедурная руна: первая буква названия на фоне цвета Дао. Иконок в проекте нет.</summary>
    private void SetRune(AbilityData ability)
    {
        if (runeLabel == null)
        {
            var go = new GameObject("Rune", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);

            runeLabel = go.GetComponent<TextMeshProUGUI>();
            runeLabel.alignment = TextAlignmentOptions.Center;
            runeLabel.fontSize = 34f;
            runeLabel.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null) runeLabel.font = TMP_Settings.defaultFontAsset;
        }

        bool show = ability != null && ability.icon == null && !string.IsNullOrEmpty(ability.displayName);
        runeLabel.gameObject.SetActive(show);
        if (!show) return;

        runeLabel.text = ability.displayName.Substring(0, 1).ToUpperInvariant();
        runeLabel.color = ability.requiredDao.HasValue
            ? DaoTypeInfo.GetColor(ability.requiredDao.Value)
            : Color.white;
    }

    /// <summary>Короткое имя под слотом: иконок нет, иначе слоты неразличимы.</summary>
    private void SetShortName(AbilityData ability)
    {
        if (nameLabel == null)
        {
            var go = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 18f);
            rect.anchoredPosition = new Vector2(0f, -2f);

            nameLabel = go.GetComponent<TextMeshProUGUI>();
            nameLabel.alignment = TextAlignmentOptions.Center;
            nameLabel.fontSize = 12f;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            nameLabel.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null) nameLabel.font = TMP_Settings.defaultFontAsset;
        }

        nameLabel.text = ability != null ? ability.displayName : string.Empty;
        nameLabel.color = new Color(0.85f, 0.85f, 0.9f, 1f);
    }

    /// <summary>Текст подсказки: имя, Дао, тип, Ци, КД, сила, требования и причина блокировки.</summary>
    private void BuildTooltip(AbilityData ability)
    {
        if (ability == null)
        {
            tooltip = "Пустой слот. Назначьте технику в экране скиллов (Esc → Навыки).";
            return;
        }

        var builder = new System.Text.StringBuilder();
        builder.Append(ability.displayName);

        if (ability.requiredDao.HasValue)
        {
            DaoType type = ability.requiredDao.Value;
            builder.Append("\nШкола: ").Append(DaoTypeInfo.GetDisplayName(type));
            builder.Append("\nАктивный Дао: ").Append(daoSystem != null && daoSystem.ActiveDao.HasValue
                ? DaoTypeInfo.GetDisplayName(daoSystem.ActiveDao.Value)
                : "нет");
        }

        builder.Append("\nТип: ").Append(ability.type == AbilityType.Melee ? "ближний бой" : "магия");
        builder.Append("\nРеалм: ").Append(Mathf.Max(0, ability.requiredRealmIndex) + 1);
        if (ability.requiredDaoLevel > 0) builder.Append("   ·   Дао ").Append(ability.requiredDaoLevel).Append(" ур.");
        builder.Append("\nЦи: ").Append(Mathf.Max(0, ability.qiCost));
        builder.Append("   ·   КД: ").Append(ability.cooldown.ToString("0.#")).Append(" с");
        if (ability.power > 0) builder.Append("   ·   сила: ").Append(ability.power);

        if (!string.IsNullOrEmpty(blockedReason))
            builder.Append("\nНе сработает: ").Append(blockedReason);

        tooltip = builder.ToString();
    }

    public void SetCooldown(float remaining, float total)
    {
        float fraction = total > 0.0001f ? Mathf.Clamp01(remaining / total) : 0f;

        if (cooldownOverlay != null)
            cooldownOverlay.fillAmount = fraction;

        if (cooldownLabel != null)
            cooldownLabel.text = remaining > 0.05f ? remaining.ToString("0.0") : string.Empty;
    }

    public void SetAvailable(bool available)
    {
        SetAvailable(available, string.Empty);
    }

    public void SetAvailable(bool available, string reason)
    {
        blockedReason = available ? string.Empty : (reason ?? string.Empty);
        BuildTooltip(currentAbility);

        if (icon != null)
        {
            Color color = iconBaseColor;
            color.a = available ? iconBaseColor.a : 0.35f;
            icon.color = color;
        }

        if (keyLabel != null)
        {
            Color color = keyLabel.color;
            color.a = available ? 1f : 0.4f;
            keyLabel.color = color;
        }
    }

    public void SetQiCostState(bool affordable)
    {
        if (qiCostLabel == null) return;
        qiCostLabel.color = affordable
            ? new Color(0.85f, 0.92f, 1f, 1f)
            : new Color(1f, 0.35f, 0.3f, 1f);
    }

    public void SetCultivationBadges(AbilityData ability, DaoSystem daoSystem)
    {
        this.daoSystem = daoSystem;
        EnsureBadges();

        if (daoBadge != null)
        {
            bool showDao = ability != null && ability.requiredDao.HasValue;
            daoBadge.gameObject.SetActive(showDao);
            if (showDao)
            {
                DaoType type = ability.requiredDao.Value;
                DaoPath path = daoSystem != null ? daoSystem.GetDaoPath(type) : null;
                daoBadge.sprite = path != null ? path.Icon : null;
                daoBadge.color = path != null ? path.Color : DaoTypeInfo.GetColor(type);
            }
        }

        if (qiCostLabel != null)
        {
            int cost = ability != null ? Mathf.Max(0, ability.qiCost) : 0;
            bool showCost = cost > 0;
            qiCostLabel.gameObject.SetActive(showCost);
            qiCostLabel.text = showCost ? cost.ToString() : string.Empty;
        }
    }

    private void EnsureBadges()
    {
        if (daoBadge == null)
        {
            var go = new GameObject("DaoBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(18f, 18f);
            rect.anchoredPosition = new Vector2(-4f, -4f);

            daoBadge = go.GetComponent<Image>();
            daoBadge.raycastTarget = false;
            daoBadge.preserveAspect = true;
        }

        if (qiCostLabel == null)
        {
            var go = new GameObject("QiCost", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(52f, 20f);
            rect.anchoredPosition = new Vector2(-4f, 4f);

            qiCostLabel = go.GetComponent<TextMeshProUGUI>();
            qiCostLabel.text = string.Empty;
            qiCostLabel.fontSize = 13f;
            qiCostLabel.alignment = TextAlignmentOptions.BottomRight;
            qiCostLabel.color = new Color(0.85f, 0.92f, 1f, 1f);
            qiCostLabel.raycastTarget = false;

            if (TMP_Settings.defaultFontAsset != null)
                qiCostLabel.font = TMP_Settings.defaultFontAsset;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        AbilitySystem system = FindFirstObjectByType<AbilitySystem>();
        if (system != null) system.ActivateAbility(slotIndex);
    }

    private static string GetKeyLabel(KeyCode hotkey, int slotIndex)
    {
        int alphaIndex = (int)hotkey - (int)KeyCode.Alpha1;
        if (alphaIndex >= 0 && alphaIndex <= 4)
            return (alphaIndex + 1).ToString();

        return (slotIndex + 1).ToString();
    }
}
