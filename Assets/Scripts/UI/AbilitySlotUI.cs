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

    private Color iconBaseColor = Color.white;
    private int slotIndex;

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
