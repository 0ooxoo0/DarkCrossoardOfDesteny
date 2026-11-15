using UnityEngine;

/// <summary>
/// Диагностика на старте сцены: если панели инвентаря нет, пишет в консоль,
/// что её нужно собрать через Tools → Inventory → Setup Test Data.
/// Не мешает игре и ничего не создаёт автоматически.
/// </summary>
public static class InventoryRuntimeGuard
{
    /// <summary>Проверяет наличие панели после загрузки сцены.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CheckPanel()
    {
        if (Object.FindFirstObjectByType<InventoryPanelUI>(FindObjectsInactive.Include) != null) return;

        Debug.LogWarning("[Inventory] Панель инвентаря не найдена в сцене. " +
                         "Выйдите из Play mode и запустите Tools → Inventory → Setup Test Data.");
    }
}
