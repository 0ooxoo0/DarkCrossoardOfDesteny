using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Fps : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI fpsText; // Ссылка на Text‑компонент UI

    private float updateInterval = 0.5f; // Интервал обновления (сек)
    private float accumulator = 0f;      // Сумма кадров за интервал
    private int frames = 0;             // Количество кадров за интервал
    private float timeLeft;              // Оставшееся время до обновления

    private void Awake()
    {
        // Если Text не назначен — ищем автоматически
        if (fpsText == null)
        {
            fpsText = GetComponent<TextMeshProUGUI>();
        }
    }

    private void Start()
    {
        timeLeft = updateInterval;
    }

    private void Update()
    {
        // Накапливаем время и кадры
        timeLeft -= Time.deltaTime;
        accumulator += Time.deltaTime;
        frames++;

        // Если интервал истёк — обновляем текст
        if (timeLeft <= 0f)
        {
            float fps = frames / accumulator;
            fpsText.text = $"FPS: {fps:F1}"; // Округление до 1 знака после запятой

            // Сброс счётчиков
            timeLeft = updateInterval;
            accumulator = 0f;
            frames = 0;
        }
    }
}
