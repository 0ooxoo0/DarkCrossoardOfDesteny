using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Клик правой кнопкой мыши по кнопке привязки очищает слот.
/// Левый клик обрабатывает обычный UI Button (назначение клавиши).
/// </summary>
[DisallowMultipleComponent]
public sealed class BindingSlotButton : MonoBehaviour, IPointerClickHandler
{
    private ControlsSettingsPanel panel;
    private string actionId;
    private InputBindingSlot slot;

    public void Initialize(ControlsSettingsPanel owner, string action, InputBindingSlot bindingSlot)
    {
        panel = owner;
        actionId = action;
        slot = bindingSlot;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Right) return;
        if (panel != null) panel.RequestUnbind(actionId, slot);
    }
}
