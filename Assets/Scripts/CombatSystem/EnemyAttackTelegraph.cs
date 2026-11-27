using System.Collections;
using UnityEngine;

/// <summary>
/// Телеграф атаки врага: короткая вспышка материала и звук замаха до открытия хитбокса.
/// Без него удар врага нечем предугадать, и блок превращается в угадайку.
/// Вешается на того же врага, что и <see cref="EnemyCombatAI"/>.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAttackTelegraph : MonoBehaviour
{
    [Header("Вспышка")]
    [Tooltip("Цвет предупреждающей вспышки.")]
    [SerializeField] private Color flashColor = new Color(1f, 0.72f, 0.2f, 1f);
    [Tooltip("Длительность вспышки. Окно удара открывается примерно через столько же.")]
    [SerializeField, Min(0.02f)] private float flashDuration = 0.14f;

    [Header("Звук")]
    [Tooltip("Короткий вдох/свист замаха. Пусто — телеграф только визуальный.")]
    [SerializeField] private AudioClip warningClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.6f;

    [Header("Ссылки")]
    [Tooltip("Рендереры модели. Пусто — берутся все дочерние.")]
    [SerializeField] private Renderer[] flashRenderers;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock block;
    private Coroutine routine;

    /// <summary>Идёт ли вспышка прямо сейчас (для тестов и отладки).</summary>
    public bool IsTelegraphing { get; private set; }

    private void Awake()
    {
        if (flashRenderers == null || flashRenderers.Length == 0)
            flashRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnDisable()
    {
        // Выключенный враг не должен оставить модель перекрашенной.
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        IsTelegraphing = false;
        ClearFlash();
    }

    /// <summary>Показать телеграф. Вызывается из <see cref="EnemyCombatAI"/> на старте атаки.</summary>
    public void Play()
    {
        if (!isActiveAndEnabled) return;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FlashRoutine());

        if (warningClip != null)
            AudioSource.PlayClipAtPoint(warningClip, transform.position + Vector3.up, volume);
    }

    private IEnumerator FlashRoutine()
    {
        IsTelegraphing = true;
        SetFlash(flashColor);

        yield return new WaitForSeconds(flashDuration);

        ClearFlash();
        IsTelegraphing = false;
        routine = null;
    }

    private void SetFlash(Color color)
    {
        if (flashRenderers == null) return;
        if (block == null) block = new MaterialPropertyBlock();

        for (int i = 0; i < flashRenderers.Length; i++)
        {
            Renderer target = flashRenderers[i];
            if (target == null) continue;

            target.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            target.SetPropertyBlock(block);
        }
    }

    private void ClearFlash()
    {
        if (flashRenderers == null) return;

        for (int i = 0; i < flashRenderers.Length; i++)
        {
            Renderer target = flashRenderers[i];
            if (target != null) target.SetPropertyBlock(null);
        }
    }
}
