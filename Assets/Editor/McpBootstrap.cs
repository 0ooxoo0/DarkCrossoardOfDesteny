using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Временный помощник: подключает редактор к запущенному MCP-серверу
/// без ручного нажатия кнопки Connect. Удаляется после использования.
/// Повторяет попытки и пишет диагностику в Console/Editor.log.
/// </summary>
[InitializeOnLoad]
public static class McpBootstrap
{
    private const string LogTag = "[McpBootstrap] ";

    private static double _nextAttempt;
    private static int _attempts;
    private static bool _done;

    static McpBootstrap()
    {
        Debug.Log(LogTag + "init");
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (_done)
        {
            EditorApplication.update -= Tick;
            return;
        }

        if (EditorApplication.timeSinceStartup < _nextAttempt) return;
        _nextAttempt = EditorApplication.timeSinceStartup + 5.0;
        _attempts++;

        if (_attempts > 120)
        {
            EditorApplication.update -= Tick;
            Debug.LogWarning(LogTag + "попытки исчерпаны");
            return;
        }

        EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);

        try
        {
            // Снимаем «сессия уже стартовала» у штатного автозапуска,
            // иначе он больше не пробует подключиться в этой сессии Unity.
            SessionState.EraseBool("HttpAutoStartHandler.SessionInitialized");
            SessionState.EraseBool("HttpAutoStartHandler.ConnectPending");

            Assembly asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "MCPForUnity.Editor");
            if (asm == null)
            {
                if (_attempts % 6 == 1) Debug.Log(LogTag + "сборка MCPForUnity.Editor не найдена");
                return;
            }

            Type handler = asm.GetType("MCPForUnity.Editor.Services.HttpAutoStartHandler");
            if (handler != null)
            {
                MethodInfo auto = handler.GetMethod("AutoStartAsync", BindingFlags.NonPublic | BindingFlags.Static);
                if (auto != null)
                {
                    object result = auto.Invoke(null, null);
                    Task task = result as Task;
                    if (task != null)
                    {
                        task.ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                                Debug.LogWarning(LogTag + "AutoStartAsync fault: " + t.Exception);
                            else
                                Debug.Log(LogTag + "AutoStartAsync завершён");
                        });
                    }
                    Debug.Log(LogTag + "AutoStartAsync вызван (попытка " + _attempts + ")");
                    return;
                }
            }

            Type locator = asm.GetType("MCPForUnity.Editor.Services.MCPServiceLocator");
            if (locator == null) return;
            PropertyInfo bridgeProp = locator.GetProperty("Bridge", BindingFlags.Public | BindingFlags.Static);
            if (bridgeProp == null) return;
            object bridge = bridgeProp.GetValue(null, null);
            MethodInfo start = bridge != null ? bridge.GetType().GetMethod("StartAsync", Type.EmptyTypes) : null;
            if (start == null) return;

            Task<bool> connect = start.Invoke(bridge, null) as Task<bool>;
            if (connect == null) return;
            connect.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Debug.LogWarning(LogTag + "StartAsync fault: " + t.Exception);
                else
                    Debug.Log(LogTag + "StartAsync -> " + t.Result);
                if (!t.IsFaulted && t.Result) _done = true;
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning(LogTag + "исключение: " + e);
        }
    }
}
