using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Эталонный контент культивации: 4 реалма × 3 подстадии, 5 путей Дао, 15 техник.
/// Используется и setup-скриптом (ассеты), и рантайм-конфигом (играть без настройки).
/// </summary>
public static class CultivationDefaults
{
    public sealed class RealmBlueprint
    {
        public string Name;
        public string Chinese;
        public string Description;
        public float QiBonus;
        public float HpBonus;
        public float RegenBonus;
        public int Comprehension;
        public float Chance;
        public string[] Materials;
    }

    public sealed class TechniqueBlueprint
    {
        public string Id;
        public string Name;
        public string Chinese;
        public DaoType Dao;
        public TechniqueKind Kind;
        public int Rank;
        public int RequiredDaoLevel;
        public int RequiredRealmIndex;
        public int QiCost;
        public float Cooldown;
        public int Power;
    }

    public static readonly RealmBlueprint[] Realms =
    {
        new RealmBlueprint
        {
            Name = "Смертное тело", Chinese = "凡躯",
            Description = "Обычная плоть. Начало пути — лишь дыхание и дисциплина.",
            QiBonus = 10f, HpBonus = 5f, RegenBonus = 0.5f, Comprehension = 0, Chance = 0.55f,
            Materials = new[] { "herb.mortal" }
        },
        new RealmBlueprint
        {
            Name = "Очищение Ци", Chinese = "练气",
            Description = "Ци течёт по каналам. Тело учится удерживать силу.",
            QiBonus = 25f, HpBonus = 15f, RegenBonus = 1f, Comprehension = 3, Chance = 0.45f,
            Materials = new[] { "herb.spirit", "ore.jade" }
        },
        new RealmBlueprint
        {
            Name = "Фундамент", Chinese = "筑基",
            Description = "Заложен фундамент Дао. Каждая ступень — новый слой печати.",
            QiBonus = 60f, HpBonus = 40f, RegenBonus = 2.5f, Comprehension = 8, Chance = 0.35f,
            Materials = new[] { "core.beast", "ore.jade" }
        },
        new RealmBlueprint
        {
            Name = "Золотое ядро", Chinese = "金丹",
            Description = "Ядро света в центре даньтяня. Рождение бессмертного.",
            QiBonus = 150f, HpBonus = 100f, RegenBonus = 6f, Comprehension = 20, Chance = 0.25f,
            Materials = new[] { "essence.golden", "core.beast" }
        }
    };

    public static readonly TechniqueBlueprint[] Techniques =
    {
        // Sword
        new TechniqueBlueprint { Id = "sword_1", Name = "Удар клинка", Chinese = "斩击", Dao = DaoType.Sword, Kind = TechniqueKind.Active, Rank = 1, RequiredDaoLevel = 1, RequiredRealmIndex = 0, QiCost = 15, Cooldown = 5f, Power = 35 },
        new TechniqueBlueprint { Id = "sword_2", Name = "Стойка клинка", Chinese = "剑势", Dao = DaoType.Sword, Kind = TechniqueKind.Block, Rank = 2, RequiredDaoLevel = 2, RequiredRealmIndex = 0, QiCost = 20, Cooldown = 10f, Power = 0 },
        new TechniqueBlueprint { Id = "sword_3", Name = "Дождь тысячи клинков", Chinese = "万剑雨", Dao = DaoType.Sword, Kind = TechniqueKind.Aura, Rank = 3, RequiredDaoLevel = 4, RequiredRealmIndex = 1, QiCost = 60, Cooldown = 40f, Power = 90 },

        // Fist
        new TechniqueBlueprint { Id = "fist_1", Name = "Кулак горы", Chinese = "山拳", Dao = DaoType.Fist, Kind = TechniqueKind.Active, Rank = 1, RequiredDaoLevel = 1, RequiredRealmIndex = 0, QiCost = 12, Cooldown = 4f, Power = 30 },
        new TechniqueBlueprint { Id = "fist_2", Name = "Железная кожа", Chinese = "铁肤", Dao = DaoType.Fist, Kind = TechniqueKind.Passive, Rank = 2, RequiredDaoLevel = 2, RequiredRealmIndex = 0, QiCost = 0, Cooldown = 0f, Power = 0 },
        new TechniqueBlueprint { Id = "fist_3", Name = "Ярость дракона", Chinese = "龙怒", Dao = DaoType.Fist, Kind = TechniqueKind.Aura, Rank = 3, RequiredDaoLevel = 4, RequiredRealmIndex = 1, QiCost = 50, Cooldown = 36f, Power = 80 },

        // Body
        new TechniqueBlueprint { Id = "body_1", Name = "Дыхание костей", Chinese = "骨息", Dao = DaoType.Body, Kind = TechniqueKind.Passive, Rank = 1, RequiredDaoLevel = 1, RequiredRealmIndex = 0, QiCost = 0, Cooldown = 0f, Power = 0 },
        new TechniqueBlueprint { Id = "body_2", Name = "Каменное тело", Chinese = "石身", Dao = DaoType.Body, Kind = TechniqueKind.Block, Rank = 2, RequiredDaoLevel = 2, RequiredRealmIndex = 0, QiCost = 25, Cooldown = 12f, Power = 0 },
        new TechniqueBlueprint { Id = "body_3", Name = "Небесное тело", Chinese = "天身", Dao = DaoType.Body, Kind = TechniqueKind.Aura, Rank = 3, RequiredDaoLevel = 4, RequiredRealmIndex = 1, QiCost = 55, Cooldown = 45f, Power = 0 },

        // Fire
        new TechniqueBlueprint { Id = "fire_1", Name = "Огненный шар", Chinese = "火球", Dao = DaoType.Fire, Kind = TechniqueKind.Active, Rank = 1, RequiredDaoLevel = 1, RequiredRealmIndex = 0, QiCost = 18, Cooldown = 6f, Power = 45 },
        new TechniqueBlueprint { Id = "fire_2", Name = "Пламенный щит", Chinese = "焰盾", Dao = DaoType.Fire, Kind = TechniqueKind.Block, Rank = 2, RequiredDaoLevel = 2, RequiredRealmIndex = 0, QiCost = 30, Cooldown = 14f, Power = 0 },
        new TechniqueBlueprint { Id = "fire_3", Name = "Огненный вихрь", Chinese = "火旋", Dao = DaoType.Fire, Kind = TechniqueKind.Aura, Rank = 3, RequiredDaoLevel = 4, RequiredRealmIndex = 1, QiCost = 65, Cooldown = 40f, Power = 100 },

        // Thunder
        new TechniqueBlueprint { Id = "thunder_1", Name = "Громовой удар", Chinese = "雷击", Dao = DaoType.Thunder, Kind = TechniqueKind.Active, Rank = 1, RequiredDaoLevel = 1, RequiredRealmIndex = 0, QiCost = 20, Cooldown = 7f, Power = 50 },
        new TechniqueBlueprint { Id = "thunder_2", Name = "Громоотвод", Chinese = "引雷", Dao = DaoType.Thunder, Kind = TechniqueKind.Block, Rank = 2, RequiredDaoLevel = 2, RequiredRealmIndex = 0, QiCost = 28, Cooldown = 13f, Power = 0 },
        new TechniqueBlueprint { Id = "thunder_3", Name = "Гнев небес", Chinese = "天怒", Dao = DaoType.Thunder, Kind = TechniqueKind.Aura, Rank = 3, RequiredDaoLevel = 4, RequiredRealmIndex = 1, QiCost = 70, Cooldown = 50f, Power = 110 }
    };

    public static List<TechniqueBlueprint> GetTechniques(DaoType type)
    {
        var result = new List<TechniqueBlueprint>();
        for (int i = 0; i < Techniques.Length; i++)
        {
            if (Techniques[i].Dao == type) result.Add(Techniques[i]);
        }
        return result;
    }

    public static List<CultivationRealm> CreateRealms()
    {
        var realms = new List<CultivationRealm>();
        for (int i = 0; i < Realms.Length; i++)
            realms.Add(CreateRealm(Realms[i], i));
        return realms;
    }

    public static CultivationRealm CreateRealm(RealmBlueprint blueprint, int index)
    {
        var realm = ScriptableObject.CreateInstance<CultivationRealm>();
        realm.name = "Realm_" + (index + 1) + "_" + blueprint.Name;
        realm.hideFlags = HideFlags.DontSave;
        realm.Setup(blueprint.Name, blueprint.Chinese, blueprint.Description, index, 3,
            blueprint.QiBonus, blueprint.HpBonus, blueprint.RegenBonus, blueprint.Comprehension, blueprint.Chance,
            new List<string>(blueprint.Materials));
        return realm;
    }

    public static List<DaoPath> CreateDaoPaths()
    {
        var paths = new List<DaoPath>();
        foreach (DaoType type in DaoTypeInfo.All)
            paths.Add(CreateDaoPath(type));
        return paths;
    }

    public static DaoPath CreateDaoPath(DaoType type)
    {
        var path = ScriptableObject.CreateInstance<DaoPath>();
        path.name = "Dao_" + type;
        path.hideFlags = HideFlags.DontSave;

        var techniques = new List<TechniqueData>();
        var blueprints = GetTechniques(type);
        for (int i = 0; i < blueprints.Count; i++)
            techniques.Add(CreateTechnique(blueprints[i]));

        path.Setup(type, DaoTypeInfo.GetDisplayName(type), DaoTypeInfo.GetChineseName(type),
            "Путь " + type + ": техники открываются по уровню Дао и реалму.",
            DaoTypeInfo.GetColor(type), 10, null, techniques);
        return path;
    }

    public static TechniqueData CreateTechnique(TechniqueBlueprint blueprint)
    {
        var technique = ScriptableObject.CreateInstance<TechniqueData>();
        technique.name = "Technique_" + blueprint.Id;
        technique.hideFlags = HideFlags.DontSave;

        string description = blueprint.Name + " — " + blueprint.Kind + " техника пути " +
                             DaoTypeInfo.GetDisplayName(blueprint.Dao) + ".";

        technique.Setup(blueprint.Id, blueprint.Name, blueprint.Chinese, description, blueprint.Dao,
            blueprint.Kind, blueprint.Rank, blueprint.RequiredDaoLevel, blueprint.RequiredRealmIndex,
            blueprint.QiCost, blueprint.Cooldown, blueprint.Power, null, null, null);
        return technique;
    }
}
