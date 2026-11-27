using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Лестница сложности врага 1..4 и её применение через <see cref="EnemyCombatAI"/>.
/// Тест сторожит числа, которые заказчик задал таблицей: уровень 1 не защищается вовсе,
/// уровень 4 парирует, а смена уровня действует в тот же кадр.
/// </summary>
public class EnemyDifficultyTests
{
    private static EnemyCombatAI MakeAI()
    {
        var go = new GameObject("EnemyTest");
        go.SetActive(false);
        var ai = go.AddComponent<EnemyCombatAI>();
        return ai;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var ai in Object.FindObjectsByType<EnemyCombatAI>(FindObjectsSortMode.None))
            if (ai != null && ai.gameObject.name == "EnemyTest") Object.DestroyImmediate(ai.gameObject);
    }

    [Test]
    public void Level1_DoesNotBlockOrParry()
    {
        var ai = MakeAI();
        ai.SetIntelligence(1);

        Assert.That(ai.BlockChance, Is.EqualTo(0f), "Уровень 1 не должен блокировать ни разу.");
        Assert.That(ai.ComboChance, Is.EqualTo(0f), "Уровень 1 не должен бить сериями.");
        Assert.That(ai.ParryChance, Is.EqualTo(0f), "Уровень 1 не парирует.");
        Assert.That(ai.ReactionDelay, Is.EqualTo(0.8f).Within(0.001f), "Реакция уровня 1 — 0.8 с.");
    }

    [Test]
    public void Level2_BlocksButNeverParries()
    {
        var ai = MakeAI();
        ai.SetIntelligence(2);

        Assert.That(ai.BlockChance, Is.GreaterThan(0f), "Уровень 2 обязан блокировать.");
        Assert.That(ai.ParryChance, Is.EqualTo(0f), "Уровень 2 не контратакует после блока.");
        Assert.That(ai.ReactionDelay, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void Level3_HitsHarderAndLongerThanLevel1()
    {
        var level1 = MakeAI();
        level1.SetIntelligence(1);
        float cooldown1 = level1.EffectiveCooldown;

        var level3 = MakeAI();
        level3.SetIntelligence(3);

        Assert.That(level3.EffectiveCooldown, Is.LessThan(cooldown1),
            "На уровне 3 интервал между ударами обязан быть короче, чем на 1.");
        Assert.That(level3.MaxCombo, Is.EqualTo(3), "Уровень 3 бьёт сериями до 3 ударов.");
        Assert.That(level3.ComboChance, Is.GreaterThan(0.5f));
        Assert.That(level3.ParryChance, Is.EqualTo(0f), "Уровень 3 ещё не парирует.");
    }

    [Test]
    public void Level4_ParriesAndBeatsLevel1OnEveryCombatAxis()
    {
        var level1 = MakeAI();
        level1.SetIntelligence(1);

        var level4 = MakeAI();
        level4.SetIntelligence(4);

        Assert.That(level4.ParryChance, Is.GreaterThan(0f), "Уровень 4 обязан парировать.");
        Assert.That(level4.BlockChance, Is.GreaterThan(level1.BlockChance));
        Assert.That(level4.ComboChance, Is.GreaterThan(level1.ComboChance));
        Assert.That(level4.EffectiveDamage, Is.GreaterThan(level1.EffectiveDamage));
        Assert.That(level4.EffectiveMoveSpeed, Is.GreaterThan(level1.EffectiveMoveSpeed));
        Assert.That(level4.EffectiveCooldown, Is.LessThan(level1.EffectiveCooldown));
        Assert.That(level4.ReactionDelay, Is.LessThan(level1.ReactionDelay));
    }

    [Test]
    public void SetIntelligence_AppliesImmediately()
    {
        var ai = MakeAI();
        ai.SetIntelligence(1);
        Assert.That(ai.IntelligenceLevel, Is.EqualTo(1));
        Assert.That(ai.BlockChance, Is.EqualTo(0f));

        ai.SetIntelligence(4);
        Assert.That(ai.IntelligenceLevel, Is.EqualTo(4));
        Assert.That(ai.ParryChance, Is.GreaterThan(0f),
            "Смена уровня обязана пересчитать шансы сразу, без перезапуска сцены.");
    }

    [Test]
    public void ApplyDifficulty_ReadsNumbersFromAsset()
    {
        var config = ScriptableObject.CreateInstance<DifficultyConfig>();
        config.intelligence = 4;
        config.blockChance = 0.9f;
        config.parryChance = 0.33f;
        config.comboChance = 0.44f;
        config.reactionDelay = 0.11f;
        config.maxCombo = 3;

        var ai = MakeAI();
        ai.ApplyDifficulty(config);

        Assert.That(ai.IntelligenceLevel, Is.EqualTo(4));
        Assert.That(ai.BlockChance, Is.EqualTo(0.9f).Within(0.001f), "Ассет важнее встроенной таблицы.");
        Assert.That(ai.ParryChance, Is.EqualTo(0.33f).Within(0.001f));
        Assert.That(ai.ComboChance, Is.EqualTo(0.44f).Within(0.001f));
        Assert.That(ai.ReactionDelay, Is.EqualTo(0.11f).Within(0.001f));

        Object.DestroyImmediate(config);
    }

    [Test]
    public void DifficultyAssets_MatchTheFourStepLadder()
    {
        string[] paths =
        {
            "Assets/Enemies/Data/Difficulty_Easy.asset",
            "Assets/Enemies/Data/Difficulty_Normal.asset",
            "Assets/Enemies/Data/Difficulty_Hard.asset",
            "Assets/Enemies/Data/Difficulty_Nightmare.asset"
        };

        for (int i = 0; i < paths.Length; i++)
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<DifficultyConfig>(paths[i]);
            Assert.That(config, Is.Not.Null, "Не найден ассет " + paths[i]);
            Assert.That(config.intelligence, Is.EqualTo(i + 1),
                config.name + " обязан соответствовать ступени " + (i + 1));

            var preset = DifficultyConfig.GetPreset(i + 1);
            Assert.That(config.blockChance, Is.EqualTo(preset.blockChance).Within(0.001f),
                config.name + ": blockChance разошёлся с таблицей уровней.");
            Assert.That(config.parryChance, Is.EqualTo(preset.parryChance).Within(0.001f),
                config.name + ": parryChance разошёлся с таблицей уровней.");
            Assert.That(config.maxCombo, Is.EqualTo(preset.maxCombo),
                config.name + ": maxCombo разошёлся с таблицей уровней.");
        }
    }

    [Test]
    public void EnemyVisuals_HaveAnimatorController()
    {
        // EnemyVisualData живёт в Assembly-CSharp и тестовой сборке не видна,
        // поэтому читаем ассеты через SerializedObject, а не через тип.
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:ScriptableObject",
                     new[] { "Assets/Enemies/Data" }))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("EnemyVisual_")) continue;

            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            Assert.That(asset, Is.Not.Null, "Не загрузился " + path);

            var so = new UnityEditor.SerializedObject(asset);
            var prop = so.FindProperty("animatorController");
            Assert.That(prop, Is.Not.Null, path + " без поля animatorController.");
            Assert.That(prop.objectReferenceValue, Is.Not.Null,
                path + " без animatorController — враг останется без анимаций.");
        }
    }
}
