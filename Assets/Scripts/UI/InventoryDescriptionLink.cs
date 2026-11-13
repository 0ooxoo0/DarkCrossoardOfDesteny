using TMPro;
using UnityEngine;

/// <summary>
/// Связывает рантайм-созданные слоты инвентаря с общей строкой описания.
/// `InventorySlotUI.DescriptText` заполняется только в сцене-примере (слоты там расставлены вручную),
/// а слоты, созданные `InventoryUI` в рантайме, остаются без ссылки. Скрипт аддитивный:
/// ничего не переписывает, просто проставляет публичное поле.
/// </summary>
[DisallowMultipleComponent]
public class InventoryDescriptionLink : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI descriptionLabel;

    private bool applied;

    private void LateUpdate()
    {
        if (applied || descriptionLabel == null) return;

        InventorySlotUI[] slots = GetComponentsInChildren<InventorySlotUI>(true);
        if (slots.Length == 0) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null) slots[i].DescriptText = descriptionLabel;
        }

        applied = true;
    }

    /// <summary>Для повторной привязки после пересоздания слотов.</summary>
    public void Rebind()
    {
        applied = false;
    }
}
