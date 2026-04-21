using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Простой универсальный таймер обратного отсчёта.
/// </summary>
public class Timer : MonoBehaviour
{
    [Header("Настройки таймера")]
    [Tooltip("Время обратного отсчёта в секундах.")]
    public float duration = 5f;
    [Tooltip("ССлучайное число.")]
    public bool Rand = false;
    public Vector2 Change = new Vector2(0f, 5f);

    [Tooltip("Запускать таймер автоматически при старте сцены.")]
    public bool autoStart = true;

    [Header("События")]
    [Tooltip("Срабатывает, когда таймер достигает нуля.")]
    public UnityEvent onComplete;

    // Текущее оставшееся время
    public float timer;
    // Флаг активности таймера
    public bool isRunning;

    private void Start()
    {

        if (autoStart)
            StartTimer();
        if (Rand)
            duration = Random.Range(Change[0], Change[1]);
    }

    private void Update()
    {
        if (!isRunning) return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            timer = 0f;
            isRunning = false;
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// Запускает таймер с текущей длительностью.
    /// </summary>
    public void StartTimer()
    {
        timer = duration;
        isRunning = true;
    }

    /// <summary>
    /// Запускает таймер с новой длительностью.
    /// </summary>
    public void StartTimer(float newDuration)
    {
        duration = newDuration;
        StartTimer();
    }

    /// <summary>
    /// Останавливает таймер (сбрасывает прогресс).
    /// </summary>
    public void StopTimer()
    {
        isRunning = false;
        timer = 0f;
    }

    /// <summary>
    /// Ставит таймер на паузу.
    /// </summary>
    public void PauseTimer()
    {
        isRunning = false;
    }

    /// <summary>
    /// Возобновляет работу таймера после паузы.
    /// </summary>
    public void ResumeTimer()
    {
        if (timer > 0f)
            isRunning = true;
    }

    /// <summary>
    /// Возвращает оставшееся время в секундах.
    /// </summary>
    public float GetRemainingTime() => timer;

    /// <summary>
    /// Возвращает true, если таймер активен.
    /// </summary>
    public bool IsRunning() => isRunning;
}