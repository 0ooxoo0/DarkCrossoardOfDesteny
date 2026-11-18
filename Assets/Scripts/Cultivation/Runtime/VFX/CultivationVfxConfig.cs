using UnityEngine;

/// <summary>
/// Ссылки на префабы эффектов культивации. Один ассет на игру: код не знает
/// конкретных ассетов, а сетап подставляет префабы из Assets/Magic.
/// </summary>
[CreateAssetMenu(fileName = "CultivationVfxConfig", menuName = "Cultivation/Vfx Config")]
public class CultivationVfxConfig : ScriptableObject
{
    [Header("Аура и медитация")]
    [SerializeField] private ParticleSystem auraPrefab;
    [Tooltip("Петля частиц вокруг игрока во время медитации.")]
    [SerializeField] private ParticleSystem meditationLoopPrefab;

    [Header("Прорыв")]
    [SerializeField] private ParticleSystem breakthroughStartPrefab;
    [SerializeField] private ParticleSystem breakthroughSuccessPrefab;
    [SerializeField] private ParticleSystem breakthroughFailPrefab;

    [Header("Последствия")]
    [SerializeField] private ParticleSystem qiSurgePrefab;
    [SerializeField] private ParticleSystem qiDeviationPrefab;
    [Tooltip("Самый тяжёлый эффект: обрыв культивации.")]
    [SerializeField] private ParticleSystem severingPrefab;

    [Header("Смерть")]
    [SerializeField] private ParticleSystem deathPrefab;

    [Header("Время жизни")]
    [SerializeField, Min(0.5f)] private float effectLifetime = 4f;

    public ParticleSystem AuraPrefab => auraPrefab;
    public ParticleSystem MeditationLoopPrefab => meditationLoopPrefab;
    public ParticleSystem BreakthroughStartPrefab => breakthroughStartPrefab;
    public ParticleSystem BreakthroughSuccessPrefab => breakthroughSuccessPrefab;
    public ParticleSystem BreakthroughFailPrefab => breakthroughFailPrefab;
    public ParticleSystem QiSurgePrefab => qiSurgePrefab;
    public ParticleSystem QiDeviationPrefab => qiDeviationPrefab;
    public ParticleSystem SeveringPrefab => severingPrefab;
    public ParticleSystem DeathPrefab => deathPrefab;
    public float EffectLifetime => Mathf.Max(0.5f, effectLifetime);
}
