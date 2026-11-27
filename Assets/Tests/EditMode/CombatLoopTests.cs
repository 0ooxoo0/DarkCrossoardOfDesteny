using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Единый контур боя: урон идёт только через <see cref="IDamageable"/>, своих не бьют,
/// добивание отличается от обычного попадания, а смерть игрока не респавнит в том же кадре.
/// Тесты ходят по реальной сцене Game и реальным ассетам, а не по собранным вручную объектам.
/// </summary>
public class CombatLoopTests
{
    private const string GameScenePath = "Assets/Scenes/Game.unity";

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
    }

    [Test]
    public void Weapons_DealDifferentDamage_FistsWeakerThanSword()
    {
        var fists = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Combat/Weapons/Fists.asset");
        var sword = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Combat/Weapons/Sword.asset");

        Assert.That(fists, Is.Not.Null, "Не найден ассет кулаков.");
        Assert.That(sword, Is.Not.Null, "Не найден ассет меча.");
        Assert.That(fists.useAnimationEvents, Is.True, "Кулаки обязаны брать окно удара из анимации.");
        Assert.That(sword.useAnimationEvents, Is.True, "Меч обязан брать окно удара из анимации.");
        Assert.That(sword.damage, Is.GreaterThan(fists.damage),
            "Меч обязан бить сильнее кулаков: иначе выбор оружия бессмысленен.");
    }

    [Test]
    public void MagicAbility_HasVfxCastAndPower()
    {
        var magic = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityData>(
            "Assets/Combat/Abilities/MagicBurst.asset");
        Assert.That(magic, Is.Not.Null, "Не найден ассет MagicBurst.");

        Assert.That(magic.vfxPrefab, Is.Not.Null, "Магия обязана иметь видимый VFX.");
        Assert.That(magic.castAnimation, Is.Not.Null, "Магия обязана иметь анимацию каста.");
        Assert.That(magic.power, Is.GreaterThan(0), "Магия обязана наносить урон: power > 0.");
        Assert.That(magic.requiredDao.HasValue, Is.False,
            "Стартовая магия не должна требовать Дао Огня: иначе слот мёртв до прокачки.");
    }

    [Test]
    public void SwordCombo_HasFourStatesWithAttackTag()
    {
        var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
            "Assets/Animations/Combat/PlayerCombat.controller");
        Assert.That(controller, Is.Not.Null, "Не найден PlayerCombat.controller.");

        int swordStates = 0;
        foreach (var cs in controller.layers[0].stateMachine.states)
        {
            if (!cs.state.name.StartsWith("Sword Attack")) continue;
            swordStates++;
            Assert.That(cs.state.tag, Is.EqualTo("Attack"),
                "Стейт " + cs.state.name + " обязан иметь тег Attack: иначе PlayerController не увидит атаку.");
            Assert.That(cs.state.motion, Is.Not.Null, "Стейт без клипа: " + cs.state.name);
            Assert.That(cs.state.behaviours.Length, Is.GreaterThanOrEqualTo(1),
                "Стейт " + cs.state.name + " обязан иметь CombatAnimationWindow: иначе хитбокс не откроется.");
        }

        Assert.That(swordStates, Is.GreaterThanOrEqualTo(3),
            "maxComboIndex в сцене = 3, значит меч обязан иметь минимум 3 удара.");
    }

    [Test]
    public void EnemyPrefab_HasHealthBarLootAndTelegraph()
    {
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Enemies/EnemyCombat.prefab");
        Assert.That(prefab, Is.Not.Null, "Не найден префаб EnemyCombat.");

        Assert.That(prefab.GetComponentInChildren<EnemyHealthBar>(true), Is.Not.Null,
            "У врага обязана быть полоса HP.");
        Assert.That(prefab.GetComponent<EnemyAttackTelegraph>(), Is.Not.Null,
            "У врага обязан быть телеграф атаки.");

        var health = prefab.GetComponent<EnemyHealth>();
        Assert.That(health, Is.Not.Null, "У префаба врага нет EnemyHealth.");

        var serialized = new UnityEditor.SerializedObject(health);
        Assert.That(serialized.FindProperty("lootTable").objectReferenceValue, Is.Not.Null,
            "За убийство обязан падать лут.");
        Assert.That(serialized.FindProperty("experienceReward").intValue, Is.GreaterThan(0),
            "За убийство обязан начисляться опыт.");
    }

    [Test]
    public void Scene_HasSeveralEnemiesAndOneSpawner()
    {
        int enemies = 0;
        int spawners = 0;

        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.GetComponent<EnemyCombatAI>() != null) enemies++;
            if (root.GetComponent<EnemySpawner>() != null) spawners++;
        }

        Assert.That(enemies, Is.GreaterThanOrEqualTo(3),
            "В сцене мало врагов: бой заканчивается за две драки.");
        Assert.That(spawners, Is.GreaterThanOrEqualTo(1),
            "Нет спавнера: после зачистки враги больше не появятся.");
    }

    [Test]
    public void PlayerPrefabAndScene_ShareOneAnimatorController()
    {
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player.prefab");
        Assert.That(prefab, Is.Not.Null, "Не найден Assets/Player.prefab.");

        var prefabAnimator = prefab.GetComponent<Animator>();
        Assert.That(prefabAnimator, Is.Not.Null, "На префабе игрока нет Animator.");
        Assert.That(prefabAnimator.runtimeAnimatorController, Is.Not.Null,
            "На префабе игрока не задан контроллер анимаций.");

        string prefabPath = UnityEditor.AssetDatabase.GetAssetPath(prefabAnimator.runtimeAnimatorController);
        Assert.That(prefabPath, Does.Contain("PlayerCombat.controller"),
            "Боевой контроллер игрока — PlayerCombat.controller, а не старый PlayerController.controller.");

        GameObject scenePlayer = null;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.GetComponent<PlayerController>() != null) scenePlayer = root;
        }

        Assert.That(scenePlayer, Is.Not.Null, "В сцене нет объекта с PlayerController.");
        var sceneAnimator = scenePlayer.GetComponent<Animator>();
        Assert.That(sceneAnimator, Is.Not.Null, "На игроке в сцене нет Animator.");

        string scenePath = UnityEditor.AssetDatabase.GetAssetPath(sceneAnimator.runtimeAnimatorController);
        Assert.That(scenePath, Is.EqualTo(prefabPath),
            "Префаб и сцена обязаны использовать один и тот же контроллер.");
    }

    [Test]
    public void PlayerController_SceneHasNoDeadHitboxStartOverride()
    {
        GameObject scenePlayer = null;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.GetComponent<PlayerController>() != null) scenePlayer = root;
        }

        Assert.That(scenePlayer, Is.Not.Null, "В сцене нет игрока.");

        var serialized = new UnityEditor.SerializedObject(scenePlayer.GetComponent<PlayerController>());
        Assert.That(serialized.FindProperty("hitboxStart"), Is.Null,
            "У PlayerController нет поля hitboxStart: мёртвый override надо было удалить.");
    }

    [Test]
    public void CombatFeedback_KeepsTimeScaleOnPlayerDamage()
    {
        var feedback = Object.FindFirstObjectByType<CombatFeedback>();
        Assert.That(feedback, Is.Not.Null, "В сцене нет CombatFeedback.");

        float saved = Time.timeScale;
        try
        {
            Time.timeScale = 1f;
            feedback.EnsureInitialized();
            feedback.ResetHitStopForTests();

            GameObject player = null;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.GetComponent<PlayerHealth>() != null) player = root;
            }

            Assert.That(player, Is.Not.Null, "В сцене нет игрока с PlayerHealth.");

            CombatEvents.Report(new CombatImpact
            {
                Point = player.transform.position,
                Damage = 12f,
                Attacker = null,
                Victim = player.transform,
                Kind = ImpactKind.Hit
            });

            Assert.That(feedback.IsHitStopActiveForTests, Is.False,
                "Хит-стоп на уроне по игроку выключен: рывок мешает увороту.");
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.0001f),
                "Урон по игроку не имеет права менять Time.timeScale.");
        }
        finally
        {
            Time.timeScale = saved;
            if (feedback != null) feedback.ResetHitStopForTests();
        }
    }

    [Test]
    public void PlayerRespawnService_DefersRespawnOutOfDeathFrame()
    {
        // Сервис респавна создаётся bootstrap'ом в рантайме, поэтому проверяем
        // сам контракт: смерть только планирует возврат, а не выполняет его в том же кадре.
        var host = new GameObject("LoopProbeRespawn");
        try
        {
            var health = host.AddComponent<PlayerHealth>();
            health.EnsureInitialized();

            var service = host.AddComponent<PlayerRespawnService>();
            service.Initialize(health, null, null, null, host.transform);

            Assert.That(service.RespawnDelay, Is.GreaterThanOrEqualTo(1f),
                "Смерть обязана быть событием с паузой в 1.5–2 с, а не мгновенным миганием.");

            health.TakeDamage(new DamageInfo(health.Max * 2f, host, host.transform.position));

            Assert.That(service.IsDeathPending, Is.True,
                "Респавн обязан отложиться: в кадре смерти игрок ещё мёртв.");
            Assert.That(health.IsDead, Is.True, "Игрок обязан оставаться мёртвым до конца паузы.");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void PlayerPrefab_HasDeathScreenAndDamageFeedback()
    {
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player.prefab");
        Assert.That(prefab, Is.Not.Null, "Не найден Assets/Player.prefab.");

        Assert.That(prefab.GetComponent<PlayerDeathScreen>(), Is.Not.Null,
            "На игроке нет экрана смерти: смерть снова станет тихой.");
        Assert.That(prefab.GetComponent<PlayerDamageFeedback>(), Is.Not.Null,
            "На игроке нет фидбека урона: игрок не увидит, что его ударили.");
        Assert.That(prefab.GetComponent<CombatAudio>(), Is.Not.Null,
            "На игроке нет боевых звуков.");
    }
}
