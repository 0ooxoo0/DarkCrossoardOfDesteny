using System;
using UnityEngine;

/// <summary>Снимок прогресса культивации (переживает смену сцен и перезапуск).</summary>
[Serializable]
public class CultivationSnapshot
{
    public bool hasState;
    public int realmIndex;
    public int stage = 1;
    public int experienceQi;
    public int comprehension;
    public float qi;
    public int activeDaoIndex = -1;
    public int unlockedMask;
    public int[] daoLevels = new int[5];
    public int[] daoPoints = new int[5];
}

/// <summary>
/// Кэш прогресса: сцена меняется — прогресс сохраняется. Дополнительно пишется в PlayerPrefs.
/// </summary>
public static class CultivationStateCache
{
    private const string PrefsKey = "Cultivation.Save.v1";

    private static CultivationSnapshot snapshot = new CultivationSnapshot();
    private static bool prefsLoaded;

    public static bool HasState => snapshot != null && snapshot.hasState;

    public static void Capture(CultivationSystem cultivation, DaoSystem dao, QiComponent qi)
    {
        if (cultivation == null) return;

        if (snapshot == null) snapshot = new CultivationSnapshot();
        snapshot.hasState = true;
        snapshot.realmIndex = cultivation.CurrentRealmIndex;
        snapshot.stage = cultivation.CurrentStage;
        snapshot.experienceQi = cultivation.ExperienceQi;
        snapshot.comprehension = cultivation.Comprehension;

        if (dao != null)
        {
            snapshot.activeDaoIndex = dao.GetActiveIndex();
            snapshot.unlockedMask = dao.GetUnlockedMask();

            if (snapshot.daoLevels == null || snapshot.daoLevels.Length != DaoTypeInfo.All.Length)
                snapshot.daoLevels = new int[DaoTypeInfo.All.Length];
            if (snapshot.daoPoints == null || snapshot.daoPoints.Length != DaoTypeInfo.All.Length)
                snapshot.daoPoints = new int[DaoTypeInfo.All.Length];

            for (int i = 0; i < DaoTypeInfo.All.Length; i++)
            {
                snapshot.daoLevels[i] = dao.GetLevel(DaoTypeInfo.All[i]);
                snapshot.daoPoints[i] = dao.GetPoints(DaoTypeInfo.All[i]);
            }
        }

        if (qi != null)
            snapshot.qi = qi.CurrentQi;
    }

    public static bool TryRestore(CultivationSystem cultivation, DaoSystem dao, QiComponent qi)
    {
        if (!HasState || cultivation == null) return false;

        cultivation.RestoreState(snapshot.realmIndex, snapshot.stage, snapshot.experienceQi, snapshot.comprehension);

        if (dao != null)
            dao.RestoreState(snapshot.daoLevels, snapshot.daoPoints, snapshot.unlockedMask, snapshot.activeDaoIndex);

        if (qi != null)
            qi.SetCurrent(snapshot.qi);

        return true;
    }

    public static void SaveToPrefs()
    {
        if (snapshot == null || !snapshot.hasState) return;
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(snapshot));
        PlayerPrefs.Save();
    }

    public static void LoadFromPrefs(bool force = false)
    {
        if (prefsLoaded && !force) return;
        prefsLoaded = true;

        string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var loaded = JsonUtility.FromJson<CultivationSnapshot>(json);
            if (loaded != null && loaded.hasState)
                snapshot = loaded;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Cultivation] Не удалось прочитать сохранение: " + exception.Message);
        }
    }

    public static void Reset(bool deletePrefs = true)
    {
        snapshot = new CultivationSnapshot();
        prefsLoaded = true;

        if (!deletePrefs) return;
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }
}
