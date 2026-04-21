using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("UI элементы")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Настройки отображения")]
    [SerializeField] private float hideDelay = 3f; // Через сколько секунд скрыть после последнего урона
    [SerializeField] private float fadeSpeed = 2f; // Скорость исчезновения

    private Transform target; // За кем следить (враг или объект)
    private float lastDamageTime;
    private bool isVisible = false;
    private Camera mainCamera;

    // Инициализация (вызывается объектом, которому принадлежит HealthBar)
    public void Initialize(Transform followTarget, float maxHealth)
    {
        target = followTarget;
        healthSlider.maxValue = maxHealth;
        healthSlider.value = maxHealth;
        mainCamera = Camera.main;

        // Изначально скрыт
        canvasGroup.alpha = 0f;
        isVisible = false;
    }

    // Вызывается при получении урона владельцем
    public void UpdateHealth(float currentHealth)
    {
        healthSlider.value = currentHealth;
        lastDamageTime = Time.time;

        if (!isVisible)
        {
            isVisible = true;
            canvasGroup.alpha = 1f;
        }
    }

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        // Следуем за целью (World Space Canvas)
        transform.position = target.position + Vector3.up * 2f; // Поднять над объектом
        transform.rotation = mainCamera.transform.rotation; // Всегда лицом к камере

        // Автоматическое скрытие через hideDelay
        if (isVisible && Time.time > lastDamageTime + hideDelay)
        {
            canvasGroup.alpha -= fadeSpeed * Time.deltaTime;
            if (canvasGroup.alpha <= 0f)
            {
                isVisible = false;
                canvasGroup.alpha = 0f;
            }
        }
    }

    // Для ручного скрытия (например, при смерти)
    public void Hide()
    {
        isVisible = false;
        canvasGroup.alpha = 0f;
    }
}