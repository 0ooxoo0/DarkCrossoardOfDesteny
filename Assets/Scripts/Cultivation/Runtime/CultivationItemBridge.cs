using UnityEngine;

/// <summary>
/// Применение расходников культивации из инвентаря: пилюли, свитки техник и семена Дао.
/// Инвентарь не знает о Ци/Дао — он только передаёт предмет в этот мост.
/// </summary>
[DisallowMultipleComponent]
public class CultivationItemBridge : MonoBehaviour
{
    [SerializeField] private QiComponent qi;
    [SerializeField] private CultivationSystem cultivation;
    [SerializeField] private DaoSystem dao;
    [SerializeField] private Inventory inventory;

    public QiComponent Qi => qi;
    public CultivationSystem Cultivation => cultivation;
    public DaoSystem Dao => dao;
    public Inventory Inventory => inventory;

    public void Initialize(QiComponent qiComponent, CultivationSystem cultivationSystem, DaoSystem daoSystem,
        Inventory playerInventory)
    {
        qi = qiComponent;
        cultivation = cultivationSystem;
        dao = daoSystem;
        inventory = playerInventory;
    }

    private void Awake()
    {
        EnsureReferences();
    }

    /// <summary>Использовать предмет из конкретного слота. Возвращает true, если предмет был потрачен.</summary>
    public bool TryUse(ItemData item, Inventory sourceInventory, int slotIndex)
    {
        if (item == null) return false;

        EnsureReferences();
        Inventory targetInventory = sourceInventory != null ? sourceInventory : inventory;
        if (targetInventory == null) return false;

        InventorySlot sourceSlot = targetInventory.GetSlot(slotIndex);
        if (sourceSlot == null || sourceSlot.itemData != item || sourceSlot.quantity <= 0)
            return false;

        bool consume = false;

        switch (item.type)
        {
            case ItemType.Pill:
                consume = ApplyPill(item);
                break;

            case ItemType.Scroll:
                consume = ApplyScroll(item);
                break;

            case ItemType.DaoSeed:
                consume = ApplyDaoSeed(item);
                break;

            case ItemType.BreakthroughMaterial:
                Debug.Log("[Cultivation] Материал прорыва не используется вручную.");
                return false;

            default:
                return false;
        }

        if (!consume) return false;

        targetInventory.RemoveItem(slotIndex, 1);
        return true;
    }

    private bool ApplyPill(ItemData item)
    {
        int qiAmount = Mathf.Max(0, item.qiRestore);
        int experience = Mathf.Max(0, item.experienceQiGain);
        int comprehension = Mathf.Max(0, item.comprehensionGain);

        if (qiAmount == 0 && experience == 0 && comprehension == 0)
        {
            Debug.LogWarning("[Cultivation] Пилюля " + item.itemName + " не имеет эффекта.");
            return false;
        }

        bool applied = false;

        if (qiAmount > 0 && qi != null)
        {
            qi.Restore(qiAmount);
            applied = true;
        }

        if (experience > 0 && cultivation != null)
        {
            cultivation.AddExperienceQi(experience);
            applied = true;
        }

        if (comprehension > 0 && cultivation != null)
        {
            cultivation.AddComprehension(comprehension);
            applied = true;
        }

        if (!applied)
        {
            Debug.LogWarning("[Cultivation] Пилюля " + item.itemName + " не применилась: нет нужных систем.");
            return false;
        }

        return true;
    }

    private bool ApplyScroll(ItemData item)
    {
        if (item.technique == null)
        {
            Debug.LogWarning("[Cultivation] У свитка " + item.itemName + " не назначена техника.");
            return false;
        }

        if (dao == null)
        {
            Debug.LogWarning("[Cultivation] Нет DaoSystem — свиток нельзя изучить.");
            return false;
        }

        if (!dao.AddLearnedTechnique(item.technique))
        {
            Debug.Log("[Cultivation] Техника уже изучена или её Дао закрыт: " + item.technique.DisplayName);
            return false;
        }

        Debug.Log("[Cultivation] Изучена техника: " + item.technique.DisplayName);
        return true;
    }

    private bool ApplyDaoSeed(ItemData item)
    {
        if (dao == null)
        {
            Debug.LogWarning("[Cultivation] Нет DaoSystem — семя Дао нельзя использовать.");
            return false;
        }

        if (dao.IsDaoUnlocked(item.unlocksDao))
        {
            Debug.Log("[Cultivation] Дао уже открыт: " + item.unlocksDao);
            return false;
        }

        dao.UnlockDao(item.unlocksDao);
        Debug.Log("[Cultivation] Открыт Дао: " + DaoTypeInfo.GetDisplayName(item.unlocksDao));
        return true;
    }

    private void EnsureReferences()
    {
        if (qi == null) qi = GetComponent<QiComponent>();
        if (cultivation == null) cultivation = GetComponent<CultivationSystem>();
        if (dao == null) dao = GetComponent<DaoSystem>();
        if (inventory == null) inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
    }
}
