using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Временный помощник: подключает редактор к запущенному MCP-серверу
/// без ручного нажатия кнопки Connect. Удаляется после использования.
/// </summary>
[InitializeOnLoad]
public static class McpBootstrap
{
    private static double _nextAttempt;
    private static int _attempts;
    private static bool _connected;

    static McpBootstrap()
    {
        _nextAttempt = 0.0;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (_connected)
        {
            EditorApplication.update -= Tick;
            return;
        }

        if (EditorApplication.timeSinceStartup < _nextAttempt) return;
        _nextAttempt = EditorApplication.timeSinceStartup + 3.0;
        _attempts++;

        if (_attempts > 60)
        {
            EditorApplication.update -= Tick;
            Debug.LogWarning("[McpBootstrap] Попытки подключения исчерпаны.");
            return;
        }

        EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);

        try
        {
            Type locator = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("MCPForUnity.Editor.Services.MCPServiceLocator"))
                .FirstOrDefault(t => t != null);

            if (locator == null) return;

            PropertyInfo bridgeProperty = locator.GetProperty("Bridge", BindingFlags.Public | BindingFlags.Static);
            if (bridgeProperty == null) return;

            object bridge = bridgeProperty.GetValue(null, null);
            if (bridge == null) return;

            MethodInfo start = bridge.GetType().GetMethod("StartAsync", Type.EmptyTypes);
            if (start == null) return;

            Task<bool> task = start.Invoke(bridge, null) as Task<bool>;
            if (task == null) return;

            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Debug.Log("[McpBootstrap] Ошибка подключения: " + t.Exception.Message);
                else
                    Debug.Log("[McpBootstrap] StartAsync -> " + t.Result);

                if (!t.IsFaulted && t.Result) _connected = true;
            });
        }
        catch (Exception e)
        {
            Debug.Log("[McpBootstrap] Исключение: " + e.Message);
        }
    }
}
