using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Боевой фидбек: хит-стоп обязан заканчиваться и не ломать паузу.
/// </summary>
public class CombatFeedbackTests
{
    private const string GameScenePath = "Assets/Scenes/Game.unity";
    private const string PlayerPrefabPath = "Assets/Player.prefab";

    private float savedTimeScale;
    private CombatFeedback feedback;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
    }

    [SetUp]
    public void SetUp()
    {
        savedTimeScale = Time.timeScale;
        Time.timeScale = 1f;

        feedback = Object.FindFirstObjectByType<CombatFeedback>();
        Assert.That(feedback, Is.Not.Null, "В сцене Game нет CombatFeedback — фидбек удара не подключён.");

        // В EditMode Unity не вызывает OnEnable — входим тем же путём, что и игра.
        feedback.EnsureInitialized();
        feedback.ResetHitStopForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = savedTimeScale;
    }

    [Test]
    public void PlayerPrefab_ProvidesImpactEffects()
    {
        GameObject root = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(root, Is.Not.Null, "Не найден префаб игрока: " + PlayerPrefabPath);

        CombatFeedback prefabFeedback = root.GetComponent<CombatFeedback>();
        Assert.That(prefabFeedback, Is.Not.Null, "На префабе игрока нет CombatFeedback.");

        var serialized = new UnityEditor.SerializedObject(prefabFeedback);
        Assert.That(serialized.FindProperty("impactEffect").objectReferenceValue, Is.Not.Null,
            "Не задан префаб impact-эффекта: удары не будут искрить.");
    }

    [Test]
    public void HitStop_SlowsThenRestoresTimeScale()
    {
        Time.timeScale = 1f;

        HitEnemy();

        Assert.That(feedback.IsHitStopActiveForTests, Is.True, "Хит-стоп обязан запуститься на попадании.");
        Assert.That(Time.timeScale, Is.LessThan(1f), "На хит-стопе время обязано замедлиться.");

        // Один длинный unscaled-шаг: хит-стоп обязан сам себя завершить.
        feedback.TickHitStopForTests(1f);

        Assert.That(feedback.IsHitStopActiveForTests, Is.False, "Хит-стоп обязан завершаться.");
        Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.0001f),
            "Время обязано вернуться к 1: иначе игра залипает в замедлении.");
    }

    [Test]
    public void HitStop_DoesNotResumePausedGame()
    {
        HitEnemy();
        Assert.That(feedback.IsHitStopActiveForTests, Is.True, "Хит-стоп обязан запуститься на попадании.");

        // Пауза меню во время хит-стопа: фидбек не имеет права её снимать.
        Time.timeScale = 0f;
        feedback.TickHitStopForTests(1f);

        Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.0001f),
            "Хит-стоп обязан уважать паузу и не включать время обратно.");
    }

    [Test]
    public void BlockedImpact_DoesNotSlowTime()
    {
        CombatEvents.Report(new CombatImpact
        {
            Point = Vector3.zero,
            Damage = 0f,
            Attacker = feedback.gameObject,
            Victim = feedback.transform,
            Kind = ImpactKind.Blocked
        });

        Assert.That(feedback.IsHitStopActiveForTests, Is.False, "Блок не замедляет время.");
        Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.0001f), "Блок не трогает Time.timeScale.");
    }

    private static void HitEnemy()
    {
        CombatEvents.Report(new CombatImpact
        {
            Point = Vector3.zero,
            Damage = 10f,
            Attacker = null,
            Victim = null,
            Kind = ImpactKind.Hit
        });
    }
}
