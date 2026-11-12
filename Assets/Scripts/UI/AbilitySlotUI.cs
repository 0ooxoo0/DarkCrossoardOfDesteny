using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AbilitySlotUI : MonoBehaviour, IPointerClickHandler
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

    public string BlockedReason => blockedReason;

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

        if (keyLabel != null)
            keyLabel.text = GetKeyLabel(hotkey, slotIndex);

        if (cooldownOverlay != null)
            cooldownOverlay.fillAmount = 0f;

        if (cooldownLabel != null)
            cooldownLabel.text = string.Empty;

        SetCultivationBadges(ability, null);
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
