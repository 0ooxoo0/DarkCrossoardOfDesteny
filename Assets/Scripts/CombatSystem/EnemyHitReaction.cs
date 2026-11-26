using System.Collections;
using UnityEngine;

/// <summary>
/// Реакция врага на попадание: вспышка материала и микронокбэк.
/// Слушает боевую шину и реагирует только на удар по себе.
/// </summary>
[DisallowMultipleComponent]
public class EnemyHitReaction : MonoBehaviour
{
    [Header("Flash")]
    [SerializeField] private Color flashColor = new Color(1f, 0.35f, 0.3f, 1f);
    [SerializeField, Min(0.01f)] private float flashDuration = 0.09f;
    [SerializeField] private Renderer[] flashRenderers;

    [Header("Knockback")]
    [SerializeField, Min(0f)] private float knockbackDistance = 0.35f;
    [SerializeField, Min(0.01f)] private float knockbackDuration = 0.12f;

    private MaterialPropertyBlock block;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Transform root;
    private UnityEngine.AI.NavMeshAgent agent;
    private Coroutine flashRoutine;

    private void Awake()
    {
        root = transform;
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (flashRenderers == null || flashRenderers.Length == 0)
            flashRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable()
    {
        CombatEvents.Impact += HandleImpact;
    }

    private void OnDisable()
    {
        CombatEvents.Impact -= HandleImpact;
    }

    private void HandleImpact(CombatImpact impact)
    {
        if (impact.Victim == null || impact.Victim != root) return;
        if (impact.Kind == ImpactKind.Blocked) return;

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());

        if (knockbackDistance > 0f && impact.Attacker != null)
        {
            Vector3 push = root.position - impact.Attacker.transform.position;
            push.y = 0f;
            if (push.sqrMagnitude > 0.0001f) StartCoroutine(KnockbackRoutine(push.normalized));
        }
    }

    private IEnumerator FlashRoutine()
    {
        SetFlash(flashColor);
        yield return new WaitForSeconds(flashDuration);

        // Сброс именно пустым блоком: белый цвет испортил бы цвет материала модели.
        ClearFlash();
        flashRoutine = null;
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

    private IEnumerator KnockbackRoutine(Vector3 direction)
    {
        float elapsed = 0f;
        while (elapsed < knockbackDuration)
        {
            elapsed += Time.deltaTime;
            float step = knockbackDistance * (Time.deltaTime / knockbackDuration);

            // Через агент: прямая запись transform.position под NavMeshAgent даёт рывки.
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Move(direction * step);
            else root.position += direction * step;

            yield return null;
        }
    }
}
