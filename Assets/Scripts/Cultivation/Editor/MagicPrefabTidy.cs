#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Гигиена префабов магии, которые пошли в узлы дерева: снять демо-скрипты AllIn1
/// (свои тайминги и тряска камеры), сделать одноразовые эффекты одноразовыми,
/// добавить якорный Root в начало координат и погасить вечные эмиттеры.
/// Меню: Tools → Cultivation → Tidy Magic Prefabs. Идемпотентно.
/// </summary>
public static class MagicPrefabTidy
{
    private const string MagicFolder = "Assets/Magic";
    private const string AnchorName = "Root";

    /// <summary>Демо-скрипты AllIn1, которые навязывают эффекту своё поведение.</summary>
    private static readonly string[] DemoScripts =
    {
        "AllIn1VfxAutoDestroy",
        "AllIn1DoShake",
        "AllIn1VfxFadeLight",
        "AllIn1AutoRotate"
    };

    [MenuItem("Tools/Cultivation/Tidy Magic Prefabs", priority = 4)]
    public static void TidyPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { MagicFolder });

        int touched = 0;
        int removed = 0;
        int stopped = 0;
        int anchored = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            bool changed = false;

            // Снег в Ice Impact сыпется после смерти эффекта: вечные эмиттеры гасим всегда.
            removed += RemoveDemoScripts(root, ref changed);
            stopped += StopLoopingEmitters(path, root, ref changed);

            if (NeedAnchor(root))
            {
                AddAnchor(root);
                anchored++;
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                touched++;
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[MagicPrefabTidy] Префабов изменено: " + touched +
                  ", снято демо-скриптов: " + removed +
                  ", погашено вечных эмиттеров: " + stopped +
                  ", добавлено якорей Root: " + anchored + ".");
    }

    private static int RemoveDemoScripts(GameObject root, ref bool changed)
    {
        int count = 0;
        var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null) continue;
            if (System.Array.IndexOf(DemoScripts, behaviour.GetType().Name) < 0) continue;

            Object.DestroyImmediate(behaviour, true);
            count++;
            changed = true;
        }

        return count;
    }

    /// <summary>
    /// Одноразовые эффекты не должны эмитить вечно: loop = 0 и stopAction = Destroy.
    /// Ауры, щиты и порталы живут до конца действия — их эмиттеры не трогаем.
    /// </summary>
    private static int StopLoopingEmitters(string path, GameObject root, ref bool changed)
    {
        if (IsSustainedEffect(path)) return 0;

        int count = 0;
        var systems = root.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem system = systems[i];
            if (system == null) continue;

            ParticleSystem.MainModule main = system.main;
            if (!main.loop) continue;

            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;
            count++;
            changed = true;
        }

        return count;
    }

    /// <summary>Долгоживущие эффекты: аура, щит, портал, область. Путь важнее имени.</summary>
    private static bool IsSustainedEffect(string path)
    {
        string lower = path.ToLowerInvariant();
        return lower.Contains("/personaura/") || lower.Contains("/otheraura/") ||
               lower.Contains("/shield/") || lower.Contains("/portal/") ||
               lower.Contains("/fire/") || lower.Contains("area");
    }

    /// <summary>Якорь добавляется один раз: повторный запуск видит Root и ничего не делает.</summary>
    private static bool NeedAnchor(GameObject root)
    {
        return root.transform.Find(AnchorName) == null;
    }

    /// <summary>Пустой дочерний GO в начале координат: точка спавна и поворота эффекта.</summary>
    private static void AddAnchor(GameObject root)
    {
        var anchor = new GameObject(AnchorName);
        anchor.transform.SetParent(root.transform, false);
        anchor.transform.localPosition = Vector3.zero;
        anchor.transform.localRotation = Quaternion.identity;
        anchor.transform.localScale = Vector3.one;
    }
}
#endif
