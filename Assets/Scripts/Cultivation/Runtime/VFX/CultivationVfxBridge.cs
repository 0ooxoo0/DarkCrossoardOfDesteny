using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Проигрывает VFX по событиям культивации: прерывание, стадии последствий,
/// прорыв. Префабы берёт из <see cref="CultivationVfxConfig"/>; если ссылки нет —
/// строит процедурный плейсхолдер и один раз предупреждает в консоль.
/// </summary>
[DisallowMultipleComponent]
public class CultivationVfxBridge : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private CultivationVfxConfig config;

    [Header("Плейсхолдеры")]
    [SerializeField] private Color qiSurgeColor = new Color(0.95f, 0.55f, 0.2f, 1f);
    [SerializeField] private Color qiDeviationColor = new Color(0.6f, 0.25f, 0.8f, 1f);
    [SerializeField] private Color severingColor = new Color(0.5f, 0.05f, 0.08f, 1f);
    [SerializeField] private Color breakthroughColor = new Color(0.85f, 0.75f, 0.4f, 1f);
    [SerializeField] private Color breakthroughFailColor = new Color(0.7f, 0.2f, 0.15f, 1f);
    [SerializeField, Min(0f)] private float effectHeight = 1f;

    [Header("Виньетка обрыва")]
    [SerializeField, Range(0f, 1f)] private float vignetteAlpha = 0.75f;
    [SerializeField, Min(0.1f)] private float vignetteFadeSeconds = 1.5f;

    [Header("Время жизни без конфига")]
    [SerializeField, Min(0.5f)] private float fallbackLifetime = 4f;

    private MeditationController meditation;
    private BreakthroughSequence breakthrough;
    private CultivationDebuffController debuffs;

    private readonly HashSet<string> warned = new HashSet<string>();
    private Image vignette;

    public CultivationVfxConfig Config => config;

    /// <summary>Подписывает мост на события и готовит виньетку.</summary>
    public void Initialize(CultivationVfxConfig vfxConfig, MeditationController meditationController,
        BreakthroughSequence breakthroughSequence, CultivationDebuffController debuffController)
    {
        Unsubscribe();

        config = vfxConfig;
        meditation = meditationController;
        breakthrough = breakthroughSequence;
        debuffs = debuffController;

        EnsureVignette();
        Subscribe();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (vignette != null && vignette.transform.parent != null)
            Destroy(vignette.transform.parent.gameObject);
    }

    private void Update()
    {
        if (vignette == null) return;

        Color color = vignette.color;
        if (color.a <= 0f) return;

        // Виньетка плавно гаснет после обрыва.
        color.a = Mathf.MoveTowards(color.a, 0f, Time.deltaTime / Mathf.Max(0.1f, vignetteFadeSeconds));
        vignette.color = color;
    }

    private void Subscribe()
    {
        if (meditation != null)
        {
            meditation.OnMeditationInterrupted -= HandleMeditationInterrupted;
            meditation.OnMeditationInterrupted += HandleMeditationInterrupted;
        }

        if (breakthrough != null)
        {
            breakthrough.OnBreakthroughStarted -= HandleBreakthroughStarted;
            breakthrough.OnBreakthroughResult -= HandleBreakthroughResult;

            breakthrough.OnBreakthroughStarted += HandleBreakthroughStarted;
            breakthrough.OnBreakthroughResult += HandleBreakthroughResult;
        }

        if (debuffs != null)
        {
            debuffs.OnDebuffApplied -= HandleDebuffApplied;
            debuffs.OnDebuffApplied += HandleDebuffApplied;
        }
    }

    private void Unsubscribe()
    {
        if (meditation != null)
            meditation.OnMeditationInterrupted -= HandleMeditationInterrupted;

        if (breakthrough != null)
        {
            breakthrough.OnBreakthroughStarted -= HandleBreakthroughStarted;
            breakthrough.OnBreakthroughResult -= HandleBreakthroughResult;
        }

        if (debuffs != null)
            debuffs.OnDebuffApplied -= HandleDebuffApplied;
    }

    private void HandleMeditationInterrupted()
    {
        Play(CultivationVfxKind.QiSurge, transform);
    }

    private void HandleBreakthroughStarted()
    {
        ParticleSystem prefab = config != null ? config.BreakthroughStartPrefab : null;
        if (Spawn(prefab, transform) != null) return;

        WarnOnce("BreakthroughStart", "Нет префаба начала прорыва — используется плейсхолдер.");
        CultivationVfxFactory.CreateBreakthroughPillar(transform, breakthroughColor);
    }

    private void HandleBreakthroughResult(BreakthroughResult result, string message)
    {
        PlayBreakthrough(result == BreakthroughResult.Success, transform);
    }

    private void HandleDebuffApplied(ActiveCultivationDebuff debuff)
    {
        if (debuff == null) return;
        Play(debuff.Vfx, transform);
    }

    /// <summary>Проигрывает эффект стадии последствий в позиции источника.</summary>
    public void Play(CultivationVfxKind kind, Transform origin)
    {
        if (kind == CultivationVfxKind.None || origin == null) return;

        if (Spawn(PrefabFor(kind), origin) != null)
        {
            if (kind == CultivationVfxKind.Severing) ShowVignette();
            return;
        }

        WarnOnce(kind.ToString(), "Нет префаба " + kind + " — используется плейсхолдер.");

        Vector3 point = origin.position + Vector3.up * effectHeight;
        switch (kind)
        {
            case CultivationVfxKind.QiSurge:
                CultivationVfxFactory.CreateCastBurst(point, qiSurgeColor, 1.2f);
                break;
            case CultivationVfxKind.QiDeviation:
                CultivationVfxFactory.CreateCastBurst(point, qiDeviationColor, 1.7f);
                CultivationVfxFactory.PlayFlash(origin, qiDeviationColor, 3f, 1.2f);
                break;
            case CultivationVfxKind.Severing:
                CultivationVfxFactory.CreateCastBurst(point, severingColor, 2.2f);
                CultivationVfxFactory.PlayFlash(origin, severingColor, 6f, 1.8f);
                ShowVignette();
                break;
        }
    }

    /// <summary>Успех и провал прорыва дают разные эффекты.</summary>
    public void PlayBreakthrough(bool success, Transform origin)
    {
        if (origin == null) return;

        ParticleSystem prefab = config != null
            ? (success ? config.BreakthroughSuccessPrefab : config.BreakthroughFailPrefab)
            : null;

        string key = success ? "BreakthroughSuccess" : "BreakthroughFail";
        string label = success ? "успешного прорыва" : "провального прорыва";

        if (Spawn(prefab, origin) != null) return;

        WarnOnce(key, "Нет префаба " + label + " — используется плейсхолдер.");

        if (success)
        {
            CultivationVfxFactory.CreateBreakthroughPillar(origin, breakthroughColor);
            CultivationVfxFactory.PlayFlash(origin, breakthroughColor, 8f, 1.4f);
        }
        else
        {
            Vector3 point = origin.position + Vector3.up * effectHeight;
            CultivationVfxFactory.CreateCastBurst(point, breakthroughFailColor, 1.6f);
            CultivationVfxFactory.PlayFlash(origin, breakthroughFailColor, 4f, 1f);
        }
    }

    private ParticleSystem PrefabFor(CultivationVfxKind kind)
    {
        if (config == null) return null;

        switch (kind)
        {
            case CultivationVfxKind.QiSurge: return config.QiSurgePrefab;
            case CultivationVfxKind.QiDeviation: return config.QiDeviationPrefab;
            case CultivationVfxKind.Severing: return config.SeveringPrefab;
            default: return null;
        }
    }

    private ParticleSystem Spawn(ParticleSystem prefab, Transform origin = null)
    {
        if (prefab == null || origin == null) return null;

        ParticleSystem effect = Instantiate(prefab, origin.position + Vector3.up * effectHeight,
            Quaternion.identity);
        Destroy(effect.gameObject, config != null ? config.EffectLifetime : fallbackLifetime);
        return effect;
    }

    private void ShowVignette()
    {
        EnsureVignette();
        if (vignette == null) return;

        Color color = vignette.color;
        color.a = vignetteAlpha;
        vignette.color = color;
    }

    private void EnsureVignette()
    {
        if (vignette != null) return;

        Canvas canvas = CultivationUIFactory.FindOrCreateCanvas();
        if (canvas == null) return;

        var holder = CultivationUIFactory.CreateRect("Cultivation Vignette", canvas.transform);
        CultivationUIFactory.Stretch(holder);

        vignette = CultivationUIFactory.CreateImage("Vignette", holder,
            new Color(severingColor.r, severingColor.g, severingColor.b, 0f),
            CultivationUIFactory.VignetteSprite, false);
        CultivationUIFactory.Stretch(vignette.rectTransform);
        vignette.raycastTarget = false;
    }

    private void WarnOnce(string key, string message)
    {
        if (!warned.Add(key)) return;
        Debug.LogWarning("[CultivationVfx] " + message);
    }
}
