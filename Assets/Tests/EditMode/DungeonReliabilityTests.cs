using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Надёжность генерации подземелья: некорректная конфигурация логируется,
/// но не приводит к исключениям и не портит чужие объекты.
/// Тесты вызывают игровые типы напрямую, без отражения.
/// </summary>
public class DungeonReliabilityTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
    }

    [Test]
    public void PointGenerateData_ForeignWall_DoesNotReparentCollider()
    {
        GameObject pointParent = CreateObject("PointParent");
        GameObject colliderParent = CreateObject("ForeignParent");
        GameObject pointObject = CreateObject("GenerationPoint", pointParent.transform);
        pointObject.SetActive(false);
        GameObject wallObject = CreateObject("Wall", colliderParent.transform);
        wallObject.tag = "Stena";

        PointGenerateData point = pointObject.AddComponent<PointGenerateData>();
        Collider wallCollider = wallObject.AddComponent<BoxCollider>();
        Transform originalParent = wallCollider.transform.parent;

        // Прямой вызов того же входа, что и у физики: без сообщений и отражения.
        point.HandleTriggerEnter(wallCollider);

        Assert.That(wallCollider.transform.parent, Is.SameAs(originalParent),
            "Чужой коллайдер не должен менять родителя.");
        Assert.That(pointObject != null, Is.True);
    }

    [Test]
    public void Room_Coroutine_SnapshotsPointsAndToleratesNullEntry()
    {
        GameObject generatorObject = CreateObject("Generator");
        GenerateData generator = generatorObject.AddComponent<GenerateData>();
        generator.RoomList = new RoomPos
        {
            forward = new List<GameObject>()
        };

        GameObject roomObject = CreateObject("Room", generatorObject.transform);
        roomObject.SetActive(false);
        roomObject.AddComponent<BoxCollider>();
        Room room = roomObject.AddComponent<Room>();
        room.ParentGenerator = generator;

        GameObject pointObject = CreateObject("Point", roomObject.transform);
        pointObject.SetActive(false);
        PointGenerateData point = pointObject.AddComponent<PointGenerateData>();

        room.PointArray.Add(point);
        room.PointArray.Add(null);

        IEnumerator coroutine = room.RunGenerationForTests();
        Assert.That(coroutine.MoveNext(), Is.True, "Ожидался WaitForEndOfFrame.");
        Assert.DoesNotThrow(() => coroutine.MoveNext(), "NULL-элемент не должен ронять проход.");

        // P3.2: обход идёт по снимку, поэтому исходный список не мутируется.
        Assert.That(room.PointArray.Count, Is.EqualTo(2), "Кортеж работает по снимку списка.");
        Assert.That(room.PointArray[0], Is.SameAs(point));
    }

    [Test]
    public void GenerateData_EmptyRoomPool_LogsConfigurationErrorWithoutThrowing()
    {
        GameObject generatorObject = CreateObject("Generator");
        GenerateData generator = generatorObject.AddComponent<GenerateData>();
        generator.RoomList = new RoomPos
        {
            forward = new List<GameObject>(),
            Stop = new List<GameObject>()
        };

        Transform position = CreateObject("Position").transform;
        LogAssert.Expect(LogType.Error, new Regex("RoomList.forward is null or empty"));

        Assert.DoesNotThrow(() => generator._Instanse(position, pointPos.Forward));
    }

    [Test]
    public void GenerateData_NullRoomList_LogsConfigurationErrorWithoutThrowing()
    {
        GameObject generatorObject = CreateObject("Generator");
        GenerateData generator = generatorObject.AddComponent<GenerateData>();
        Transform position = CreateObject("Position").transform;
        LogAssert.Expect(LogType.Error, new Regex("RoomList is null"));

        Assert.DoesNotThrow(() => generator._Instanse(position, pointPos.Forward));
    }

    [Test]
    public void Spawner_EmptySpawnLists_LogsConfigurationErrorWithoutThrowing()
    {
        GameObject generatorObject = CreateObject("Generator");
        GenerateData generator = generatorObject.AddComponent<GenerateData>();
        GameObject spawnerObject = CreateObject("Spawner", generatorObject.transform);
        Spawner spawner = spawnerObject.AddComponent<Spawner>();
        spawner.ChangeSpawnForTests = 100;
        LogAssert.Expect(LogType.Error, new Regex("SpawnObjects|EnemySpawn list is null or empty"));

        Assert.DoesNotThrow(() => spawner.GetSelectedPrefabForTests(generator));
    }

    private GameObject CreateObject(string objectName, Transform parent = null)
    {
        var instance = new GameObject(objectName);
        if (parent != null) instance.transform.SetParent(parent);

        createdObjects.Add(instance);
        return instance;
    }
}
