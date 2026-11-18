using System;
using UnityEngine;

/// <summary>
/// Здоровье игрока: урон, реген, смерть и восстановление.
/// Не знает о контроллере игрока — только события, чтобы смерть и респавн
/// оставались снаружи и не ломали <c>PlayerController</c>.
/// </summary>
[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [Header("Базовые значения")]
    [SerializeField, Min(1f)] private float baseMaxHealth = 100f;
    [SerializeField, Min(0f)] private float regenPerSecond = 0.5f;
    [SerializeField, Min(0f)] private float regenDelayAfterDamage = 5f;

    [Header("Ссылки")]
    [Tooltip("Необязательно: если задан, к максимуму HP прибавляется бонус реалма.")]
    [SerializeField] private CultivationSystem cultivation;

    private float current;
    private float regenDelayRemaining;
    private float invulnerableRemaining;
    private bool dead;

    /// <summary>Текущее здоровье, округлённое для UI.</summary>
    public int Current => Mathf.Clamp(Mathf.RoundToInt(current), 0, Max);

    /// <summary>Максимум здоровья с учётом реалма и множителей дебафов.</summary>
    public int Max
    {
        get
        {
            float realmBonus = cultivation != null ? cultivation.TotalHpBonus : 0f;
            float raw = (baseMaxHealth + realmBonus) * Mathf.Max(0.1f, MaxHealthMultiplier)
                        * Mathf.Max(0.1f, passiveMaxHealthMultiplier);
            return Mathf.Max(1, Mathf.FloorToInt(raw));
        }
    }

    /// <summary>Доля здоровья от 0 до 1 — для полосы HUD.</summary>
    public float Normalized => Max <= 0 ? 0f : Mathf.Clamp01(current / Max);

    /// <summary>Мёртв ли игрок. Сбрасывается только через <see cref="NotifyRespawned"/>.</summary>
    public bool IsDead => dead;

    /// <summary>Множитель получаемого урона: дебафы и защита.</summary>
    public float IncomingDamageMultiplier { get; set; } = 1f;

    /// <summary>Множитель получаемого урона от пассивов (снижение урона).</summary>
    public float PassiveIncomingDamageMultiplier { get; set; } = 1f;

    private float maxHealthMultiplier = 1f;
    private float passiveMaxHealthMultiplier = 1f;

    /// <summary>Множитель максимума HP от пассивных скиллов.</summary>
    public float PassiveMaxHealthMultiplier
    {
        get => passiveMaxHealthMultiplier;
        set
        {
            passiveMaxHealthMultiplier = Mathf.Max(0.1f, value);
            RecalculateMaxHealth();
        }
    }

    /// <summary>Множитель максимума HP: дебафы и пассивные скиллы.</summary>
    public float MaxHealthMultiplier
    {
        get => maxHealthMultiplier;
        set
        {
            maxHealthMultiplier = Mathf.Max(0.1f, value);
            RecalculateMaxHealth();
        }
    }
    public event Action<int, int> OnHealthChanged;
    public event Action<DamageInfo> OnDamaged;
    public event Action OnDied;
    public event Action OnRespawned;

    private void Awake()
    {
        current = Max;
        RaiseHealthChanged();
    }

    private void OnEnable()
    {
        if (cultivation != null)
            cultivation.OnRealmBonusesChanged += HandleRealmBonusesChanged;
    }

    private void OnDisable()
    {
        if (cultivation != null)
            cultivation.OnRealmBonusesChanged -= HandleRealmBonusesChanged;
    }

    private void Update()
    {
        if (dead) return;

        if (invulnerableRemaining > 0f)
            invulnerableRemaining = Mathf.Max(0f, invulnerableRemaining - Time.deltaTime);

        if (regenDelayRemaining > 0f)
        {
            regenDelayRemaining = Mathf.Max(0f, regenDelayRemaining - Time.deltaTime);
            return;
        }

        if (regenPerSecond <= 0f || current >= Max) return;

        current = Mathf.Min(Max, current + regenPerSecond * Time.deltaTime);
        RaiseHealthChanged();
    }

    /// <summary>
    /// Наносит урон. Неуязвимость и смерть игнорируют вызов.
    /// </summary>
    public void TakeDamage(DamageInfo info)
    {
        if (dead || invulnerableRemaining > 0f) return;

        float incoming = Mathf.Max(0f, info.Amount) * Mathf.Max(0f, IncomingDamageMultiplier)
                         * Mathf.Max(0f, PassiveIncomingDamageMultiplier);
        if (incoming <= 0f) return;

        current = Mathf.Max(0f, current - incoming);
        regenDelayRemaining = regenDelayAfterDamage;

        var applied = new DamageInfo(incoming, info.Source, info.HitPoint, info.IsCritical);
        OnDamaged?.Invoke(applied);
        RaiseHealthChanged();

        if (current <= 0f)
            Die();
    }

    /// <summary>Лечит живого игрока. Мёртвого не воскрешает.</summary>
    public void Heal(int amount)
    {
        if (dead || amount <= 0) return;

        current = Mathf.Min(Max, current + amount);
        RaiseHealthChanged();
    }

    /// <summary>
    /// Восстанавливает долю максимума. Меняет HP даже у мёртвого,
    /// но флаг смерти снимает только <see cref="NotifyRespawned"/>.
    /// </summary>
    public void RestorePercent(float percent)
    {
        float clamped = Mathf.Clamp01(percent);
        current = Mathf.Max(0f, Max * clamped);
        RaiseHealthChanged();
    }

    /// <summary>Включает неуязвимость на указанное время.</summary>
    public void SetInvulnerable(float seconds)
    {
        invulnerableRemaining = Mathf.Max(invulnerableRemaining, seconds);
    }

    /// <summary>Пересчитывает максимум после смены множителя и клампит текущее HP.</summary>
    public void RecalculateMaxHealth()
    {
        current = Mathf.Clamp(current, 0f, Max);
        RaiseHealthChanged();
    }

    /// <summary>Завершает респавн: снимает флаг смерти и сообщает подписчикам.</summary>
    public void NotifyRespawned()
    {
        dead = false;
        if (current <= 0f)
            current = 1f;

        RaiseHealthChanged();
        OnRespawned?.Invoke();
    }

    private void Die()
    {
        if (dead) return;

        dead = true;
        current = 0f;
        RaiseHealthChanged();
        OnDied?.Invoke();
    }

    private void HandleRealmBonusesChanged(int qiBonus, int hpBonus, float regenBonus)
    {
        RecalculateMaxHealth();
    }

    private void RaiseHealthChanged()
    {
        OnHealthChanged?.Invoke(Current, Max);
    }
}
