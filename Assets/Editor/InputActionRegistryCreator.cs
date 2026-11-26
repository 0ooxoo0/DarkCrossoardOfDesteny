using UnityEditor;
using UnityEngine;

/// <summary>
/// Создаёт Resources/InputActionRegistry.asset из набора по умолчанию.
/// После этого действия можно править в инспекторе, не трогая код.
/// </summary>
public static class InputActionRegistryCreator
{
    private const string ResourcesFolder = "Assets/Resources";
    private const string AssetPath = ResourcesFolder + "/InputActionRegistry.asset";

    [MenuItem("Tools/Input/Create Input Action Registry")]
    public static void CreateRegistryAsset()
    {
        InputActionRegistry existing = AssetDatabase.LoadAssetAtPath<InputActionRegistry>(AssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            AssetDatabase.CreateFolder("Assets", "Resources");

        InputActionRegistry registry = InputActionRegistry.CreateDefault();
        AssetDatabase.CreateAsset(registry, AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = registry;
        EditorGUIUtility.PingObject(registry);
        Debug.Log("[InputRemap] Создан " + AssetPath);
    }

    [MenuItem("Tools/Input/Open Bindings File Folder")]
    public static void OpenBindingsFolder()
    {
        EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
}
