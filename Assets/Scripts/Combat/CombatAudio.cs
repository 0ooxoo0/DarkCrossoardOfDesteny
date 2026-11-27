using UnityEngine;

/// <summary>
/// Боевые звуки одним компонентом: замах, попадание, блок, смерть и каст.
/// Работает без префабов и без сцены: AudioSource создаётся сам, клипы
/// подставляются в инспекторе. Живёт на игроке; удар врага по игроку
/// озвучивается теми же клипами, потому что событие боя одно на всех.
/// </summary>
[DisallowMultipleComponent]
public class CombatAudio : MonoBehaviour
{
    [Header("Клипы")]
    [Tooltip("Свист замаха: играет в начале удара игрока.")]
    [SerializeField] private AudioClip swingClip;
    [Tooltip("Попадание по живой цели.")]
    [SerializeField] private AudioClip hitClip;
    [Tooltip("Успешный блок.")]
    [SerializeField] private AudioClip blockClip;
    [Tooltip("Смерть цели: добивание врага.")]
    [SerializeField] private AudioClip deathClip;
    [Tooltip("Каст магии.")]
    [SerializeField] private AudioClip castClip;

    [Header("Громкость")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
    [Tooltip("Минимальный интервал между звуками попадания: спам ударов не должен трещать.")]
    [SerializeField, Min(0f)] private float impactCooldown = 0.04f;

    [Header("Ссылки")]
    [Tooltip("Источник звука. Пусто — создаётся на этом объекте.")]
    [SerializeField] private AudioSource audioSource;

    private float lastImpactTime = -10f;
    private bool initialized;

    /// <summary>Замах игрока: вызывается <see cref="PlayerController"/> на старте атаки.</summary>
    public void PlaySwing()
    {
        Play(swingClip);
    }

    /// <summary>Каст магии: вызывается <see cref="AbilitySystem"/> при запуске способности.</summary>
    public void PlayCast()
    {
        Play(castClip);
    }

    /// <summary>
    /// Идемпотентная инициализация: в EditMode Unity не вызывает <c>Awake</c>,
    /// поэтому тесты входят этим же путём, что и игра.
    /// </summary>
    internal void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = volume;
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        CombatEvents.Impact += HandleImpact;
    }

    private void OnDisable()
    {
        CombatEvents.Impact -= HandleImpact;
    }

    /// <summary>Звук по факту попадания: блок, смерть и обычный удар различаются по виду.</summary>
    private void HandleImpact(CombatImpact impact)
    {
        if (impact.Kind == ImpactKind.Blocked)
        {
            Play(blockClip);
            return;
        }

        if (Time.unscaledTime - lastImpactTime < impactCooldown) return;
        lastImpactTime = Time.unscaledTime;

        Play(impact.Kind == ImpactKind.Killed && deathClip != null ? deathClip : hitClip);
    }

    private void Play(AudioClip clip)
    {
        if (clip == null) return;

        EnsureInitialized();
        if (audioSource == null) return;

        audioSource.PlayOneShot(clip, volume);
    }

    /// <summary>Ссылка на источник звука для тестов: он обязан быть ровно один.</summary>
    internal AudioSource SourceForTests => audioSource;
}
