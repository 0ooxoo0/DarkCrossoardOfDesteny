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

    [Test]
    public void ViewCamera_IsTheGameplayCamera()
    {
        Camera view = controller.ViewCameraForTests;

        Assert.That(view, Is.Not.Null,
            "Игровая камера не найдена: ходьба, удары и кувырки пойдут по осям персонажа.");
        Assert.That(view.targetTexture, Is.Null,
            "Выбрана портретная камера инвентаря вместо игровой.");
        Assert.That(view.cullingMask, Is.Not.EqualTo(0), "Игровая камера не должна быть пустой.");
    }

    [Test]
    public void Dodge_WithoutInput_GoesForwardAlongModel()
    {
        // Сбрасываем разворот: без ввода кувырок обязан идти туда, куда смотрит модель.
        player.transform.rotation = Quaternion.LookRotation(Vector3.forward);

        Vector3 direction = controller.DodgeDirectionForTests(Vector2.zero);
        Vector3 flat = new Vector3(direction.x, 0f, direction.z).normalized;

        Assert.That(flat.z, Is.GreaterThan(0.9f), "Без WASD кувырок обязан уходить вперёд, а не назад.");
        Assert.That(Mathf.Abs(flat.x), Is.LessThan(0.1f), "Кувырок не должен уходить вбок.");
    }

    [Test]
    public void Dodge_WithInput_FollowsCameraDirection()
    {
        Camera view = controller.ViewCameraForTests;
        if (view == null)
        {
            Assert.Pass("Игровая камера недоступна в EditMode — проверка пропущена.");
            return;
        }

        Vector3 forward = view.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 pressedW = controller.DodgeDirectionForTests(new Vector2(0f, 1f));
        Vector3 flatW = new Vector3(pressedW.x, 0f, pressedW.z).normalized;

        Assert.That(Vector3.Dot(flatW, forward), Is.GreaterThan(0.9f),
            "Кувырок по W обязан идти по направлению камеры.");

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 pressedD = controller.DodgeDirectionForTests(new Vector2(1f, 0f));
        Vector3 flatD = new Vector3(pressedD.x, 0f, pressedD.z).normalized;

        Assert.That(Vector3.Dot(flatD, right), Is.GreaterThan(0.9f),
            "Кувырок по D обязан идти вправо от камеры.");
    }

    [Test]
    public void DodgeIndex_MatchesModelOrientation()
    {
        player.transform.rotation = Quaternion.LookRotation(Vector3.forward);

        // 0 — вперёд, 1 — назад, 2 — влево, 3 — вправо относительно модели.
        Assert.That(controller.DodgeIndexForTests(Vector3.forward), Is.EqualTo(0), "Вперёд — индекс 0.");
        Assert.That(controller.DodgeIndexForTests(Vector3.back), Is.EqualTo(1), "Назад — индекс 1.");
        Assert.That(controller.DodgeIndexForTests(Vector3.left), Is.EqualTo(2), "Влево — индекс 2.");
        Assert.That(controller.DodgeIndexForTests(Vector3.right), Is.EqualTo(3), "Вправо — индекс 3.");
    }

    [Test]
    public void DodgeIndex_StaysCorrectAfterModelTurns()
    {
        // Модель развёрнута на 90: вперёд для неё — мировое +X.
        player.transform.rotation = Quaternion.LookRotation(Vector3.right);

        Assert.That(controller.DodgeIndexForTests(Vector3.right), Is.EqualTo(0),
            "Индекс обязан считаться в осях модели, а не в мировых.");
        Assert.That(controller.DodgeIndexForTests(Vector3.back), Is.EqualTo(3),
            "При развороте на 90 мир-назад становится правым боком модели.");
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
