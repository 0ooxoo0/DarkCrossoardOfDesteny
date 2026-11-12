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

    [Header("Анимация (необязательно)")]
    [Tooltip("Имя триггера Animator. Пусто = Animator не трогаем: состояния Meditate в проекте нет.")]
    [SerializeField] private string animatorTriggerName = "";

    [Header("VFX (опционально)")]
    [Tooltip("Префаб частиц медитации. Пусто = процедурные частицы из CultivationVfxFactory.")]
    [SerializeField] private ParticleSystem meditationVfxPrefab;

    private readonly List<MeditationZone> zones = new List<MeditationZone>();
    private readonly List<IMeditationInterrupter> interrupters = new List<IMeditationInterrupter>();

    private Coroutine tickRoutine;
    private ParticleSystem meditationVfx;
    private float savedRegenPerSecond;
    private bool savedRegenerate;
    private float restoreAccumulator;
    private float comprehensionAccumulator;

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
    }

    private void OnDisable()
    {
        SubscribeInterrupters(false);
        interrupters.Clear();
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

    private void Update()
    {
        if (!IsMeditating) return;

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

        IsMeditating = true;
        restoreAccumulator = 0f;
        comprehensionAccumulator = 0f;

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

    /// <summary>Прервать медитацию (урон, кнопка «Прервать», внешние системы).</summary>
    public void Interrupt()
    {
        if (!IsMeditating) return;

        ExitMeditation();
        OnMeditationInterrupted?.Invoke();
    }

    public void ToggleMeditation()
    {
        if (IsMeditating) ExitMeditation();
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
                cultivation.AddExperienceQi(experience);

            if (dao != null && dao.ActiveDao.HasValue)
                dao.AddPoints(dao.ActiveDao.Value, points);

            if (cultivation != null && comprehensionPerMinute > 0f)
            {
                comprehensionAccumulator += comprehensionPerMinute * (tickInterval / 60f);
                int comprehension = Mathf.FloorToInt(comprehensionAccumulator);
                if (comprehension > 0)
                {
                    comprehensionAccumulator -= comprehension;
                    cultivation.AddComprehension(comprehension);
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
        }
        else
        {
            meditationVfx = CultivationVfxFactory.CreateMeditationMotes(transform, ResolveDaoColor());
        }

        meditationVfx.Play();
    }

    private void StopMeditationVfx()
    {
        if (meditationVfx == null) return;
        Destroy(meditationVfx.gameObject);
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
        Interrupt();
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
