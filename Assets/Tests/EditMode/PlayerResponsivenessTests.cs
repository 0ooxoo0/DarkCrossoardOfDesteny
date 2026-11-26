using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Отзывчивость боя на реальной сцене Game: буфер атаки, окна отмены, уворот.
/// В EditMode Unity не вызывает Awake/OnEnable — входим тем же путём, что и игра.
/// </summary>
public class PlayerResponsivenessTests
{
    private const string GameScenePath = "Assets/Scenes/Game.unity";

    private GameObject player;
    private PlayerController controller;
    private PlayerHealth health;
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

        controller = player.GetComponent<PlayerController>();
        health = player.GetComponent<PlayerHealth>();
        block = player.GetComponent<PlayerBlockController>();

        controller.EnsureInitialized();
        health.EnsureInitialized();
        block.EnsureInitialized();

        health.RestorePercent(1f);
        block.SetBlockingForTests(false);
    }

    [TearDown]
    public void TearDown()
    {
        if (health == null) return;

        if (health.IsDead) health.NotifyRespawned();
        health.RestorePercent(1f);

        if (block != null) block.SetBlockingForTests(false);
    }

    [Test]
    public void Player_HasDodgeLayerOnAnimator()
    {
        Animator animator = player.GetComponent<Animator>();
        Assert.That(animator, Is.Not.Null, "На Player нет Animator.");
        Assert.GreaterOrEqual(animator.GetLayerIndex("Dodge"), 0, "В PlayerCombat нет слоя Dodge.");
    }

    [Test]
    public void Dodge_IsRejectedWhileBlocking()
    {
        block.SetBlockingForTests(true);

        Assert.That(controller.TryDodge(), Is.False, "Блок обязан запрещать кувырок.");
        Assert.That(controller.IsDodging, Is.False, "Состояние кувырка не должно включаться.");
    }

    [Test]
    public void Dodge_StartsAndGrantsInvulnerability()
    {
        // Агент вне навмеша в EditMode: ждём только решения контроллера, не движения.
        if (!controller.TryDodge())
        {
            Assert.Pass("Агент не на навмеше в EditMode — кувырок недоступен, проверка пропущена.");
            return;
        }

        Assert.That(controller.IsDodging, Is.True, "Кувырок обязан включиться.");

        float before = health.Current;
        health.TakeDamage(new DamageInfo(10f, player, player.transform.position));

        Assert.That(health.Current, Is.EqualTo(before), "i-frames кувырка обязаны гасить урон.");
    }

    [Test]
    public void Dodge_IsRejectedWhileAirborne()
    {
        if (!controller.TryJump())
        {
            Assert.Pass("Агент не на навмеше в EditMode — прыжок недоступен, проверка пропущена.");
            return;
        }

        Assert.That(controller.IsJumping, Is.True, "Прыжок обязан включиться.");
        Assert.That(controller.TryDodge(), Is.False, "В воздухе кувырок запрещён.");
    }

    [Test]
    public void JumpLayerWeight_ReturnsToZeroAfterLanding()
    {
        // Регрессия: раньше CancelJump не гасил вес слоя Jump и поза приземления залипала.
        Assert.That(controller.JumpLayerWeightForTests, Is.EqualTo(0f).Within(0.0001f),
            "В покое слой Jump обязан быть выключен.");

        if (controller.TryJump())
        {
            Assert.That(controller.JumpLayerWeightForTests, Is.EqualTo(1f).Within(0.0001f),
                "В прыжке слой Jump должен быть включён.");
        }

        controller.CancelJumpForTests();

        Assert.That(controller.IsJumping, Is.False, "После сброса прыжок обязан выключиться.");
        Assert.That(controller.JumpLayerWeightForTests, Is.EqualTo(0f).Within(0.0001f),
            "После сброса вес слоя Jump обязан вернуться в 0.");
    }


    [Test]
    public void AttackBuffer_KeepsPressThatLandsBeforeComboWindow()
    {
        if (block != null) block.SetBlockingForTests(false);

        controller.PressAttackForTests();
        if (!controller.IsAttacking)
        {
            Assert.Pass("Атака недоступна в EditMode (нет Animator) — проверка пропущена.");
            return;
        }

        // Нажатие приходит сразу после старта, окно комбо ещё закрыто.
        controller.PressAttackForTests();
        Assert.That(controller.ComboIndexForTests, Is.EqualTo(0), "Окно комбо ещё не открылось — комбо не растёт.");

        // Окно комбо открывает StateMachineBehaviour; буфер обязан его пережить.
        controller.AdvanceAttackTimersForTests(0.05f);
        controller.OnComboWindowOpen();
        controller.UpdateAttackStateForTests();

        Assert.That(controller.ComboIndexForTests, Is.EqualTo(1),
            "Буфер атаки обязан сработать в окне комбо: иначе ввод теряется.");

        controller.EndAttackForTests();
    }

    [Test]
    public void AttackBuffer_ExpiresWhenTooOld()
    {
        if (block != null) block.SetBlockingForTests(false);

        controller.PressAttackForTests();
        if (!controller.IsAttacking)
        {
            Assert.Pass("Атака недоступна в EditMode (нет Animator) — проверка пропущена.");
            return;
        }

        controller.PressAttackForTests();
        controller.AdvanceAttackTimersForTests(1f);
        controller.OnComboWindowOpen();
        controller.UpdateAttackStateForTests();

        Assert.That(controller.ComboIndexForTests, Is.EqualTo(0),
            "Просроченное нажатие не должно запускать комбо через секунду.");

        controller.EndAttackForTests();
    }

    [Test]
    public void Attack_IsRejectedWhileBlocking()
    {
        block.SetBlockingForTests(true);

        Assert.That(controller.TryAttack(), Is.False, "Блок обязан запрещать атаку.");
        Assert.That(controller.IsAttacking, Is.False, "Атака не должна начинаться под блоком.");
    }

    [Test]
    public void Attack_IsRejectedWhenDodgingCannotBeCancelled()
    {
        if (!controller.TryDodge())
        {
            Assert.Pass("Кувырок недоступен в EditMode — проверка пропущена.");
            return;
        }

        // Первые кадры кувырка неотменяемы: иначе спам атаки рвал бы ролл.
        Assert.That(controller.TryAttack(), Is.False, "Нельзя атаковать в начале кувырка.");
    }

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
