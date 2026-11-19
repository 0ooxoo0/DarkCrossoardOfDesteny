using UnityEngine;

/// <summary>
/// Ссылки на префабы эффектов культивации. Один ассет на игру: код не знает
/// конкретных ассетов, а сетап подставляет префабы из Assets/Magic.
/// Хранятся целые префабы: часть эффектов собрана из мешей, а не из частиц,
/// поэтому тип — <see cref="GameObject"/>, а не <see cref="ParticleSystem"/>.
/// </summary>
[CreateAssetMenu(fileName = "CultivationVfxConfig", menuName = "Cultivation/Vfx Config")]
public class CultivationVfxConfig : ScriptableObject
{
    [Header("Аура и медитация")]
    [Tooltip("Аура Дао вокруг игрока. Частицы берутся из префаба автоматически.")]
    [SerializeField] private GameObject auraPrefab;
    [Tooltip("Петля эффектов вокруг игрока во время медитации.")]
    [SerializeField] private GameObject meditationLoopPrefab;

    [Header("Прорыв")]
    [SerializeField] private GameObject breakthroughStartPrefab;
    [SerializeField] private GameObject breakthroughSuccessPrefab;
    [SerializeField] private GameObject breakthroughFailPrefab;

    [Header("Последствия")]
    [SerializeField] private GameObject qiSurgePrefab;
    [SerializeField] private GameObject qiDeviationPrefab;
    [Tooltip("Самый тяжёлый эффект: обрыв культивации.")]
    [SerializeField] private GameObject severingPrefab;

    [Header("Смерть")]
    [SerializeField] private GameObject deathPrefab;

    [Header("Время жизни")]
    [SerializeField, Min(0.5f)] private float effectLifetime = 4f;

    public GameObject AuraPrefab => auraPrefab;
    public GameObject MeditationLoopPrefab => meditationLoopPrefab;
    public GameObject BreakthroughStartPrefab => breakthroughStartPrefab;
    public GameObject BreakthroughSuccessPrefab => breakthroughSuccessPrefab;
    public GameObject BreakthroughFailPrefab => breakthroughFailPrefab;
    public GameObject QiSurgePrefab => qiSurgePrefab;
    public GameObject QiDeviationPrefab => qiDeviationPrefab;
    public GameObject SeveringPrefab => severingPrefab;
    public GameObject DeathPrefab => deathPrefab;
    public float EffectLifetime => Mathf.Max(0.5f, effectLifetime);
}
