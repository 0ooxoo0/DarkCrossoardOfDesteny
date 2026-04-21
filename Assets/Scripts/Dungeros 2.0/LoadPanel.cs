using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LoadPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private GameObject textObject;
    [SerializeField] private Image panel;
    [SerializeField] private float fadeDuration = 5f;

    private Color panelColor;
    private bool shouldFade = false;

    private void Start()
    {
        panelColor = panel.color;
        panelColor.a = 1f;
        panel.color = panelColor;

        // Пример: начинаем fade через 3 секунды или по событию завершения генерации
        Invoke(nameof(StartFade), 3f);
    }

    private void Update()
    {
        if (shouldFade)
        {
            if (panelColor.a > 0f)
            {
                panelColor.a -= Time.deltaTime / fadeDuration;
                panel.color = panelColor;
            }
        }
    }

    private void StartFade()
    {
        shouldFade = true;
        Destroy(gameObject, fadeDuration);
    }
}