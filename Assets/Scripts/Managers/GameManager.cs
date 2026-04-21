using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Таблицы дропа")]
    [SerializeField] private List<DropManager.DropChance> treeDrops;
    [SerializeField] private List<DropManager.DropChance> stoneDrops;
    [SerializeField] private List<DropManager.DropChance> crateDrops;
    [SerializeField] private List<DropManager.DropChance> enemyDrops;

    private void Awake()
    {
        DropManager.InitializeDropTables(treeDrops, stoneDrops, crateDrops, enemyDrops);
    }
}