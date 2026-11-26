using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Защита от падений на смерти: смерть и визуал удара обязаны доводиться
/// в конце кадра, а не внутри физического колбэка хитбокса.
/// </summary>
public class CombatCrashSafetyTests
{
    private const string GameScenePath = "Assets/Scenes/Game.unity";
    private const System.Reflection.BindingFlags Private =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
    }

    [Test]
    public void HarvestableObject_DefersDeathOutOfPhysicsCallback()
    {
        var host = new GameObject("CrashProbeHarvestable");
        try
        {
            var health = host.AddComponent<HarvestableObject>();

            // Awake/Start в EditMode Unity не вызывает: поля ставим теми же
            // значениями, что и игра, чтобы проверялся сам порядок смерти.
            typeof(HarvestableObject).GetField("maxHealth", Private).SetValue(health, 10f);
            typeof(HarvestableObject).GetField("currentHealth", Private).SetValue(health, 10f);

            health.TakeDamage(100f, Vector3.zero);

            Assert.That(Field<bool>(health, "deathPending"), Is.True,
                "Смерть обязана отложиться: Destroy и выброс лута внутри физического шага роняют редактор.");
            Assert.That(Field<bool>(health, "isDead"), Is.False,
                "Внутри колбэка объект ещё живой.");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void EnemyHealth_DefersDeathOutOfPhysicsCallback()
    {
        var host = new GameObject("CrashProbeEnemy");
        try
        {
            var enemy = host.AddComponent<EnemyHealth>();

            // Awake в EditMode не вызывается — здоровье выставляем вручную.
            typeof(EnemyHealth).GetField("currentHealth", Private).SetValue(enemy, enemy.MaxHealth);

            enemy.TakeDamage(new DamageInfo(enemy.MaxHealth + 50f, null, Vector3.zero));

            Assert.That(Field<bool>(enemy, "deathPending"), Is.True,
                "Смерть врага обязана откладываться до конца кадра.");
            Assert.That(enemy.IsDead, Is.False, "Внутри колбэка враг ещё живой.");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void CombatFeedback_QueuesVisualsInsteadOfSpawningInline()
    {
        CombatFeedback feedback = Object.FindFirstObjectByType<CombatFeedback>();
        Assert.That(feedback, Is.Not.Null, "В сцене Game нет CombatFeedback.");

        feedback.EnsureInitialized();

        int queued = feedback.QueueImpactForTests(new CombatImpact
        {
            Point = Vector3.one,
            Damage = 7f,
            Attacker = feedback.gameObject,
            Victim = feedback.transform,
            Kind = ImpactKind.Hit
        });

        Assert.That(queued, Is.EqualTo(1),
            "Визуал обязан встать в очередь: Instantiate VFX прямо из физического колбэка роняет редактор.");

        feedback.ResetHitStopForTests();
    }

    private static T Field<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, Private).GetValue(target);
    }
}
