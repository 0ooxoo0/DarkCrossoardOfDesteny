using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Проверяет Animator Controller игрока: слой прыжка и происхождение клипов.
/// </summary>
public class PlayerCombatControllerTests
{
    private const string ControllerPath = "Assets/Animations/Combat/PlayerCombat.controller";
    private const string FantacodeRoot = "Assets/Unity Technologies/Fantacode Studios/";
    private const string RollRoot = "Assets/Unity Technologies/Sword_sheath_AnimSet/Animation/Humanoid/";

    [Test]
    public void PlayerCombat_HasJumpLayerWithThreePhases()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Assert.IsNotNull(controller, "PlayerCombat.controller не найден: " + ControllerPath);

        int jumpLayer = -1;
        for (int i = 0; i < controller.layers.Length; i++)
            if (controller.layers[i].name == "Jump") jumpLayer = i;

        Assert.GreaterOrEqual(jumpLayer, 0, "В контроллере нет слоя Jump");
        Assert.AreEqual(0f, controller.layers[jumpLayer].defaultWeight, 0.0001f,
            "Слой Jump должен стартовать с весом 0: вес поднимает PlayerController");

        bool hasPhase = false;
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name != "JumpPhase") continue;
            hasPhase = true;
            Assert.AreEqual(AnimatorControllerParameterType.Int, parameter.type,
                "JumpPhase должен быть Int");
        }

        Assert.IsTrue(hasPhase, "В контроллере нет параметра JumpPhase");

        ChildAnimatorState[] states = controller.layers[jumpLayer].stateMachine.states;
        Assert.AreEqual(3, states.Length, "Ожидались 3 состояния прыжка: Jump, Fall, Land");
        foreach (ChildAnimatorState state in states)
            Assert.IsNotNull(state.state.motion, "Состояние без клипа: " + state.state.name);
    }

    [Test]
    public void PlayerCombat_AllAnimationClipsComeFromApprovedPacks()
    {
        // Кувырки взяты из Sword_sheath_AnimSet: в Fantacode нет направленных роллов.
        foreach (string path in AssetDatabase.GetDependencies(ControllerPath, true))
        {
            if (!path.EndsWith(".anim") && !path.EndsWith(".fbx")) continue;

            bool approved = path.StartsWith(FantacodeRoot) || path.StartsWith(RollRoot);
            Assert.IsTrue(approved, "Клип из неразрешённого пака подключён к PlayerCombat.controller: " + path);
        }
    }

    [Test]
    public void PlayerCombat_HasDodgeLayerWithFourRolls()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Assert.IsNotNull(controller, "PlayerCombat.controller не найден: " + ControllerPath);

        int dodgeLayer = -1;
        for (int i = 0; i < controller.layers.Length; i++)
            if (controller.layers[i].name == "Dodge") dodgeLayer = i;

        Assert.GreaterOrEqual(dodgeLayer, 0, "В контроллере нет слоя Dodge");
        Assert.AreEqual(0f, controller.layers[dodgeLayer].defaultWeight, 0.0001f,
            "Слой Dodge должен стартовать с весом 0: вес поднимает PlayerController");

        ChildAnimatorState[] states = controller.layers[dodgeLayer].stateMachine.states;
        Assert.AreEqual(4, states.Length, "Ожидались 4 состояния кувырка по сторонам");
        foreach (ChildAnimatorState state in states)
            Assert.IsNotNull(state.state.motion, "Состояние без клипа: " + state.state.name);
    }

    [Test]
    public void PlayerCombat_JumpLayerRestartsOnPhaseOne()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        int jumpLayer = LayerIndex(controller, "Jump");
        Assert.GreaterOrEqual(jumpLayer, 0, "В контроллере нет слоя Jump");

        // Второй прыжок не запускался, пока слой Jump не имел AnyState-перехода по JumpPhase == 1.
        bool restartOnPhaseOne = false;
        foreach (AnimatorStateTransition transition in controller.layers[jumpLayer].stateMachine.anyStateTransitions)
        {
            if (transition.destinationState == null || transition.destinationState.name != "Jump") continue;

            foreach (AnimatorCondition condition in transition.conditions)
            {
                if (condition.parameter != "JumpPhase") continue;
                if (condition.mode != AnimatorConditionMode.Equals) continue;
                if (Mathf.Approximately(condition.threshold, 1f)) restartOnPhaseOne = true;
            }
        }

        Assert.IsTrue(restartOnPhaseOne, "Нет AnyState-перехода в Jump по JumpPhase == 1");
    }

    private static int LayerIndex(AnimatorController controller, string name)
    {
        for (int i = 0; i < controller.layers.Length; i++)
            if (controller.layers[i].name == name) return i;

        return -1;
    }
}
