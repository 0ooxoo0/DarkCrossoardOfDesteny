using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Автоматически создаёт InputRemapManager и подключает экран управления
/// к уже существующей панели Canvas → PauseMenu → «Настройки Управления».
///
/// Ничего в сцене размечать не нужно: компонент добавляется в рантайме.
/// Если панель называется иначе, поменяйте PanelName.
/// </summary>
public static class ControlsSettingsBootstrap
{
    /// <summary>Имя GameObject панели, внутри которой строится список.</summary>
    public const string PanelName = "Настройки Управления";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateManager()
    {
        InputRemapManager.EnsureExists();

        // После перезагрузки домена подписка могла остаться — снимаем и ставим заново.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForFirstScene()
    {
        Install(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Install(scene);
    }

    private static void Install(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        if (Object.FindFirstObjectByType<ControlsSettingsPanel>(FindObjectsInactive.Include) != null) return;

        GameObject panel = FindPanel(scene);
        if (panel == null) return;

        // Панель не должна висеть поверх паузы до первого нажатия «Управление».
        if (panel.activeSelf) panel.SetActive(false);

        panel.AddComponent<ControlsSettingsPanel>();
    }

    private static GameObject FindPanel(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                if (transforms[j].name == PanelName)
                    return transforms[j].gameObject;
            }
        }

        return null;
    }
}
