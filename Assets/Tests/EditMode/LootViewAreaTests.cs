using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Область просмотра камеры: подбор клавиатурой обязан видеть только то,
/// что перед игроком, и не тянуться к контейнеру за спиной.
/// </summary>
public class LootViewAreaTests
{
    private GameObject cameraHost;
    private GameObject containerHost;
    private LootViewArea area;

    [SetUp]
    public void SetUp()
    {
        cameraHost = new GameObject("ProbeCamera");
        var camera = cameraHost.AddComponent<Camera>();
        cameraHost.transform.position = Vector3.zero;
        cameraHost.transform.rotation = Quaternion.identity;

        containerHost = new GameObject("ProbeContainer");
        containerHost.AddComponent<WorldLootContainer>();

        var areaHost = new GameObject("ProbeArea");
        area = areaHost.AddComponent<LootViewArea>();

        var serialized = new UnityEditor.SerializedObject(area);
        serialized.FindProperty("viewCamera").objectReferenceValue = camera;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(area.gameObject);
        Object.DestroyImmediate(containerHost);
        Object.DestroyImmediate(cameraHost);
    }

    [Test]
    public void ContainerInFrontOfCamera_IsVisible()
    {
        containerHost.transform.position = new Vector3(0f, 0f, 3f);
        AddLoot();

        area.Refresh();

        Assert.That(area.Visible.Count, Is.EqualTo(1),
            "Контейнер прямо перед камерой обязан попасть в область просмотра.");
    }

    [Test]
    public void ContainerBehindCamera_IsIgnored()
    {
        containerHost.transform.position = new Vector3(0f, 0f, -3f);
        AddLoot();

        area.Refresh();

        Assert.That(area.Visible.Count, Is.EqualTo(0),
            "Контейнер за спиной не должен становиться целью подбора.");
    }

    [Test]
    public void ContainerBeyondViewDistance_IsIgnored()
    {
        containerHost.transform.position = new Vector3(0f, 0f, 60f);
        AddLoot();

        area.Refresh();

        Assert.That(area.Visible.Count, Is.EqualTo(0),
            "Слишком далёкий контейнер не должен попадать в область просмотра.");
    }

    [Test]
    public void ContainerFarSideways_IsIgnored()
    {
        containerHost.transform.position = new Vector3(25f, 0f, 3f);
        AddLoot();

        area.Refresh();

        Assert.That(area.Visible.Count, Is.EqualTo(0),
            "Контейнер далеко в стороне не должен попадать в область просмотра.");
    }

    [Test]
    public void EmptyContainer_IsIgnored()
    {
        containerHost.transform.position = new Vector3(0f, 0f, 3f);

        area.Refresh();

        Assert.That(area.Visible.Count, Is.EqualTo(0),
            "Пустой контейнер незачем предлагать к подбору.");
    }

    [Test]
    public void NearestContainerComesFirst()
    {
        containerHost.transform.position = new Vector3(0f, 0f, 8f);
        AddLoot();

        var nearHost = new GameObject("ProbeNear");
        try
        {
            nearHost.AddComponent<WorldLootContainer>();
            nearHost.transform.position = new Vector3(0f, 0f, 2f);
            AddLoot(nearHost);

            area.Refresh();

            Assert.That(area.Visible.Count, Is.EqualTo(2), "Оба контейнера в кадре.");
            Assert.That(area.Visible[0].gameObject.name, Is.EqualTo("ProbeNear"),
                "Ближний контейнер обязан идти первым: клавиатура выбирает его.");
        }
        finally
        {
            Object.DestroyImmediate(nearHost);
        }
    }

    private void AddLoot(GameObject target = null)
    {
        var host = target != null ? target : containerHost;
        var container = host.GetComponent<WorldLootContainer>();
        container.Add(Item(), 1);
    }

    private static ItemData Item()
    {
        var item = ScriptableObject.CreateInstance<ItemData>();
        item.name = "ProbeItem";
        return item;
    }
}
