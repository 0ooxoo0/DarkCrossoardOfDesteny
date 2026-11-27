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
    public string[] learnedTechniqueIds = new string[0];

    // v2: последствия срыва культивации и здоровье игрока.
    public int debuffKind;
    public float debuffRemaining;
    public int debuffSeverity;
    [Tooltip("Секунд чистой медитации до следующего снижения стадии.")]
    public float severityDecayAt;
    public int playerHealth;
    public int playerMaxHealth;

    // v2: навык алхимика.
    public int alchemyGrade;
    public int alchemySubRank;
    public int alchemyMastery;

    // v3: назначенные техники слотов 4–5 (индекс 0 = слот 4, индекс 1 = слот 5).
    public string[] techniqueSlotIds = new string[2];
}

/// <summary>
/// Кэш прогресса: сцена меняется — прогресс сохраняется. Дополнительно пишется в PlayerPrefs.
/// Формат v3 добавляет назначенные техники слотов 4–5; старые v1/v2 читаются как v3
/// с пустыми слотами. Ключ PrefsKey остаётся v2 — иначе старые сейвы потерялись бы.
/// </summary>
public static class CultivationStateCache
{
    private const string PrefsKey = "Cultivation.Save.v2";
    private const string LegacyPrefsKey = "Cultivation.Save.v1";

    private static CultivationSnapshot snapshot = new CultivationSnapshot();
    private static bool prefsLoaded;

    public static bool HasState => snapshot != null && snapshot.hasState;

    public static void Capture(CultivationSystem cultivation, DaoSystem dao, QiComponent qi,
        CultivationDebuffController debuffs = null, PlayerHealth health = null, AlchemySkill alchemy = null,
        CultivationAbilityBridge bridge = null)
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

            snapshot.learnedTechniqueIds = dao.GetLearnedTechniqueIds();
        }

        if (qi != null)
            snapshot.qi = qi.CurrentQi;

        if (debuffs != null)
        {
            ActiveCultivationDebuff primary = debuffs.Primary;
            snapshot.debuffKind = primary != null ? (int)primary.Vfx : (int)CultivationVfxKind.None;
            snapshot.debuffRemaining = primary != null ? primary.Remaining : 0f;
            snapshot.debuffSeverity = debuffs.Severity;
            snapshot.severityDecayAt = debuffs.TimeSinceLastInterruption;
        }

        if (health != null)
        {
            snapshot.playerHealth = health.Current;
            snapshot.playerMaxHealth = health.Max;
        }

        if (alchemy != null)
        {
            snapshot.alchemyGrade = (int)alchemy.Grade.Name;
            snapshot.alchemySubRank = (int)alchemy.Grade.SubRank;
            snapshot.alchemyMastery = alchemy.Mastery;
        }

        if (bridge != null)
        {
            string[] ids = bridge.GetAssignedTechniqueIds();
            if (ids != null && ids.Length > 0) snapshot.techniqueSlotIds = ids;
        }
    }

    public static bool TryRestore(CultivationSystem cultivation, DaoSystem dao, QiComponent qi,
        CultivationDebuffController debuffs = null, PlayerHealth health = null, AlchemySkill alchemy = null,
        CultivationAbilityBridge bridge = null)
    {
        if (!HasState || cultivation == null) return false;

        cultivation.RestoreState(snapshot.realmIndex, snapshot.stage, snapshot.experienceQi, snapshot.comprehension);

        if (dao != null)
            dao.RestoreState(snapshot.daoLevels, snapshot.daoPoints, snapshot.unlockedMask,
                snapshot.activeDaoIndex, snapshot.learnedTechniqueIds);

        // Слоты восстанавливаются после Dao.RestoreState: техники ищутся в путях Дао.
        if (bridge != null) bridge.RestoreAssignments(snapshot.techniqueSlotIds);

        if (qi != null)
            qi.SetCurrent(snapshot.qi);

        if (debuffs != null)
            debuffs.Restore(snapshot.debuffSeverity, snapshot.debuffRemaining, snapshot.severityDecayAt);

        if (health != null && snapshot.playerMaxHealth > 0 && snapshot.playerHealth > 0)
            health.RestorePercent((float)snapshot.playerHealth / snapshot.playerMaxHealth);

        if (alchemy != null)
            alchemy.Restore(new AlchemyGrade((AlchemyGradeName)snapshot.alchemyGrade,
                (AlchemySubRank)snapshot.alchemySubRank), snapshot.alchemyMastery);

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
        if (string.IsNullOrEmpty(json))
            json = PlayerPrefs.GetString(LegacyPrefsKey, string.Empty);

        if (string.IsNullOrEmpty(json)) return;

        try
        {
            // v1/v2/v3 — один класс: старый JSON просто оставляет новые поля нулями.
            var loaded = JsonUtility.FromJson<CultivationSnapshot>(json);
            if (loaded != null && loaded.hasState)
            {
                // Миграция: в старом сейве поля слотов нет — читаем как пустые слоты.
                if (loaded.techniqueSlotIds == null)
                    loaded.techniqueSlotIds = new string[2];
                snapshot = loaded;
            }
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
        PlayerPrefs.DeleteKey(LegacyPrefsKey);
        PlayerPrefs.Save();
    }
}
