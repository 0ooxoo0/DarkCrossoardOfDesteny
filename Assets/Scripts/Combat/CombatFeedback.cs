using System.Collections;
using UnityEngine;

/// <summary>
/// Боевой фидбек удара: хит-стоп, тряска камеры, impact-VFX и цифры урона.
/// Подписывается на боевую шину и не знает, кто именно ударил.
/// </summary>
[DisallowMultipleComponent]
public class CombatFeedback : MonoBehaviour
{
    [Header("Hit stop")]
    [Tooltip("Замедление времени на попадании: короткая пауза делает удар весомым.")]
    [SerializeField, Min(0f)] private float hitStopDuration = 0.055f;
    [SerializeField, Range(0.01f, 1f)] private float hitStopTimeScale = 0.05f;
    [SerializeField, Min(0f)] private float hitStopCooldown = 0.08f;
    [Tooltip("Замедлять время, когда урон получает игрок. Выключено: рывок мешает увороту.")]
    [SerializeField] private bool hitStopOnPlayerDamage = false;

    [Header("Impact VFX")]
    [Tooltip("Префаб искр/вспышки в точке удара.")]
    [SerializeField] private GameObject impactEffect;
    [Tooltip("Префаб эффекта добивания.")]
    [SerializeField] private GameObject killEffect;
    [SerializeField, Min(0.1f)] private float effectLifetime = 2f;

    [Header("Camera shake")]
    [Tooltip("Камера трясётся на попадании. Пусто — ищется Camera.main в рантайме.")]
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField, Min(0f)] private float hitShake = 1f;
    [SerializeField, Min(0f)] private float killShake = 1.8f;

    [Header("Damage numbers")]
    [SerializeField] private bool showDamageNumbers = true;
    [Tooltip("Урон, нанесённый игроком: врагу, дереву или животному.")]
    [SerializeField] private Color hitColor = new Color(1f, 0.85f, 0.2f, 1f);
    [Tooltip("Удар по игроку: красная цифра, чтобы «меня попали» отличалось от «я попал».")]
    [SerializeField] private Color playerHitColor = new Color(1f, 0.25f, 0.2f, 1f);
    [SerializeField] private Color criticalColor = new Color(1f, 0.35f, 0.15f, 1f);
    [SerializeField] private Color blockedColor = new Color(0.6f, 0.8f, 1f, 1f);

    private float lastHitStopTime = -10f;
    private float hitStopRemaining;

    private bool subscribed;

    // Удар приходит из OnTriggerStay: Instantiate, Destroy и работа с пулом
    // цифр внутри физического шага роняли редактор нативным падением.
    private readonly System.Collections.Generic.List<CombatImpact> pending =
        new System.Collections.Generic.List<CombatImpact>(8);

    private void OnEnable()
    {
        EnsureInitialized();
    }

    private void OnDisable()
    {
        if (subscribed)
        {
            CombatEvents.Impact -= HandleImpact;
            subscribed = false;
        }

        pending.Clear();
        EndHitStop();
    }

    /// <summary>
    /// Идемпотентная подписка на боевую шину. В EditMode Unity не вызывает
    /// <c>OnEnable</c>, поэтому тесты входят этим же путём, что и игра.
    /// </summary>
    internal void EnsureInitialized()
    {
        if (subscribed) return;
        subscribed = true;

        CombatEvents.Impact += HandleImpact;
    }

    /// <summary>
    /// Хит-стоп тикает сам, а не корутиной: убитая при перезагрузке домена
    /// корутина оставляла Time.timeScale в замедлении навсегда.
    /// </summary>
    private void Update()
    {
        if (hitStopRemaining <= 0f) return;

        hitStopRemaining -= Time.unscaledDeltaTime;
        if (hitStopRemaining > 0f) return;

        EndHitStop();
    }

    private void EndHitStop()
    {
        hitStopRemaining = 0f;

        // Игру могли поставить на паузу во время хит-стопа — паузу не ломаем.
        if (Time.timeScale > 0f) Time.timeScale = 1f;
    }

    /// <summary>Сброс хит-стопа для тестов: кулдаун иначе глушит соседние проверки.</summary>
    internal void ResetHitStopForTests()
    {
        hitStopRemaining = 0f;
        lastHitStopTime = -10f;
        Time.timeScale = 1f;
    }

    /// <summary>Прогон хит-стопа для тестов: в EditMode Update не тикает.</summary>
    internal void TickHitStopForTests(float unscaledDeltaSeconds)
    {
        if (hitStopRemaining <= 0f) return;

        hitStopRemaining -= unscaledDeltaSeconds;
        if (hitStopRemaining <= 0f) EndHitStop();
    }

    /// <summary>Идёт ли замедление прямо сейчас.</summary>
    internal bool IsHitStopActiveForTests => hitStopRemaining > 0f;

    /// <summary>
    /// Кладёт удар в очередь и сразу отдаёт её размер. В EditMode LateUpdate
    /// чистит очередь без спавна, поэтому проверять надо в том же вызове.
    /// </summary>
    internal int QueueImpactForTests(CombatImpact impact)
    {
        HandleImpact(impact);
        int count = pending.Count;
        pending.Clear();
        return count;
    }

    private void HandleImpact(CombatImpact impact)
    {
        bool victimIsPlayer = IsPlayer(impact.Victim);

        if (impact.Kind != ImpactKind.Blocked)
        {
            // Удар по игроку не замедляет время по умолчанию: рывок timeScale
            // на собственном уроне ощущается как лаг и мешает увороту. Тряска остаётся.
            if (!victimIsPlayer || hitStopOnPlayerDamage) PlayHitStop();
            ShakeCamera(impact.Kind == ImpactKind.Killed ? killShake : hitShake);
        }

        // Сам спавн откладывается: сейчас мы внутри физического шага.
        // Проверка режима стоит в LateUpdate: в EditMode спавнить нельзя,
        // но очередь обязана наполняться — иначе это нечем проверить.
        pending.Add(impact);
    }

    /// <summary>Игрок ли цель удара: по нему выбирается цвет цифры и хит-стоп.</summary>
    private static bool IsPlayer(Transform victim)
    {
        return victim != null && victim.GetComponent<PlayerHealth>() != null;
    }

    /// <summary>
    /// Визуал удара создаётся в конце кадра, а не в колбэке физики:
    /// Instantiate тяжёлого VFX-префаба прямо из OnTriggerStay роняет редактор.
    /// </summary>
    private void LateUpdate()
    {
        if (pending.Count == 0) return;

        // Вне игры визуал не спавним: в EditMode нельзя ни Destroy, ни DontDestroyOnLoad.
        if (!Application.isPlaying)
        {
            pending.Clear();
            return;
        }

        for (int i = 0; i < pending.Count; i++)
        {
            CombatImpact impact = pending[i];
            SpawnEffect(impact);
            if (showDamageNumbers) SpawnPopup(impact);
        }

        pending.Clear();
    }

    /// <summary>Короткая остановка времени. Паузу меню не снимает и не ставит.</summary>
    private void PlayHitStop()
    {
        if (hitStopDuration <= 0f || hitStopTimeScale >= 1f) return;
        if (Time.timeScale <= 0f) return;
        if (Time.unscaledTime - lastHitStopTime < hitStopCooldown) return;

        lastHitStopTime = Time.unscaledTime;
        hitStopRemaining = hitStopDuration;
        Time.timeScale = hitStopTimeScale;
    }

    private void ShakeCamera(float strength)
    {
        if (strength <= 0f) return;

        // Ссылку на камеру сцены префаб держать не может: ищем один раз в рантайме.
        if (cameraFollow == null)
        {
            Camera main = Camera.main;
            if (main != null) cameraFollow = main.GetComponent<CameraFollow>();
        }

        if (cameraFollow == null) return;
        cameraFollow.Shake(strength);
    }

    private void SpawnEffect(CombatImpact impact)
    {
        GameObject prefab = impact.Kind == ImpactKind.Killed && killEffect != null ? killEffect : impactEffect;
        if (prefab == null) return;

        Vector3 forward = Vector3.up;
        if (impact.Attacker != null)
        {
            Vector3 direction = impact.Point - impact.Attacker.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) forward = direction.normalized;
        }

        GameObject instance = Instantiate(prefab, impact.Point, Quaternion.LookRotation(forward));
        Object.Destroy(instance, effectLifetime);
    }

    private void SpawnPopup(CombatImpact impact)
    {
        if (impact.Kind == ImpactKind.Blocked)
        {
            DamagePopup.Spawn(impact.Point, "БЛОК", blockedColor);
            return;
        }

        Color color = IsPlayer(impact.Victim)
            ? playerHitColor
            : (impact.IsCritical ? criticalColor : hitColor);
        DamagePopup.Spawn(impact.Point, impact.Damage, color);
    }
}
