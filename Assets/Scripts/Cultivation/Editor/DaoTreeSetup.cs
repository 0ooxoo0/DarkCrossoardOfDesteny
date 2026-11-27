#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Собирает древо Дао из префабов Assets/Magic: каждый префаб-скилл становится
/// TechniqueData с типом доставки, ценой в очках и предпосылкой предыдущего яруса.
/// Меню: Tools → Cultivation → Build Dao Tree From Magic. Идемпотентно.
/// </summary>
public static class DaoTreeSetup
{
    private const string MagicFolder = "Assets/Magic";
    private const string TechniqueFolder = "Assets/Scripts/Cultivation/Data/Techniques";
    private const string DaoFolder = "Assets/Scripts/Cultivation/Data/Dao";
    private const string TreePrefix = "Tree_";

    /// <summary>Категория префаба: задаёт доставку, урон и стоимость.</summary>
    private enum MagicCategory
    {
        Projectile,
        Beam,
        Aura,
        Shield,
        Portal,
        Impact,
        Slash,
        Muzzle,
        Trail,
        Character
    }

    /// <summary>Строка дерева: префаб + школа + ветка.</summary>
    private sealed class Node
    {
        public string Path;
        public string Name;
        public DaoType Dao;
        public MagicCategory Category;
        public int Rank;
        public string PrerequisiteId;
    }

    [MenuItem("Tools/Cultivation/Build Dao Tree From Magic", priority = 3)]
    public static void BuildTree()
    {
        EnsureFolder(TechniqueFolder);

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { MagicFolder });
        var nodes = new List<Node>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            string folder = Path.GetFileName(Path.GetDirectoryName(path));
            string name = prefab.name;
            MagicCategory category = ResolveCategory(folder, name);

            // Шлейф и риг персонажа — не скиллы: узлов дерева не создаём.
            if (category == MagicCategory.Trail || category == MagicCategory.Character) continue;

            nodes.Add(new Node
            {
                Path = path,
                Name = name,
                Dao = ResolveDao(name, category),
                Category = category
            });
        }

        // Ярусы внутри школы: сначала простые эффекты, потом сильнее.
        AssignRanks(nodes);

        var created = new List<TechniqueData>();
        var byDao = new Dictionary<DaoType, List<TechniqueData>>();

        for (int i = 0; i < nodes.Count; i++)
        {
            Node node = nodes[i];
            TechniqueData technique = CreateOrUpdate(node);
            created.Add(technique);

            if (!byDao.TryGetValue(node.Dao, out List<TechniqueData> list))
            {
                list = new List<TechniqueData>();
                byDao[node.Dao] = list;
            }
            list.Add(technique);
        }

        AttachToDaoPaths(byDao);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[DaoTree] Готово: " + created.Count + " узлов дерева из префабов " + MagicFolder +
                  " (школ: " + byDao.Count + ").");
    }

    /// <summary>Идемпотентность: существующий ассет обновляется, а не дублируется.</summary>
    private static TechniqueData CreateOrUpdate(Node node)
    {
        string id = TreePrefix + Sanitize(node.Path.Substring(MagicFolder.Length + 1));
        string assetPath = TechniqueFolder + "/Technique_" + id + ".asset";

        var technique = AssetDatabase.LoadAssetAtPath<TechniqueData>(assetPath);
        if (technique == null)
        {
            technique = ScriptableObject.CreateInstance<TechniqueData>();
            AssetDatabase.CreateAsset(technique, assetPath);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(node.Path);
        StrengthFor(node.Category, out int power, out int qiCost, out float cooldown);
        DeliveryFor(node.Category, out DeliveryType delivery, out float speed, out float lifetime);
        float range = RangeFor(node.Category, node.Dao);
        float radius = Mathf.Clamp(1f + power * 0.03f, 1.5f, 5f);
        string description = node.Name + " — " + CategoryName(node.Category) + " школы «" +
                             DaoTypeInfo.GetDisplayName(node.Dao) + "»: " + KindWord(node.Category) +
                             ", сила " + (power > 0 ? power.ToString() : "—") + ", Ци " + qiCost +
                             ", КД " + cooldown.ToString("0.#") + " с.";

        technique.Setup(id, node.Name, string.Empty, description, node.Dao, TechniqueKind.Active,
            node.Rank, RequiredLevelFor(node.Rank), 0, qiCost, cooldown, power, null, null, prefab);
        technique.SetAim(range, radius);

        float travelSpeed = delivery == DeliveryType.Projectile ? speed : 0f;
        technique.SetDelivery(delivery, travelSpeed, delivery == DeliveryType.Aura ? 8f : lifetime, null, null);
        // Первый узел ветки бесплатен: с него игрок начинает дерево без очков.
        int cost = node.PrerequisiteId == null ? 0 : CostFor(node.Rank);
        technique.SetTree(cost, node.PrerequisiteId != null ? new[] { node.PrerequisiteId } : new string[0],
            node.PrerequisiteId == null);

        EditorUtility.SetDirty(technique);
        return technique;
    }

    private static void AttachToDaoPaths(Dictionary<DaoType, List<TechniqueData>> byDao)
    {
        foreach (var pair in byDao)
        {
            DaoType type = pair.Key;
            string path = DaoFolder + "/Dao_" + type + ".asset";
            var daoPath = AssetDatabase.LoadAssetAtPath<DaoPath>(path);
            if (daoPath == null)
            {
                Debug.LogWarning("[DaoTree] Нет " + path + " — запустите Setup Test Assets.");
                continue;
            }

            var serialized = new SerializedObject(daoPath);
            SerializedProperty list = serialized.FindProperty("techniques");
            if (list == null) continue;

            // Узлы дерева дописываются к базовым техникам, дубликаты не создаются.
            var existing = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                Object value = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (value != null) existing.Add(value);
            }

            for (int i = 0; i < pair.Value.Count; i++)
            {
                TechniqueData technique = pair.Value[i];
                if (existing.Contains(technique)) continue;

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = technique;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(daoPath);
        }
    }

    /// <summary>
    /// Ярусы внутри школы: узлы сортируются по силе (взрыв → снаряд → луч → аура → щит → портал),
    /// но ветка всегда начинается с бесплатного узла: цепочка предпосылок не должна
    /// упираться в платный ярус.
    /// </summary>
    private static void AssignRanks(List<Node> nodes)
    {
        var byDao = new Dictionary<DaoType, List<Node>>();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (!byDao.TryGetValue(nodes[i].Dao, out List<Node> list))
            {
                list = new List<Node>();
                byDao[nodes[i].Dao] = list;
            }
            list.Add(nodes[i]);
        }

        foreach (var pair in byDao)
        {
            pair.Value.Sort(CompareNodes);

            string previous = null;
            for (int i = 0; i < pair.Value.Count; i++)
            {
                Node node = pair.Value[i];
                node.Rank = Mathf.Min(i + 1, MaxRank);
                node.PrerequisiteId = i == 0 ? null : previous;
                previous = TreePrefix + Sanitize(node.Path.Substring(MagicFolder.Length + 1));
            }
        }
    }

    /// <summary>Порядок в ветке: сначала дешёвые категории, внутри категории — по имени.</summary>
    private static int CompareNodes(Node a, Node b)
    {
        int byCategory = RankFor(a.Category).CompareTo(RankFor(b.Category));
        return byCategory != 0 ? byCategory : string.CompareOrdinal(a.Name, b.Name);
    }

    /// <summary>Выше восьмого яруса дерево не растёт: уровней Дао на большее не хватает.</summary>
    private const int MaxRank = 8;

    /// <summary>Порядок категорий в ветке: чем позже, тем выше ярус.</summary>
    private static int RankFor(MagicCategory category)
    {
        switch (category)
        {
            case MagicCategory.Impact:
            case MagicCategory.Muzzle:
            case MagicCategory.Slash: return 1;
            case MagicCategory.Projectile: return 2;
            case MagicCategory.Beam: return 3;
            case MagicCategory.Aura: return 4;
            case MagicCategory.Shield: return 5;
            case MagicCategory.Portal: return 6;
            default: return 1;
        }
    }

    private static int CostFor(int rank)
    {
        // Растёт с ярусом: 1/2/3/4/6/8/10/12.
        switch (rank)
        {
            case 1: return 1;
            case 2: return 2;
            case 3: return 3;
            case 4: return 4;
            case 5: return 6;
            case 6: return 8;
            case 7: return 10;
            default: return 12;
        }
    }

    /// <summary>Требование уровня Дао: не выше потолка школы (4 в текущем контенте).</summary>
    private static int RequiredLevelFor(int rank) => Mathf.Clamp((rank + 1) / 2, 1, 4);

    private static void StrengthFor(MagicCategory category, out int power, out int qiCost, out float cooldown)
    {
        switch (category)
        {
            case MagicCategory.Impact: power = 30; qiCost = 12; cooldown = 4f; break;
            case MagicCategory.Muzzle: power = 25; qiCost = 10; cooldown = 3f; break;
            case MagicCategory.Slash: power = 35; qiCost = 14; cooldown = 5f; break;
            case MagicCategory.Projectile: power = 55; qiCost = 20; cooldown = 8f; break;
            case MagicCategory.Beam: power = 70; qiCost = 30; cooldown = 14f; break;
            case MagicCategory.Aura: power = 40; qiCost = 40; cooldown = 30f; break;
            case MagicCategory.Shield: power = 0; qiCost = 35; cooldown = 25f; break;
            case MagicCategory.Portal: power = 0; qiCost = 50; cooldown = 40f; break;
            default: power = 30; qiCost = 12; cooldown = 4f; break;
        }
    }

    private static void DeliveryFor(MagicCategory category, out DeliveryType delivery,
        out float speed, out float lifetime)
    {
        switch (category)
        {
            case MagicCategory.Projectile: delivery = DeliveryType.Projectile; speed = 18f; lifetime = 4f; break;
            case MagicCategory.Beam: delivery = DeliveryType.Beam; speed = 0f; lifetime = 2f; break;
            case MagicCategory.Aura: delivery = DeliveryType.Aura; speed = 0f; lifetime = 8f; break;
            case MagicCategory.Shield:
            case MagicCategory.Portal: delivery = DeliveryType.Self; speed = 0f; lifetime = 10f; break;
            default: delivery = DeliveryType.Impact; speed = 0f; lifetime = 3f; break;
        }
    }

    private static float RangeFor(MagicCategory category, DaoType dao)
    {
        switch (category)
        {
            case MagicCategory.Impact:
            case MagicCategory.Muzzle:
            case MagicCategory.Slash: return 4f;
            case MagicCategory.Shield:
            case MagicCategory.Portal: return 4f;
            case MagicCategory.Beam: return 22f;
            case MagicCategory.Projectile: return 20f;
            case MagicCategory.Aura: return 8f;
            default: return dao == DaoType.Fire || dao == DaoType.Thunder ? 18f : 6f;
        }
    }

    private static string CategoryName(MagicCategory category)
    {
        switch (category)
        {
            case MagicCategory.Projectile: return "снаряд";
            case MagicCategory.Beam: return "луч";
            case MagicCategory.Aura: return "аура";
            case MagicCategory.Shield: return "щит";
            case MagicCategory.Portal: return "портал";
            case MagicCategory.Impact: return "взрыв";
            case MagicCategory.Slash: return "росчерк";
            case MagicCategory.Muzzle: return "вспышка";
            default: return "эффект";
        }
    }

    private static string KindWord(MagicCategory category)
    {
        switch (category)
        {
            case MagicCategory.Projectile: return "летит в точку прицела";
            case MagicCategory.Beam: return "бьёт лучом по цели";
            case MagicCategory.Aura: return "действует вокруг игрока";
            case MagicCategory.Shield:
            case MagicCategory.Portal: return "эффект на игроке";
            default: return "мгновенный удар в точке прицела";
        }
    }

    /// <summary>Маппинг имени и папки на школу. Правится здесь одной таблицей.</summary>
    private static DaoType ResolveDao(string name, MagicCategory category)
    {
        string lower = name.ToLowerInvariant();

        if (lower.Contains("fire") || lower.Contains("incinerate") || lower.Contains("plasma")) return DaoType.Fire;
        if (lower.Contains("lightning") || lower.Contains("electric") || lower.Contains("thunder")) return DaoType.Thunder;
        if (lower.Contains("slash") || lower.Contains("beam")) return DaoType.Sword;
        if (lower.Contains("gun") || lower.Contains("impact") || lower.Contains("muzzle")) return DaoType.Fist;
        if (lower.Contains("shield") || lower.Contains("portal") || lower.Contains("ice") ||
            lower.Contains("sand") || lower.Contains("water")) return DaoType.Body;

        switch (category)
        {
            case MagicCategory.Beam:
            case MagicCategory.Slash: return DaoType.Sword;
            case MagicCategory.Projectile:
            case MagicCategory.Impact: return DaoType.Fire;
            default: return DaoType.Body;
        }
    }

    private static MagicCategory ResolveCategory(string folder, string name)
    {
        string lower = name.ToLowerInvariant();

        if (folder == "Trail") return MagicCategory.Trail;
        if (folder == "CharacterEffect") return MagicCategory.Character;
        if (folder == "Shield") return MagicCategory.Shield;
        if (folder == "Portal") return MagicCategory.Portal;
        if (folder == "PersonAura" || folder == "otherAura" || folder == "Fire") return MagicCategory.Aura;
        if (folder == "2Dammage" && lower.Contains("beam")) return MagicCategory.Beam;
        if (folder == "FihtEffect")
        {
            if (lower.Contains("slash")) return MagicCategory.Slash;
            if (lower.Contains("muzzle")) return MagicCategory.Muzzle;
            return MagicCategory.Impact;
        }

        if (folder == "1Dammage")
        {
            if (lower.Contains("projectile") || lower.Contains("spiral") || lower.Contains("bullet"))
                return MagicCategory.Projectile;
            return MagicCategory.Impact;
        }

        return MagicCategory.Impact;
    }

    private static string Sanitize(string value)
    {
        value = value.Replace('\\', '_').Replace('/', '_').Replace(' ', '_');
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return value;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(parent)) return;
        parent = parent.Replace("\\", "/");

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif
