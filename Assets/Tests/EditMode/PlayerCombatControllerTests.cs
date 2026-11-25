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
    public void PlayerCombat_AllAnimationClipsComeFromFantacode()
    {
        foreach (string path in AssetDatabase.GetDependencies(ControllerPath, true))
        {
            if (!path.EndsWith(".anim") && !path.EndsWith(".fbx")) continue;
            StringAssert.StartsWith(FantacodeRoot, path,
                "Клип вне Fantacode Studios подключён к PlayerCombat.controller: " + path);
        }
    }
}
