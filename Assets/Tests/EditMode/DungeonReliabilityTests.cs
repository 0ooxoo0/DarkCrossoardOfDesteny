using System;
using System.Collections;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class DungeonReliabilityTests
{
    private readonly System.Collections.Generic.List<GameObject> createdObjects =
        new System.Collections.Generic.List<GameObject>();
    private int eventCount;

    [SetUp]
    public void SetUp()
    {
        eventCount = 0;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
        }
        createdObjects.Clear();
    }

    [Test]
    public void PointGenerateData_DifferentParent_DoesNotReparentCollider()
    {
        Type pointType = FindType("PointGenerateData");
        GameObject pointParent = CreateObject("PointParent");
        GameObject colliderParent = CreateObject("ForeignParent");
        GameObject pointObject = CreateObject("GenerationPoint", pointParent.transform);
        pointObject.SetActive(false);
        GameObject wallObject = CreateObject("Wall", colliderParent.transform);
        pointObject.tag = "Untagged";
        wallObject.tag = "Stena";

        Component point = pointObject.AddComponent(pointType);
        Collider wallCollider = wallObject.AddComponent<BoxCollider>();
        Transform originalParent = wallCollider.transform.parent;

        Invoke(point, "OnTriggerEnter", wallCollider);

        Assert.That(wallCollider.transform.parent, Is.SameAs(originalParent));
        string sourcePath = Path.Combine(Application.dataPath, "Scripts", "Dungeros", "PointGenerateData.cs");
        string source = File.ReadAllText(sourcePath);
        StringAssert.DoesNotContain("other.transform.parent = transform.parent", source,
            "Regression guard: trigger handling must never assign another collider's parent.");
        Assert.That(pointObject != null, Is.True);
    }

    [Test]
    public void Room_NullLastEntry_DoesNotThrowAndProcessesValidPoint()
    {
        Type generateType = FindType("GenerateData");
        Type roomType = FindType("Room");
        Type pointType = FindType("PointGenerateData");
        Type roomPosType = FindType("RoomPos");

        GameObject generatorObject = CreateObject("Generator");
        Component generator = generatorObject.AddComponent(generateType);
        GameObject roomObject = CreateObject("Room", generatorObject.transform);
        roomObject.SetActive(false);
        roomObject.AddComponent<BoxCollider>();
        Component room = roomObject.AddComponent(roomType);
        GameObject pointObject = CreateObject("Point", roomObject.transform);
        pointObject.SetActive(false);
        Component point = pointObject.AddComponent(pointType);
        GameObject roomPrefab = CreateObject("GeneratedRoomPrefab");

        object roomPos = Activator.CreateInstance(roomPosType);
        var forward = (System.Collections.IList)Activator.CreateInstance(
            typeof(System.Collections.Generic.List<>).MakeGenericType(typeof(GameObject)));
        forward.Add(roomPrefab);
        SetField(roomPos, "forward", forward);
        SetField(generator, "RoomList", roomPos);
        SetField(room, "_Parent", generator);

        FieldInfo pointsField = roomType.GetField("PointArray");
        var points = (System.Collections.IList)pointsField.GetValue(room);
        points.Add(point);
        points.Add(null);

        IEnumerator coroutine = (IEnumerator)Invoke(room, "InstanseCoroutine");
        Assert.That(coroutine.MoveNext(), Is.True); // WaitForEndOfFrame
        Assert.DoesNotThrow(() => coroutine.MoveNext());
        Assert.That(points.Count, Is.EqualTo(1));
        Assert.That(points[0], Is.SameAs(point));
    }

    [Test]
    public void PlayerAttackInput_OnePress_RaisesOneAcceptedAttackEvent()
    {
        Type controllerType = FindType("PlayerController");
        Type interactionType = FindType("PlayerInteraction");
        GameObject player = CreateObject("Player");
        player.SetActive(false);
        player.AddComponent<UnityEngine.AI.NavMeshAgent>();
        player.AddComponent<Animator>();
        Component controller = player.AddComponent(controllerType);
        Component interaction = player.AddComponent(interactionType);

        EnsureAwake(controller);
        EnsureAwake(interaction);
        interaction.gameObject.SetActive(false);
        SetField(controller, "playerInteraction", null);
        Assert.That(interactionType.GetField("controls", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
        SetField(controller, "isAttacking", true);
        SetField(controller, "comboWindowOpen", true);
        SetField(controller, "comboIndex", 0);

        EventInfo attackStarted = controllerType.GetEvent("AttackStarted");
        Assert.That(attackStarted, Is.Not.Null);
        MethodInfo countEvent = GetType().GetMethod(nameof(CountEvent), BindingFlags.Instance | BindingFlags.Public);
        Delegate handler = Delegate.CreateDelegate(attackStarted.EventHandlerType, this, countEvent);
        attackStarted.AddEventHandler(controller, handler);

        MethodInfo onAttackPressed = controllerType.GetMethod("OnAttackPressed", BindingFlags.Instance | BindingFlags.NonPublic);
        object emptyContext = Activator.CreateInstance(onAttackPressed.GetParameters()[0].ParameterType);
        onAttackPressed.Invoke(controller, new[] { emptyContext });
        onAttackPressed.Invoke(controller, new[] { emptyContext });

        Assert.That(eventCount, Is.EqualTo(1));
        Assert.That((bool)controllerType.GetField("comboQueued", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), Is.True);
        attackStarted.RemoveEventHandler(controller, handler);
    }

    public void CountEvent()
    {
        eventCount++;
    }

    [Test]
    public void GenerateData_EmptyRoomLists_LogsConfigurationErrorWithoutThrowing()
    {
        Type generateType = FindType("GenerateData");
        Type roomPosType = FindType("RoomPos");
        GameObject generatorObject = CreateObject("Generator");
        Component generator = generatorObject.AddComponent(generateType);
        object roomPos = Activator.CreateInstance(roomPosType);
        SetField(roomPos, "forward", Activator.CreateInstance(
            typeof(System.Collections.Generic.List<>).MakeGenericType(typeof(GameObject))));
        SetField(roomPos, "Stop", Activator.CreateInstance(
            typeof(System.Collections.Generic.List<>).MakeGenericType(typeof(GameObject))));
        SetField(generator, "RoomList", roomPos);
        Transform position = CreateObject("Position").transform;
        LogAssert.Expect(LogType.Error, new Regex("RoomList.forward is null or empty"));

        Assert.DoesNotThrow(() => Invoke(generator, "_Instanse", position, Enum.ToObject(FindType("pointPos"), 0)));
    }

    [Test]
    public void GenerateData_NullRoomList_LogsConfigurationErrorWithoutThrowing()
    {
        Type generateType = FindType("GenerateData");
        GameObject generatorObject = CreateObject("Generator");
        Component generator = generatorObject.AddComponent(generateType);
        Transform position = CreateObject("Position").transform;
        LogAssert.Expect(LogType.Error, new Regex("RoomList is null"));

        Assert.DoesNotThrow(() => Invoke(generator, "_Instanse", position, Enum.ToObject(FindType("pointPos"), 0)));
    }

    [Test]
    public void Spawner_EmptySpawnLists_LogsConfigurationErrorWithoutThrowing()
    {
        Type generateType = FindType("GenerateData");
        Type spawnerType = FindType("Spawner");
        GameObject generatorObject = CreateObject("Generator");
        generatorObject.AddComponent(generateType);
        GameObject spawnerObject = CreateObject("Spawner", generatorObject.transform);
        Component spawner = spawnerObject.AddComponent(spawnerType);
        LogAssert.Expect(LogType.Error, new Regex("SpawnObjects list is null or empty"));

        Assert.DoesNotThrow(() => Invoke(spawner, "SpawnObjects"));
    }

    private GameObject CreateObject(string objectName, Transform parent = null)
    {
        GameObject instance = new GameObject(objectName);
        if (parent != null) instance.transform.SetParent(parent);
        createdObjects.Add(instance);
        return instance;
    }

    private static Type FindType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name);
            if (type != null) return type;
        }
        Assert.Fail("Could not find runtime type: " + name);
        return null;
    }

    private static object Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Missing method " + methodName);
        return method.Invoke(target, args);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field " + fieldName);
        field.SetValue(target, value);
    }

    private static void EnsureAwake(Component component)
    {
        FieldInfo controls = component.GetType().GetField("controls", BindingFlags.Instance | BindingFlags.NonPublic);
        if (controls != null && controls.GetValue(component) != null) return;
        Invoke(component, "Awake");
    }

    private static void InvokeIfPresent(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        method?.Invoke(target, null);
    }
}
