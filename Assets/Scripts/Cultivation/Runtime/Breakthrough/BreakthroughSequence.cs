using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Прорыв: 3 секунды прогресса + бросок шанса (база реалма + бонус Прозрения).
/// Мини-игра (QTE) сюда не входит — отдельный скрипт по запросу.
/// </summary>
[DisallowMultipleComponent]
public class BreakthroughSequence : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private CultivationSystem cultivation;

    [Header("Последовательность")]
    [SerializeField, Min(0.1f)] private float duration = 3f;
    [SerializeField] private bool canBeCancelled = true;
    [Tooltip("Прерывать прорыв при получении урона (через IMeditationInterrupter).")]
    [SerializeField] private bool cancelOnDamage = false;

    [Header("VFX (опционально)")]
    [Tooltip("Префаб столба света. Пусто = процедурный плейсхолдер.")]
    [SerializeField] private GameObject breakthroughVfxPrefab;

    private readonly List<IMeditationInterrupter> interrupters = new List<IMeditationInterrupter>();
    private Coroutine routine;

    public event Action OnBreakthroughStarted;
    public event Action<float> OnBreakthroughProgress;
    public event Action<BreakthroughResult, string> OnBreakthroughResult;

    public bool IsRunning { get; private set; }
    public float Progress { get; private set; }
    public float LastChance { get; private set; }
    public bool CanBeCancelled => canBeCancelled;
    public CultivationSystem Cultivation => cultivation;

    private void OnEnable()
    {
        CollectInterrupters();
        for (int i = 0; i < interrupters.Count; i++)
            interrupters[i].OnInterrupted += HandleInterrupted;
    }

    private void OnDisable()
    {
        for (int i = 0; i < interrupters.Count; i++)
        {
            if (interrupters[i] != null)
                interrupters[i].OnInterrupted -= HandleInterrupted;
        }
        interrupters.Clear();
    }

    public void Initialize(CultivationSystem system, float sequenceDuration)
    {
        cultivation = system;
        duration = Mathf.Max(0.1f, sequenceDuration);
    }

    public bool StartBreakthrough()
    {
        if (IsRunning) return false;

        if (cultivation == null)
        {
            Emit(BreakthroughResult.NotReady, "Нет CultivationSystem на игроке");
            return false;
        }

        if (!cultivation.CanAttemptBreakthrough(out string reason))
        {
            Emit(BreakthroughResult.NotReady, reason);
            return false;
        }

        var meditation = GetComponent<MeditationController>();
        if (meditation != null && meditation.IsMeditating)
            meditation.Interrupt();

        LastChance = cultivation.ComputeSuccessChance();
        Progress = 0f;
        IsRunning = true;
        SpawnVfx();
        OnBreakthroughStarted?.Invoke();
        routine = StartCoroutine(RunSequence());
        return true;
    }

    public void CancelBreakthrough()
    {
        if (!IsRunning || !canBeCancelled) return;

        if (routine != null) StopCoroutine(routine);
        routine = null;
        IsRunning = false;
        Progress = 0f;
        Emit(BreakthroughResult.NotReady, "Прорыв прерван");
    }

    private IEnumerator RunSequence()
    {
        float elapsed = 0f;
        while (elapsed < duration && IsRunning)
        {
            elapsed += Time.deltaTime;
            Progress = Mathf.Clamp01(elapsed / duration);
            OnBreakthroughProgress?.Invoke(Progress);
            yield return null;
        }

        if (!IsRunning) yield break;

        Progress = 1f;
        OnBreakthroughProgress?.Invoke(1f);
        IsRunning = false;
        routine = null;

        float chance = cultivation.ComputeSuccessChance();
        BreakthroughResult result = cultivation.TryBreakthrough();
        string message = BuildMessage(result, chance);
        OnBreakthroughResult?.Invoke(result, message);
    }

    private string BuildMessage(BreakthroughResult result, float chance)
    {
        int chancePercent = Mathf.RoundToInt(chance * 100f);

        switch (result)
        {
            case BreakthroughResult.Success:
                return "Прорыв успешен! " + cultivation.GetProgressLabel();
            case BreakthroughResult.Failed:
                return "Прорыв сорвался. Шанс был " + chancePercent + "%. Прозрение поможет в следующий раз.";
            default:
                cultivation.CanAttemptBreakthrough(out string reason);
                return string.IsNullOrEmpty(reason) ? "Прорыв невозможен" : reason;
        }
    }

    private void SpawnVfx()
    {
        Color color = ResolveDaoColor();

        if (breakthroughVfxPrefab != null)
        {
            Instantiate(breakthroughVfxPrefab, transform.position, Quaternion.identity);
            return;
        }

        CultivationVfxFactory.CreateBreakthroughPillar(transform, color);
        CultivationVfxFactory.PlayFlash(transform, color);
    }

    private Color ResolveDaoColor()
    {
        var dao = GetComponent<DaoSystem>();
        if (dao != null && dao.ActiveDao.HasValue)
        {
            var path = dao.GetDaoPath(dao.ActiveDao.Value);
            if (path != null) return path.Color;
            return DaoTypeInfo.GetColor(dao.ActiveDao.Value);
        }
        return DaoTypeInfo.GetColor(DaoType.Sword);
    }

    private void Emit(BreakthroughResult result, string message)
    {
        OnBreakthroughResult?.Invoke(result, message);
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

    private void HandleInterrupted()
    {
        if (!cancelOnDamage) return;
        CancelBreakthrough();
    }
}
