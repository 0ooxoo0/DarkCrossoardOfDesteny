using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Скиллы: доступность техник, активация пассивов по прогрессу, назначение в слоты
/// и миграция сохранения v2 → v3. Проверяет только данные и сервисы, без сцены.
/// </summary>
public class SkillSystemTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        }
        created.Clear();
    }

    private T Track<T>(T value) where T : Object
    {
        created.Add(value);
        return value;
    }

    private TechniqueData MakeTechnique(string id, DaoType dao, TechniqueKind kind, int requiredDaoLevel,
        int requiredRealmIndex, float passiveValue = 0f,
        PassiveEffectKind effect = PassiveEffectKind.MaxHealthPercent)
    {
        var technique = Track(ScriptableObject.CreateInstance<TechniqueData>());
        technique.Setup(id, id, string.Empty, string.Empty, dao, kind, 1, requiredDaoLevel, requiredRealmIndex,
            10, 5f, 20, null, null, null);
        technique.SetPassive(effect, passiveValue);
        return technique;
    }

    private DaoSystem MakeDao(params TechniqueData[] techniques)
    {
        var host = Track(new GameObject("DaoHost"));
        var dao = host.AddComponent<DaoSystem>();

        var path = Track(ScriptableObject.CreateInstance<DaoPath>());
        path.Setup(DaoType.Sword, "Меч", string.Empty, string.Empty, Color.white, 4, null,
            new List<TechniqueData>(techniques));

        var other = Track(ScriptableObject.CreateInstance<DaoPath>());
        other.Setup(DaoType.Fire, "Огонь", string.Empty, string.Empty, Color.white, 4, null,
            new List<TechniqueData>());

        var cultivationHost = Track(new GameObject("CultivationHost"));
        var cultivation = cultivationHost.AddComponent<CultivationSystem>();
        cultivation.Initialize(new List<CultivationRealm> { MakeRealm() }, 0, 1, 100, 1.45f, 0.35f);

        dao.Initialize(new List<DaoPath> { path, other }, cultivation, true,
            new List<DaoType> { DaoType.Sword, DaoType.Fire }, 10, 1);
        return dao;
    }

    private CultivationRealm MakeRealm()
    {
        var realm = Track(ScriptableObject.CreateInstance<CultivationRealm>());
        realm.Setup("Смертное тело", string.Empty, string.Empty, 0, 3, 0f, 0f, 0f, 0, 0.35f,
            new List<string>());
        return realm;
    }

    [Test]
    public void DaoSystem_TechniqueAvailable_RespectsLevelAndRealm()
    {
        TechniqueData early = MakeTechnique("early", DaoType.Sword, TechniqueKind.Active, 1, 0);
        TechniqueData late = MakeTechnique("late", DaoType.Sword, TechniqueKind.Active, 3, 1);
        DaoSystem dao = MakeDao(early, late);

        Assert.That(dao.IsTechniqueAvailable(early), Is.True, "Техника 1 уровня доступна сразу.");
        Assert.That(dao.IsTechniqueAvailable(late), Is.False, "Техника 3 уровня требует уровня и реалма.");

        dao.AddPoints(DaoType.Sword, 40);
        Assert.That(dao.GetLevel(DaoType.Sword), Is.EqualTo(4), "10 очков = 1 уровень, потолок пути 4.");
        Assert.That(dao.IsTechniqueAvailable(late, 4, 1), Is.True,
            "Требования уровня и реалма выполнены — техника доступна.");
    }

    [Test]
    public void DaoSystem_LearnedTechnique_BypassesRequirements()
    {
        TechniqueData late = MakeTechnique("late", DaoType.Sword, TechniqueKind.Active, 9, 3);
        DaoSystem dao = MakeDao(late);

        Assert.That(dao.IsTechniqueAvailable(late), Is.False, "Без свитка требования не выполнены.");
        Assert.That(dao.AddLearnedTechnique(late), Is.True, "Свиток учит технику.");
        Assert.That(dao.IsTechniqueAvailable(late), Is.True, "Выучка обходит требования.");
    }

    [Test]
    public void DaoSystem_RestoreState_KeepsLearnedTechniques()
    {
        TechniqueData technique = MakeTechnique("sword_1", DaoType.Sword, TechniqueKind.Active, 1, 0);
        DaoSystem dao = MakeDao(technique);
        dao.AddLearnedTechnique(technique);

        dao.RestoreState(new[] { 2, 0 }, new[] { 20, 0 }, 1, 0, new[] { "sword_1" });

        Assert.That(dao.GetLevel(DaoType.Sword), Is.EqualTo(2), "Уровень восстановлен.");
        Assert.That(dao.HasLearnedTechnique("sword_1"), Is.True, "Выученные техники восстановлены.");
        Assert.That(dao.IsDaoUnlocked(DaoType.Sword), Is.True, "Маска открытых Дао восстановлена.");
    }

    [Test]
    public void PassiveSkillService_ActivatesByProgress_WithoutScroll()
    {
        TechniqueData passive = MakeTechnique("sword_passive", DaoType.Sword, TechniqueKind.Passive, 2, 0,
            0.1f, PassiveEffectKind.MeleeDamagePercent);
        DaoSystem dao = MakeDao(passive);

        var serviceHost = Track(new GameObject("PassiveHost"));
        var service = serviceHost.AddComponent<PassiveSkillService>();
        service.Initialize(dao, dao.Cultivation, null, null, null, null);

        Assert.That(service.IsPassiveActive(passive), Is.False, "Уровень 1 < требуемого 2 — пассив молчит.");
        Assert.That(service.GetInactiveReason(passive), Does.Contain("Нужен Дао 2"));

        dao.AddPoints(DaoType.Sword, 10);
        Assert.That(service.IsPassiveActive(passive), Is.True, "Свиток не нужен: хватает прогресса.");
        Assert.That(service.GetMultiplier(PassiveEffectKind.MeleeDamagePercent), Is.EqualTo(1.1f).Within(0.0001f));
    }

    [Test]
    public void PassiveSkillService_LockedDao_StaysInactive()
    {
        TechniqueData passive = MakeTechnique("fire_passive", DaoType.Fire, TechniqueKind.Passive, 1, 0,
            0.1f, PassiveEffectKind.SpellPowerPercent);
        DaoSystem dao = MakeDao(passive);
        // Маска 2 = открыт только Гром: Огонь остаётся закрытым.
        dao.RestoreState(new[] { 5, 0 }, new[] { 50, 0 }, 2, 0, new string[0]);

        var serviceHost = Track(new GameObject("PassiveHost"));
        var service = serviceHost.AddComponent<PassiveSkillService>();
        service.Initialize(dao, dao.Cultivation, null, null, null, null);

        Assert.That(dao.IsDaoUnlocked(DaoType.Fire), Is.False, "Огонь закрыт.");
        Assert.That(service.IsPassiveActive(passive), Is.False, "Пассив закрытого Дао не активируется.");
        Assert.That(service.GetInactiveReason(passive), Does.Contain("Огня"));
    }

    [Test]
    public void CultivationAbilityBridge_AssignReplaceUnassign_Restore()
    {
        TechniqueData first = MakeTechnique("sword_1", DaoType.Sword, TechniqueKind.Active, 1, 0);
        TechniqueData second = MakeTechnique("sword_2", DaoType.Sword, TechniqueKind.Active, 1, 0);
        TechniqueData passive = MakeTechnique("sword_passive", DaoType.Sword, TechniqueKind.Passive, 1, 0, 0.1f);
        DaoSystem dao = MakeDao(first, second, passive);

        var host = Track(new GameObject("BridgeHost"));
        var system = host.AddComponent<AbilitySystem>();
        var bridge = host.AddComponent<CultivationAbilityBridge>();
        bridge.Initialize(dao, null, system);

        Assert.That(bridge.AssignTechnique(first, 3), Is.True, "Первая техника идёт в слот 4.");
        Assert.That(bridge.GetAssignedTechnique(3), Is.EqualTo(first));
        Assert.That(bridge.AssignTechnique(first, 3), Is.False, "Повторное назначение в тот же слот — no-op.");
        Assert.That(bridge.AssignTechnique(passive, 4), Is.False, "Пассив в слот не кладётся.");

        Assert.That(bridge.AssignTechnique(second, 4), Is.True, "Вторая техника идёт в слот 5.");
        Assert.That(bridge.AssignTechnique(first, 4), Is.True, "Замена слота по явному запросу.");
        Assert.That(bridge.GetAssignedTechnique(4), Is.EqualTo(first));
        Assert.That(bridge.GetAssignedTechnique(3), Is.Null, "Техника не остаётся в двух слотах.");

        Assert.That(bridge.UnassignTechnique(4), Is.True, "Снятие освобождает слот.");
        Assert.That(bridge.RegisteredCount, Is.EqualTo(0));

        Assert.That(bridge.RestoreAssignments(new[] { "sword_2", "" }), Is.EqualTo(1), "Сохранение вернуло один слот.");
        Assert.That(bridge.GetAssignedTechnique(3), Is.EqualTo(second));
    }

    [Test]
    public void CultivationSnapshot_V2Json_ReadsAsV3WithEmptySlots()
    {
        const string legacyJson = "{\"hasState\":true,\"realmIndex\":1,\"stage\":2,\"experienceQi\":40," +
                                  "\"comprehension\":3,\"qi\":80.0,\"activeDaoIndex\":0,\"unlockedMask\":1," +
                                  "\"daoLevels\":[2,0,0,0,0],\"daoPoints\":[20,0,0,0,0]," +
                                  "\"learnedTechniqueIds\":[\"sword_1\"],\"playerHealth\":90,\"playerMaxHealth\":100}";

        var snapshot = JsonUtility.FromJson<CultivationSnapshot>(legacyJson);

        Assert.That(snapshot, Is.Not.Null, "Старый JSON читается без исключения.");
        Assert.That(snapshot.techniqueSlotIds, Is.Not.Null.And.Length.EqualTo(2),
            "Поле слотов есть, но оба слота пустые: миграция без исключений.");
        Assert.That(string.IsNullOrEmpty(snapshot.techniqueSlotIds[0]) &&
                    string.IsNullOrEmpty(snapshot.techniqueSlotIds[1]), Is.True,
            "В v2 назначений не было — слоты пусты.");
        Assert.That(snapshot.experienceQi, Is.EqualTo(40), "Старые поля сохранены.");
    }
}
