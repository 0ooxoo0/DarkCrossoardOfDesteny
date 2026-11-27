using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Медитация: тик опыта Ци и очков Дао, ускоренный реген Ци, прерывание уроном.
/// Animator трогается только если в инспекторе задан существующий триггер.
/// </summary>
[DisallowMultipleComponent]
public class MeditationController : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private CultivationSystem cultivation;
    [SerializeField] private DaoSystem dao;
    [SerializeField] private QiComponent qi;

    [Header("Тик медитации")]
    [SerializeField, Min(0.1f)] private float tickInterval = 1f;
    [SerializeField, Min(0)] private int experienceQiPerTick = 15;
    [SerializeField, Min(0)] private int daoPointsPerTick = 2;
    [SerializeField, Min(0f)] private float qiRestorePerSecond = 6f;
    [SerializeField, Min(0f)] private float comprehensionPerMinute = 1f;

    [Header("Прерывание")]
    [SerializeField] private bool interruptOnDamage = true;

    [Header("Риск и последствия")]
    [SerializeField] private MeditationRiskConfig riskConfig;
    [SerializeField] private CultivationDebuffController debuffs;
    [SerializeField] private MeditationConsequenceService consequences;
    [SerializeField] private PlayerHealth playerHealth;
    [Tooltip("Радиус поиска угрозы, если MeditationRiskConfig не назначен.")]
    [SerializeField, Min(0f)] private float threatRadiusFallback = 8f;
    [SerializeField, Min(0.1f)] private float eyeHeight = 1.7f;
    [Tooltip("Слои, перекрывающие обзор врагу. Пусто — линия видимости не проверяется.")]
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Header("Анимация (необязательно)")]
    [Tooltip("Имя триггера Animator. Пусто = Animator не трогаем: состояния Meditate в проекте нет.")]
    [SerializeField] private string animatorTriggerName = "";

    [Header("VFX (опционально)")]
    [Tooltip("Префаб эффекта медитации. Пусто = процедурные частицы из CultivationVfxFactory.")]
    [SerializeField] private GameObject meditationVfxPrefab;

    private readonly List<MeditationZone> zones = new List<MeditationZone>();
    private readonly List<IMeditationInterrupter> interrupters = new List<IMeditationInterrupter>();

    private Coroutine tickRoutine;
    private GameObject meditationVfx;
    private float savedRegenPerSecond;
    private bool savedRegenerate;
    private float restoreAccumulator;
    private float comprehensionAccumulator;
    private float threatTimer;

    public event Action OnMeditationStarted;
    public event Action OnMeditationStopped;
    /// <summary>(получено опыта Ци, получено очков Дао, множитель зоны)</summary>
    public event Action<int, int, float> OnMeditationTick;
    public event Action OnMeditationInterrupted;

    public bool IsMeditating { get; private set; }
    public CultivationSystem Cultivation => cultivation;
    public DaoSystem Dao => dao;
    public QiComponent Qi => qi;
    public bool InterruptOnDamage { get => interruptOnDamage; set => interruptOnDamage = value; }

    /// <summary>Снимок текущей медитации: сколько накоплено до прерывания.</summary>
    public MeditationSession Session { get; } = new MeditationSession();

    /// <summary>Игрок стоит в зоне медитации (алтарь/пещера).</summary>
    public bool IsInSafeZone => zones.Count > 0;

    public float CurrentMultiplier
    {
        get
        {
            float multiplier = 1f;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] == null) continue;
                if (zones[i].BonusMultiplier > multiplier)
                    multiplier = zones[i].BonusMultiplier;
            }
            return multiplier;
        }
    }

    public float ExperiencePerSecond => tickInterval <= 0f ? 0f : experienceQiPerTick / tickInterval * CurrentMultiplier;
    public float DaoPointsPerSecond => tickInterval <= 0f ? 0f : daoPointsPerTick / tickInterval * CurrentMultiplier;
    public float QiRestorePerSecond => qiRestorePerSecond * CurrentMultiplier;

    private void OnEnable()
    {
        CollectInterrupters();
        SubscribeInterrupters(true);

        if (playerHealth != null)
        {
            playerHealth.OnDamaged += HandlePlayerDamaged;
            playerHealth.OnDied += HandlePlayerDied;
        }
    }

    private void OnDisable()
    {
        SubscribeInterrupters(false);
        interrupters.Clear();

        if (playerHealth != null)
        {
            playerHealth.OnDamaged -= HandlePlayerDamaged;
            playerHealth.OnDied -= HandlePlayerDied;
        }
    }

    public void Initialize(CultivationSystem cultivationSystem, DaoSystem daoSystem, QiComponent qiComponent,
        float tick, int experiencePerTick, int daoPoints, float qiRestore, float comprehension)
    {
        cultivation = cultivationSystem;
        dao = daoSystem;
        qi = qiComponent;
        tickInterval = Mathf.Max(0.1f, tick);
        experienceQiPerTick = Mathf.Max(0, experiencePerTick);
        daoPointsPerTick = Mathf.Max(0, daoPoints);
        qiRestorePerSecond = Mathf.Max(0f, qiRestore);
        comprehensionPerMinute = Mathf.Max(0f, comprehension);
    }

    /// <summary>Префаб петли медитации из конфига VFX; пусто — процедурные частицы.</summary>
    public void SetVfxPrefab(GameObject prefab)
    {
        meditationVfxPrefab = prefab;
    }

    /// <summary>Подключает риск медитации, дебафы и здоровье из bootstrap.</summary>
    public void InitializeRisk(MeditationRiskConfig risk, CultivationDebuffController debuffController,
        MeditationConsequenceService consequenceService, PlayerHealth health)
    {
        riskConfig = risk;
        debuffs = debuffController;
        consequences = consequenceService;

        if (playerHealth == health) return;

        if (playerHealth != null)
        {
            playerHealth.OnDamaged -= HandlePlayerDamaged;
            playerHealth.OnDied -= HandlePlayerDied;
        }

        playerHealth = health;

        // Компонент мог быть уже включён — подписка из OnEnable не повторится.
        if (isActiveAndEnabled && playerHealth != null)
        {
            playerHealth.OnDamaged += HandlePlayerDamaged;
            playerHealth.OnDied += HandlePlayerDied;
        }
    }

    private void Update()
    {
        if (!IsMeditating) return;

        ScanForThreat();

        if (qi == null || qiRestorePerSecond <= 0f) return;

        restoreAccumulator += qiRestorePerSecond * CurrentMultiplier * Time.deltaTime;
        int whole = Mathf.FloorToInt(restoreAccumulator);
        if (whole <= 0) return;

        restoreAccumulator -= whole;
        qi.Restore(whole);
    }

    public bool EnterMeditation()
    {
        if (IsMeditating) return false;
        if (cultivation == null)
        {
            Debug.LogWarning("[Meditation] Нет CultivationSystem — медитация невозможна.");
            return false;
        }

        if (playerHealth != null && playerHealth.IsDead)
            return false;

        if (debuffs != null && debuffs.IsMeditationBlocked)
        {
            Debug.Log("[Meditation] Медитация заблокирована последствиями срыва.");
            return false;
        }

        IsMeditating = true;
        restoreAccumulator = 0f;
        comprehensionAccumulator = 0f;
        threatTimer = 0f;
        Session.Start(IsInSafeZone);

        if (qi != null)
        {
            savedRegenPerSecond = qi.RegenPerSecond;
            savedRegenerate = qi.Regenerate;
            qi.Regenerate = false;
        }

        StartMeditationVfx();
        ApplyAnimatorTrigger(true);
        tickRoutine = StartCoroutine(TickRoutine());
        OnMeditationStarted?.Invoke();
        return true;
    }

    /// <summary>Мягко завершить медитацию без последствий: смерть, прорыв, каст.</summary>
    public void ExitMeditation()
    {
        if (!IsMeditating) return;

        IsMeditating = false;
        if (tickRoutine != null)
        {
            StopCoroutine(tickRoutine);
            tickRoutine = null;
        }

        if (qi != null)
        {
            qi.RegenPerSecond = savedRegenPerSecond;
            qi.Regenerate = savedRegenerate;
        }

        StopMeditationVfx();
        ApplyAnimatorTrigger(false);
        OnMeditationStopped?.Invoke();
    }

    /// <summary>Прервать медитацию по урону (совместимость со старым API).</summary>
    public void Interrupt()
    {
        Interrupt(MeditationInterruptionReason.Damage);
    }

    /// <summary>
    /// Прервать медитацию с последствиями. <see cref="MeditationInterruptionReason.Death"/>
    /// и мягкий выход через <see cref="ExitMeditation"/> последствий не дают.
    /// </summary>
    public void Interrupt(MeditationInterruptionReason reason)
    {
        if (!IsMeditating) return;

        bool wasSafe = Session.WasSafe;
        ExitMeditation();

        if (consequences != null)
            consequences.Apply(reason, Session, wasSafe);

        Session.Reset();
        OnMeditationInterrupted?.Invoke();
    }

    /// <summary>Завершает медитацию перед смертью: без дебафа, смерть сама по себе штраф.</summary>
    public void StopMeditationForDeath()
    {
        if (!IsMeditating) return;

        ExitMeditation();
        Session.Reset();
    }

    /// <summary>Совместимый вход: то же, что <see cref="EnterMeditation"/>.</summary>
    public bool StartMeditation() => EnterMeditation();

    public void ToggleMeditation()
    {
        if (IsMeditating) Interrupt(MeditationInterruptionReason.Manual);
        else EnterMeditation();
    }

    private IEnumerator TickRoutine()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.1f, tickInterval));

        while (IsMeditating)
        {
            yield return wait;
            if (!IsMeditating) yield break;

            float multiplier = CurrentMultiplier;
            int experience = Mathf.RoundToInt(experienceQiPerTick * multiplier);
            int points = Mathf.RoundToInt(daoPointsPerTick * multiplier);

            if (cultivation != null)
            {
                cultivation.AddExperienceQi(experience);
                Session.AddExperience(experience);
            }

            if (dao != null && dao.ActiveDao.HasValue)
            {
                dao.AddPoints(dao.ActiveDao.Value, points);
                Session.AddDaoPoints(points);
            }

            if (cultivation != null && comprehensionPerMinute > 0f)
            {
                comprehensionAccumulator += comprehensionPerMinute * (tickInterval / 60f);
                int comprehension = Mathf.FloorToInt(comprehensionAccumulator);
                if (comprehension > 0)
                {
                    comprehensionAccumulator -= comprehension;
                    cultivation.AddComprehension(comprehension);
                    Session.AddComprehension(comprehension);
                }
            }

            OnMeditationTick?.Invoke(experience, points, multiplier);
        }
    }

    private void StartMeditationVfx()
    {
        if (meditationVfx != null) return;

        if (meditationVfxPrefab != null)
        {
            meditationVfx = Instantiate(meditationVfxPrefab, transform);
            meditationVfx.transform.localPosition = Vector3.zero;
            PlayParticles(meditationVfx);
            return;
        }

        ParticleSystem motes = CultivationVfxFactory.CreateMeditationMotes(transform, ResolveDaoColor());
        meditationVfx = motes.gameObject;
        motes.Play();
    }

    /// <summary>Запускает все частицы префаба: у мешевых эффектов их может не быть.</summary>
    private static void PlayParticles(GameObject root)
    {
        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
            systems[i].Play(true);
    }

    private void StopMeditationVfx()
    {
        if (meditationVfx == null) return;
        Destroy(meditationVfx);
        meditationVfx = null;
    }

    private Color ResolveDaoColor()
    {
        if (dao != null && dao.ActiveDao.HasValue)
        {
            var path = dao.GetDaoPath(dao.ActiveDao.Value);
            if (path != null) return path.Color;
            return DaoTypeInfo.GetColor(dao.ActiveDao.Value);
        }
        return DaoTypeInfo.GetColor(DaoType.Sword);
    }

    public void RegisterZone(MeditationZone zone)
    {
        if (zone == null || zones.Contains(zone)) return;
        zones.Add(zone);
    }

    public void UnregisterZone(MeditationZone zone)
    {
        if (zone == null) return;
        zones.Remove(zone);
    }

    private void CollectInterrupters()
    {
        interrupters.Clear();
        var behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IMeditationInterrupter interrupter && !ReferenceEquals(behaviours[i], this))
                interrupters.Add(interrupter);
        }
    }

    private void SubscribeInterrupters(bool subscribe)
    {
        for (int i = 0; i < interrupters.Count; i++)
        {
            if (interrupters[i] == null) continue;
            if (subscribe) interrupters[i].OnInterrupted += HandleInterrupted;
            else interrupters[i].OnInterrupted -= HandleInterrupted;
        }
    }

    private void HandleInterrupted()
    {
        if (!interruptOnDamage) return;
        Interrupt(MeditationInterruptionReason.Damage);
    }

    private void HandlePlayerDamaged(DamageInfo info)
    {
        if (!interruptOnDamage) return;

        // Смертельный удар обрабатывает HandlePlayerDied: дебаф за смерть не нужен.
        if (playerHealth != null && playerHealth.Current <= 0) return;

        Interrupt(MeditationInterruptionReason.Damage);
    }

    private void HandlePlayerDied()
    {
        StopMeditationForDeath();
    }

    /// <summary>Раз в интервал ищет агрессивного врага рядом с линией видимости.</summary>
    private void ScanForThreat()
    {
        threatTimer -= Time.deltaTime;
        if (threatTimer > 0f) return;

        threatTimer = riskConfig != null ? riskConfig.ThreatScanInterval : 0.25f;

        float radius = riskConfig != null ? riskConfig.ThreatRadius : threatRadiusFallback;
        if (radius <= 0f) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;

            // Боевой враг важнее старого: без него медитацию прерывал только legacy-EnemyAI.
            EnemyCombatAI combat = hits[i].GetComponentInParent<EnemyCombatAI>();
            if (combat != null)
            {
                if (combat.IsDead || combat.CurrentState == EnemyCombatAI.State.Idle) continue;
                if (!HasLineOfSight(combat.transform)) continue;

                Interrupt(MeditationInterruptionReason.Threat);
                return;
            }

#pragma warning disable CS0618
            EnemyAI enemy = hits[i].GetComponentInParent<EnemyAI>();
#pragma warning restore CS0618
            if (enemy == null || !enemy.IsAggroed) continue;
            if (!HasLineOfSight(enemy.transform)) continue;

            Interrupt(MeditationInterruptionReason.Threat);
            return;
        }
    }

    private bool HasLineOfSight(Transform target)
    {
        if (target == null) return false;
        if (obstacleMask.value == 0) return true;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 aim = target.position + Vector3.up * (eyeHeight * 0.5f);

        if (!Physics.Linecast(eye, aim, out RaycastHit hit, obstacleMask, QueryTriggerInteraction.Ignore))
            return true;

#pragma warning disable CS0618
        // Линия может задеть самого врага — это не препятствие.
        return hit.collider != null && hit.collider.GetComponentInParent<EnemyAI>() != null;
#pragma warning restore CS0618
    }

    private void ApplyAnimatorTrigger(bool entering)
    {
        if (string.IsNullOrEmpty(animatorTriggerName)) return;

        var animator = GetComponentInChildren<Animator>();
        if (animator == null) return;

        var parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name != animatorTriggerName) continue;
            if (parameters[i].type != AnimatorControllerParameterType.Trigger) continue;

            if (entering) animator.SetTrigger(animatorTriggerName);
            else animator.ResetTrigger(animatorTriggerName);
            return;
        }
    }
}
