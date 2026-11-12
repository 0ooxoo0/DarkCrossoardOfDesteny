using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Дао-пути: уровни, активный путь, разблокировка и список доступных техник.
/// Применением техник занимается AbilitySystem (Промт B).
/// </summary>
[DisallowMultipleComponent]
public class DaoSystem : MonoBehaviour
{
    [Header("Пути")]
    [SerializeField] private List<DaoPath> daoPaths = new List<DaoPath>();
    [SerializeField] private List<DaoType> startingUnlocked = new List<DaoType> { DaoType.Sword, DaoType.Fist };

    [Header("Развитие")]
    [SerializeField, Min(1)] private int pointsPerLevel = 10;
    [SerializeField, Min(0)] private int startingLevel = 1;

    [Header("Смена активного Дао")]
    [Tooltip("false = менять Дао можно только у алтаря (SetNearAltar(true) вызывает Промт B).")]
    [SerializeField] private bool canSwitchAnywhere = false;

    [Header("Ссылки")]
    [SerializeField] private CultivationSystem cultivation;

    [SerializeField] private bool hasActiveDao;
    [SerializeField] private DaoType activeDaoType = DaoType.Sword;

    private readonly Dictionary<DaoType, int> levels = new Dictionary<DaoType, int>();
    private readonly Dictionary<DaoType, int> points = new Dictionary<DaoType, int>();
    private readonly HashSet<DaoType> unlocked = new HashSet<DaoType>();
    private readonly Dictionary<string, TechniqueData> learnedTechniques = new Dictionary<string, TechniqueData>();
    private bool nearAltar;

    public event Action<DaoType, int> OnDaoLevelChanged;
    public event Action<DaoType> OnActiveDaoChanged;
    public event Action<DaoType> OnDaoUnlocked;
    /// <summary>(dao, points, pointsPerLevel)</summary>
    public event Action<DaoType, int, int> OnDaoPointsChanged;
    /// <summary>Свиток или иной источник добавил технику в изученные.</summary>
    public event Action<TechniqueData> OnTechniqueLearned;

    public DaoType? ActiveDao => hasActiveDao ? activeDaoType : (DaoType?)null;
    public bool CanSwitchAnywhere { get => canSwitchAnywhere; set => canSwitchAnywhere = value; }
    public bool IsNearAltar => nearAltar;
    public bool CanSwitchDao => canSwitchAnywhere || nearAltar;
    public int PointsPerLevel => Mathf.Max(1, pointsPerLevel);
    public IReadOnlyList<DaoPath> DaoPaths => daoPaths;

    private void Awake()
    {
        EnsureDictionaries();
    }

    private void EnsureDictionaries()
    {
        foreach (var type in DaoTypeInfo.All)
        {
            if (!levels.ContainsKey(type)) levels[type] = 0;
            if (!points.ContainsKey(type)) points[type] = 0;
        }
    }

    public void Initialize(List<DaoPath> paths, CultivationSystem cultivationSystem, bool switchAnywhere,
        List<DaoType> unlockedAtStart, int levelCost, int startLevel)
    {
        daoPaths = paths ?? new List<DaoPath>();
        cultivation = cultivationSystem;
        canSwitchAnywhere = switchAnywhere;
        pointsPerLevel = Mathf.Max(1, levelCost);
        startingLevel = Mathf.Max(0, startLevel);

        levels.Clear();
        points.Clear();
        unlocked.Clear();
        learnedTechniques.Clear();
        EnsureDictionaries();

        if (unlockedAtStart != null)
        {
            foreach (var type in unlockedAtStart)
                Unlock(type, false);
        }

        if (!hasActiveDao)
        {
            foreach (var type in DaoTypeInfo.All)
            {
                if (unlocked.Contains(type))
                {
                    SetActiveDaoForced(type);
                    break;
                }
            }
        }
    }

    public CultivationSystem Cultivation => cultivation;

    public void SetCultivation(CultivationSystem system) => cultivation = system;

    public DaoPath GetDaoPath(DaoType type)
    {
        if (daoPaths == null) return null;
        for (int i = 0; i < daoPaths.Count; i++)
        {
            if (daoPaths[i] != null && daoPaths[i].DaoType == type)
                return daoPaths[i];
        }
        return null;
    }

    public int GetMaxLevel(DaoType type)
    {
        var path = GetDaoPath(type);
        return path != null ? path.MaxLevel : 10;
    }

    public int GetLevel(DaoType type)
    {
        return levels.TryGetValue(type, out int level) ? level : 0;
    }

    public int GetPoints(DaoType type)
    {
        return points.TryGetValue(type, out int value) ? value : 0;
    }

    public int GetPointsForNextLevel()
    {
        return PointsPerLevel;
    }

    public int GetPointsIntoLevel(DaoType type)
    {
        return GetPoints(type) % PointsPerLevel;
    }

    public bool IsDaoUnlocked(DaoType type) => unlocked.Contains(type);

    public bool IsMaxLevel(DaoType type) => GetLevel(type) >= GetMaxLevel(type);

    public void UnlockDao(DaoType type)
    {
        Unlock(type, true);
    }

    /// <summary>Добавить технику в изученные (свиток). Уровень/реалм при этом могут быть ещё не достигнуты.</summary>
    public bool AddLearnedTechnique(TechniqueData technique)
    {
        if (technique == null || string.IsNullOrEmpty(technique.TechniqueId)) return false;

        if (!IsDaoUnlocked(technique.DaoType))
        {
            Debug.LogWarning("[DaoSystem] Нельзя изучить технику закрытого Дао: " + technique.DaoType);
            return false;
        }

        if (learnedTechniques.ContainsKey(technique.TechniqueId))
        {
            learnedTechniques[technique.TechniqueId] = technique;
            return false;
        }

        learnedTechniques[technique.TechniqueId] = technique;
        OnTechniqueLearned?.Invoke(technique);
        return true;
    }

    public bool HasLearnedTechnique(TechniqueData technique)
    {
        return technique != null && !string.IsNullOrEmpty(technique.TechniqueId) &&
               learnedTechniques.ContainsKey(technique.TechniqueId);
    }

    public bool HasLearnedTechnique(string techniqueId)
    {
        return !string.IsNullOrEmpty(techniqueId) && learnedTechniques.ContainsKey(techniqueId);
    }

    public List<TechniqueData> GetLearnedTechniques()
    {
        return new List<TechniqueData>(learnedTechniques.Values);
    }

    public string[] GetLearnedTechniqueIds()
    {
        return new List<string>(learnedTechniques.Keys).ToArray();
    }

    public TechniqueData GetTechniqueById(string techniqueId)
    {
        if (string.IsNullOrEmpty(techniqueId)) return null;
        if (learnedTechniques.TryGetValue(techniqueId, out TechniqueData learned) && learned != null)
            return learned;

        if (daoPaths == null) return null;
        for (int i = 0; i < daoPaths.Count; i++)
        {
            var path = daoPaths[i];
            if (path == null) continue;
            var techniques = path.Techniques;
            for (int j = 0; j < techniques.Count; j++)
            {
                if (techniques[j] != null && techniques[j].TechniqueId == techniqueId)
                    return techniques[j];
            }
        }
        return null;
    }

    private void Unlock(DaoType type, bool notify)
    {
        if (!unlocked.Add(type)) return;

        if (!levels.ContainsKey(type)) levels[type] = 0;
        if (!points.ContainsKey(type)) points[type] = 0;

        int level = Mathf.Clamp(startingLevel, 0, GetMaxLevel(type));
        if (level > levels[type])
        {
            levels[type] = level;
            OnDaoLevelChanged?.Invoke(type, level);
        }

        if (notify) OnDaoUnlocked?.Invoke(type);

        if (!hasActiveDao) SetActiveDaoForced(type);
    }

    /// <summary>Добавить очки Дао. Уровень пересчитывается автоматически (1 уровень = pointsPerLevel очков).</summary>
    public void AddPoints(DaoType type, int amount)
    {
        if (amount <= 0) return;
        if (!IsDaoUnlocked(type))
        {
            Debug.LogWarning("[DaoSystem] Путь " + type + " не разблокирован — очки не начислены.");
            return;
        }

        EnsureDictionaries();
        int maxLevel = GetMaxLevel(type);
        int cap = maxLevel * PointsPerLevel;
        int newPoints = Mathf.Min(cap, GetPoints(type) + amount);
        points[type] = newPoints;

        int newLevel = Mathf.Clamp(startingLevel + newPoints / PointsPerLevel, 0, maxLevel);
        if (newLevel != GetLevel(type))
        {
            levels[type] = newLevel;
            OnDaoLevelChanged?.Invoke(type, newLevel);
        }

        OnDaoPointsChanged?.Invoke(type, newPoints, PointsPerLevel);
    }

    /// <summary>Смена активного Дао. Возвращает false, если путь закрыт или игрок не у алтаря.</summary>
    public bool SetActiveDao(DaoType type)
    {
        if (!IsDaoUnlocked(type))
        {
            Debug.LogWarning("[DaoSystem] Путь " + type + " не разблокирован.");
            return false;
        }

        if (!canSwitchAnywhere && !nearAltar)
        {
            Debug.Log("[DaoSystem] Смена Дао доступна только у алтаря.");
            return false;
        }

        if (hasActiveDao && activeDaoType == type) return true;

        activeDaoType = type;
        hasActiveDao = true;
        OnActiveDaoChanged?.Invoke(type);
        return true;
    }

    /// <summary>Принудительная смена (bootstrap, загрузка сохранения, алтарь).</summary>
    public void SetActiveDaoForced(DaoType type)
    {
        if (!IsDaoUnlocked(type)) Unlock(type, true);

        activeDaoType = type;
        hasActiveDao = true;
        OnActiveDaoChanged?.Invoke(type);
    }

    public int GetUnlockedMask()
    {
        int mask = 0;
        for (int i = 0; i < DaoTypeInfo.All.Length; i++)
        {
            if (unlocked.Contains(DaoTypeInfo.All[i])) mask |= 1 << i;
        }
        return mask;
    }

    public int GetActiveIndex()
    {
        if (!hasActiveDao) return -1;
        return Array.IndexOf(DaoTypeInfo.All, activeDaoType);
    }

    /// <summary>Восстановление уровней/очков/разблокировки из сохранения.</summary>
    public void RestoreState(int[] levelsIn, int[] pointsIn, int unlockedMask, int activeIndex)
    {
        RestoreState(levelsIn, pointsIn, unlockedMask, activeIndex, null);
    }

    public void RestoreState(int[] levelsIn, int[] pointsIn, int unlockedMask, int activeIndex, string[] learnedIds)
    {
        EnsureDictionaries();

        for (int i = 0; i < DaoTypeInfo.All.Length; i++)
        {
            DaoType type = DaoTypeInfo.All[i];
            bool isUnlocked = (unlockedMask & (1 << i)) != 0;

            if (isUnlocked)
            {
                unlocked.Add(type);
                int level = levelsIn != null && i < levelsIn.Length ? levelsIn[i] : startingLevel;
                int value = pointsIn != null && i < pointsIn.Length ? pointsIn[i] : 0;
                levels[type] = Mathf.Clamp(level, 0, GetMaxLevel(type));
                points[type] = Mathf.Max(0, value);
            }
            else
            {
                unlocked.Remove(type);
                levels[type] = 0;
                points[type] = 0;
            }
        }

        learnedTechniques.Clear();
        if (learnedIds != null)
        {
            for (int i = 0; i < learnedIds.Length; i++)
            {
                string id = learnedIds[i];
                if (string.IsNullOrEmpty(id)) continue;
                TechniqueData technique = GetTechniqueById(id);
                if (technique != null)
                    learnedTechniques[id] = technique;
            }
        }

        if (activeIndex >= 0 && activeIndex < DaoTypeInfo.All.Length)
        {
            DaoType active = DaoTypeInfo.All[activeIndex];
            if (unlocked.Contains(active))
            {
                activeDaoType = active;
                hasActiveDao = true;
                OnActiveDaoChanged?.Invoke(active);
            }
        }
    }

    /// <summary>Алтарь вызывает true на входе и false на выходе (Промт B).</summary>
    public void SetNearAltar(bool value)
    {
        nearAltar = value;
    }

    /// <summary>Техники активного Дао, доступные по уровню Дао и текущему реалму.</summary>
    public List<TechniqueData> GetAvailableTechniques()
    {
        var result = new List<TechniqueData>();
        if (!hasActiveDao) return result;

        int realmIndex = cultivation != null ? cultivation.CurrentRealmIndex : 0;
        var path = GetDaoPath(activeDaoType);
        int level = GetLevel(activeDaoType);

        if (path == null) return result;

        var techniques = path.Techniques;
        for (int i = 0; i < techniques.Count; i++)
        {
            var technique = techniques[i];
            if (technique == null) continue;
            if (!IsTechniqueAvailable(technique, level, realmIndex)) continue;
            result.Add(technique);
        }

        // Свиток мог открыть технику раньше уровня/реалма — она всё равно доступна.
        foreach (var learned in learnedTechniques.Values)
        {
            if (learned == null || learned.DaoType != activeDaoType) continue;
            if (!result.Contains(learned)) result.Add(learned);
        }
        return result;
    }

    public bool IsTechniqueAvailable(TechniqueData technique)
    {
        if (technique == null) return false;
        if (HasLearnedTechnique(technique)) return true;
        int realmIndex = cultivation != null ? cultivation.CurrentRealmIndex : 0;
        return IsTechniqueAvailable(technique, GetLevel(technique.DaoType), realmIndex);
    }

    public bool IsTechniqueAvailable(TechniqueData technique, int level, int realmIndex)
    {
        if (technique == null) return false;
        return technique.RequiredDaoLevel <= level && technique.RequiredRealmIndex <= realmIndex;
    }

    /// <summary>Все техники всех путей (для витрины TechniqueListUI).</summary>
    public List<TechniqueData> GetAllTechniques()
    {
        var result = new List<TechniqueData>();
        if (daoPaths == null) return result;

        for (int i = 0; i < daoPaths.Count; i++)
        {
            var path = daoPaths[i];
            if (path == null) continue;
            var techniques = path.Techniques;
            for (int j = 0; j < techniques.Count; j++)
            {
                if (techniques[j] != null && !result.Contains(techniques[j]))
                    result.Add(techniques[j]);
            }
        }

        foreach (var learned in learnedTechniques.Values)
        {
            if (learned != null && !result.Contains(learned)) result.Add(learned);
        }
        return result;
    }

    public List<TechniqueData> GetTechniquesForDao(DaoType type)
    {
        var result = new List<TechniqueData>();
        var path = GetDaoPath(type);
        if (path == null) return result;

        var techniques = path.Techniques;
        for (int i = 0; i < techniques.Count; i++)
        {
            if (techniques[i] != null) result.Add(techniques[i]);
        }

        foreach (var learned in learnedTechniques.Values)
        {
            if (learned != null && learned.DaoType == type && !result.Contains(learned)) result.Add(learned);
        }
        return result;
    }
}
