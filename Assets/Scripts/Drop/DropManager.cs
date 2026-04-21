using System.Collections.Generic;
using UnityEngine;

// Перечисление типов интерактивных объектов
public enum ResourceType
{
    Tree,
    Stone,
    Crate,
    Enemy
}

// Статический класс для управления дропом
public static class DropManager
{
    // Класс для описания возможного дропа с вероятностью
    [System.Serializable]
    public class DropChance
    {
        public GameObject itemPrefab; // Префаб выпадающего предмета
        [Range(0f, 1f)] public float chance; // Вероятность выпадения (0..1)
        public int minLevel = 1; // Минимальный уровень для этого дропа
        public int maxLevel = 99; // Максимальный уровень
    }

    // Таблицы дропа для разных типов ресурсов
    private static Dictionary<ResourceType, List<DropChance>> dropTables = new Dictionary<ResourceType, List<DropChance>>();

    // Инициализация таблиц (вызвать из любого места, например, в Awake GameManager)
    public static void InitializeDropTables(List<DropChance> treeDrops, List<DropChance> stoneDrops, List<DropChance> crateDrops, List<DropChance> enemyDrops)
    {
        dropTables[ResourceType.Tree] = treeDrops;
        dropTables[ResourceType.Stone] = stoneDrops;
        dropTables[ResourceType.Crate] = crateDrops;
        dropTables[ResourceType.Enemy] = enemyDrops;
    }

    // Основной метод: возвращает префаб предмета, который должен выпасть
    public static GameObject GetDropItem(ResourceType type, int level)
    {
        if (!dropTables.ContainsKey(type))
        {
            Debug.LogError($"Drop table for {type} not found!");
            return null;
        }

        List<DropChance> drops = dropTables[type];
        List<DropChance> validDrops = drops.FindAll(d => level >= d.minLevel && level <= d.maxLevel);

        if (validDrops.Count == 0)
        {
            Debug.LogWarning($"No valid drops for {type} at level {level}");
            return null;
        }

        // Суммируем вероятности для нормализации (на случай если сумма != 1)
        float totalChance = 0f;
        foreach (var drop in validDrops)
            totalChance += drop.chance;

        float randomPoint = Random.value * totalChance;
        float currentSum = 0f;

        foreach (var drop in validDrops)
        {
            currentSum += drop.chance;
            if (randomPoint <= currentSum)
                return drop.itemPrefab;
        }

        // На всякий случай возвращаем первый
        return validDrops[0].itemPrefab;
    }
}