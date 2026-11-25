using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Смерть и респавн игрока на реальной сцене Game.
/// Сцена открывается явно, а объект берётся перебором её корней:
/// тесты проверяют настоящие сериализованные ссылки, а не собранный вручную объект.
/// </summary>
public class PlayerDeathTests
{
    private const string GameScenePath = "Assets/Scenes/Game.unity";

    private GameObject player;
    private PlayerHealth health;
    private PlayerController controller;
    private PlayerBlockController block;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
    }

    [SetUp]
    public void SetUp()
    {
        player = FindPlayerRoot();
        Assert.That(player, Is.Not.Null, "В сцене Game нет объекта с PlayerController.");

        health = player.GetComponent<PlayerHealth>();
        controller = player.GetComponent<PlayerController>();
        block = player.GetComponent<PlayerBlockController>();

        Assert.That(health, Is.Not.Null, "На Player нет PlayerHealth.");
        Assert.That(controller, Is.Not.Null, "На Player нет PlayerController.");
        Assert.That(block, Is.Not.Null, "На Player нет PlayerBlockController.");

        // В EditMode Unity не вызывает Awake/OnEnable — входим тем же путём, что и игра.
        health.EnsureInitialized();
        controller.EnsureInitialized();
        block.EnsureInitialized();
    }

    [TearDown]
    public void TearDown()
    {
        if (health == null) return;

        // Возвращаем игрока в живое состояние с полным здоровьем.
        if (health.IsDead) health.NotifyRespawned();
        health.RestorePercent(1f);
    }

    [Test]
    public void Death_RaisesOnDiedExactlyOnce()
    {
        int diedCount = 0;
        health.OnDied += () => diedCount++;

        Kill();

        Assert.That(health.IsDead, Is.True, "Игрок должен быть мёртв.");
        Assert.That(diedCount, Is.EqualTo(1), "OnDied обязан сработать ровно один раз.");
    }

    [Test]
    public void Death_IsIdempotent_WhenDamageRepeats()
    {
        int diedCount = 0;
        health.OnDied += () => diedCount++;

        Kill();
        health.TakeDamage(new DamageInfo(health.Max, player, player.transform.position));

        Assert.That(diedCount, Is.EqualTo(1), "Повторный урон не должен поднимать OnDied снова.");
    }

    [Test]
    public void Death_ClearsBlock()
    {
        block.SetBlockingForTests(true);
        Assert.That(block.IsBlocking, Is.True, "Перед смертью блок должен быть активен.");

        Kill();

        Assert.That(block.IsBlocking, Is.False, "Смерть обязана снять блок.");
    }

    [Test]
    public void Death_StopsMovement()
    {
        controller.SetMoveInputForTests(new Vector2(0f, 1f));
        controller.HandleMovementForTests();
        Assert.That(controller.CurrentSpeedForTests, Is.GreaterThan(0f), "Живой игрок должен двигаться.");

        Kill();
        controller.SetMoveInputForTests(new Vector2(0f, 1f));
        controller.HandleMovementForTests();

        Assert.That(controller.CurrentSpeedForTests, Is.EqualTo(0f).Within(0.0001f),
            "Мёртвый игрок не должен набирать скорость.");
    }

    [Test]
    public void Player_HealthAndBlockShareOneGameObject_AndHaveDeathPresentation()
    {
        Assert.That(health.gameObject, Is.SameAs(block.gameObject),
            "PlayerHealth и PlayerBlockController обязаны быть на одном объекте.");

        Assert.That(player.GetComponent<PlayerDeathPresentation>(), Is.Not.Null,
            "Презентация смерти обязана присутствовать.");
    }

    [Test]
    public void CultivationBootstrap_AddOrGet_DoesNotDuplicateExistingHealth()
    {
        PlayerHealth before = player.GetComponent<PlayerHealth>();

        PlayerHealth result = CultivationBootstrap.AddOrGet<PlayerHealth>(player);

        Assert.That(result, Is.SameAs(before), "Bootstrap обязан вернуть существующий компонент.");
        Assert.That(player.GetComponents<PlayerHealth>().Length, Is.EqualTo(1),
            "Дубликат PlayerHealth создавать нельзя.");
    }

    private void Kill()
    {
        health.TakeDamage(new DamageInfo(health.Max * 2f, player, player.transform.position));
    }

    /// <summary>Перебор корней сцены вместо поиска по имени: явный и предсказуемый.</summary>
    private static GameObject FindPlayerRoot()
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].GetComponent<PlayerController>() != null) return roots[i];
        }

        return null;
    }
}
