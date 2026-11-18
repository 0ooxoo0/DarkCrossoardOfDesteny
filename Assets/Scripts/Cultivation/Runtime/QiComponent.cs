using System;
using UnityEngine;

/// <summary>
/// Ци — ресурс культивации. Компонент не знает ни про HP, ни про AbilitySystem,
/// ни про инвентарь, ни про UI: он только хранит значение и раздаёт события.
/// </summary>
[DisallowMultipleComponent]
public class QiComponent : MonoBehaviour
{
    [Header("Ёмкость")]
    [SerializeField, Min(0f)] private float maxQi = 100f;
    [Tooltip("-1 = стартовать с полной шкалой.")]
    [SerializeField] private float startQi = -1f;

    [Header("Пассивная регенерация")]
    [SerializeField] private bool regenerate = true;
    [SerializeField, Min(0f)] private float regenPerSecond = 1f;

    private float currentQi;
    private int lastCurrent;
    private int lastMax;
    private bool depletedRaised;
    private float maxQiMultiplier = 1f;
    private float regenMultiplier = 1f;

    /// <summary>Текущее и максимальное значение (целые) — для UI.</summary>
    public event Action<int, int> OnQiChanged;
    /// <summary>Ци упало до нуля.</summary>
    public event Action OnQiDepleted;
    /// <summary>Успешно потраченное количество.</summary>
    public event Action<int> OnQiSpent;

    public float MaxQi => maxQi * Mathf.Max(0.1f, maxQiMultiplier);
    public float CurrentQi => currentQi;
    public int Current => Mathf.FloorToInt(currentQi + 0.0001f);
    public int Max => Mathf.Max(0, Mathf.FloorToInt(MaxQi + 0.0001f));
    public float Normalized => MaxQi <= 0.0001f ? 0f : Mathf.Clamp01(currentQi / MaxQi);
    public bool IsDepleted => currentQi <= 0.0001f;

    /// <summary>Множитель максимума Ци от пассивных скиллов.</summary>
    public float MaxQiMultiplier
    {
        get => maxQiMultiplier;
        set
        {
            maxQiMultiplier = Mathf.Max(0.1f, value);
            currentQi = Mathf.Min(currentQi, MaxQi);
            ForceNotify();
        }
    }

    /// <summary>Множитель пассивной регенерации Ци.</summary>
    public float RegenMultiplier
    {
        get => regenMultiplier;
        set => regenMultiplier = Mathf.Max(0f, value);
    }

    public float RegenPerSecond
    {
        get => regenPerSecond;
        set => regenPerSecond = Mathf.Max(0f, value);
    }

    public bool Regenerate
    {
        get => regenerate;
        set => regenerate = value;
    }

    private void Awake()
    {
        ResetToStart();
    }

    private void Update()
    {
        if (!regenerate || regenPerSecond <= 0f || currentQi >= MaxQi)
            return;

        currentQi = Mathf.Min(MaxQi, currentQi + regenPerSecond * regenMultiplier * Time.deltaTime);
        RaiseIfChanged();
    }

    /// <summary>Полная перенастройка (используется bootstrap'ом и сохранениями).</summary>
    public void Configure(float newMaxQi, float newRegenPerSecond, bool refill = true)
    {
        maxQi = Mathf.Max(0f, newMaxQi);
        regenPerSecond = Mathf.Max(0f, newRegenPerSecond);
        if (refill) currentQi = MaxQi;
        else currentQi = Mathf.Clamp(currentQi, 0f, MaxQi);
        ForceNotify();
    }

    /// <summary>Стартовое значение (-1 = полная шкала).</summary>
    public void ConfigureStartValue(float value)
    {
        startQi = value;
        ResetToStart();
        ForceNotify();
    }

    public void SetMaxQi(float value, bool keepPercent = true)
    {
        float percent = Normalized;
        maxQi = Mathf.Max(0f, value);
        currentQi = keepPercent ? MaxQi * percent : Mathf.Clamp(currentQi, 0f, MaxQi);
        ForceNotify();
    }

    /// <summary>Потратить Ци. Возвращает false, если ресурса не хватает (значение не меняется).</summary>
    public bool Spend(int amount)
    {
        if (amount <= 0) return false;
        if (currentQi + 0.0001f < amount) return false;

        currentQi = Mathf.Max(0f, currentQi - amount);
        OnQiSpent?.Invoke(amount);
        RaiseIfChanged();
        return true;
    }

    /// <summary>Восстановить Ци. Возвращает фактически восстановленное количество.</summary>
    public int Restore(int amount)
    {
        if (amount <= 0) return 0;

        float before = currentQi;
        currentQi = Mathf.Min(MaxQi, currentQi + amount);
        float delta = currentQi - before;
        if (delta > 0.0001f) RaiseIfChanged();
        return Mathf.FloorToInt(delta + 0.0001f);
    }

    public void RestoreFull()
    {
        if (currentQi >= MaxQi) return;
        currentQi = MaxQi;
        RaiseIfChanged();
    }

    public bool HasQi(int amount) => amount <= 0 || currentQi + 0.0001f >= amount;

    /// <summary>Жёстко задать значение (загрузка сохранения).</summary>
    public void SetCurrent(float value)
    {
        currentQi = Mathf.Clamp(value, 0f, MaxQi);
        ForceNotify();
    }

    private void ResetToStart()
    {
        currentQi = startQi >= 0f ? Mathf.Clamp(startQi, 0f, MaxQi) : MaxQi;
        lastCurrent = Current;
        lastMax = Max;
        depletedRaised = IsDepleted;
    }

    private void ForceNotify()
    {
        lastCurrent = int.MinValue;
        lastMax = int.MinValue;
        RaiseIfChanged();
    }

    private void RaiseIfChanged()
    {
        int current = Current;
        int max = Max;
        if (current == lastCurrent && max == lastMax) return;

        lastCurrent = current;
        lastMax = max;
        OnQiChanged?.Invoke(current, max);

        if (current <= 0)
        {
            if (!depletedRaised)
            {
                depletedRaised = true;
                OnQiDepleted?.Invoke();
            }
        }
        else
        {
            depletedRaised = false;
        }
    }
}
