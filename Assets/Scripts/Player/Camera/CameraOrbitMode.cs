using UnityEngine;

/// Переключатель режима камеры на CameraContainer.
/// Включено — работает авто-орбита из Assets/CameraOrbit, ручное вращение мышью выключено.
/// Выключено — ручное вращение мышью (BazeCharacterRotation), авто-орбита выключена.
/// Оба режима пишут один и тот же Transform камеры в LateUpdate, поэтому активен ровно один.
[DisallowMultipleComponent]
public class CameraOrbitMode : MonoBehaviour
{
    [Tooltip("Включено — авто-орбита ассета; выключено — ручное вращение мышью.")]
    [SerializeField] private bool autoOrbit = true;

    [Tooltip("Компонент камеры Game Creator (MainCamera) — авто-орбита.")]
    [SerializeField] private Behaviour orbitCamera;

    [Tooltip("Компонент ручного вращения мышью (BazeCharacterRotation).")]
    [SerializeField] private Behaviour manualRotation;

    public bool AutoOrbit
    {
        get => autoOrbit;
        set { autoOrbit = value; Apply(); }
    }

    private void OnEnable() => Apply();

    private void OnValidate() => Apply();

    private void Apply()
    {
        SetEnabled(orbitCamera, autoOrbit);
        SetEnabled(manualRotation, !autoOrbit);
    }

    private static void SetEnabled(Behaviour target, bool value)
    {
        if (target != null && target.enabled != value) target.enabled = value;
    }
}
