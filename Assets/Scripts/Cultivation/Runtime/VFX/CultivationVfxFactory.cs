using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Процедурные плейсхолдеры VFX: аура Ци, частицы медитации, столб прорыва.
/// Реальные префабы (All In 1 Vfx Toolkit) назначаются в инспекторе поверх этих заглушек.
/// </summary>
public static class CultivationVfxFactory
{
    public static Material CreateParticleMaterial(Color color, bool additive = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        if (shader == null)
        {
            Debug.LogWarning("[Cultivation] Не найден шейдер частиц — VFX будет без материала.");
            return null;
        }

        var material = new Material(shader) { name = "CultivationParticle (Runtime)" };

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", Texture2D.whiteTexture);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", additive ? 2f : 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetShaderPassEnabled("ShadowCaster", false);
        return material;
    }

    public static ParticleSystem CreateAura(Transform parent, Color color)
    {
        ParticleSystem particles = CreateSystem(parent, "QiAura");
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.2f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 240;
        main.gravityModifier = -0.015f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 24f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;
        shape.radiusThickness = 1f;

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.2f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.3f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(FadeGradient());

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f));

        ApplyRenderer(particles, color, false);
        particles.Play();
        return particles;
    }

    public static ParticleSystem CreateMeditationMotes(Transform parent, Color color)
    {
        ParticleSystem particles = CreateSystem(parent, "MeditationMotes");
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.duration = 3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = color;
        main.maxParticles = 160;
        main.gravityModifier = -0.02f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 14f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.radius = 0.7f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(FadeGradient());

        ApplyRenderer(particles, color, false);
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    public static ParticleSystem CreateBreakthroughPillar(Transform parent, Color color)
    {
        ParticleSystem particles = CreateSystem(parent, "BreakthroughPillar");
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = 1.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
        main.startColor = color;
        main.maxParticles = 600;
        main.gravityModifier = 0.15f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 260) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 5f;
        shape.radius = 0.35f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(FadeGradient());

        ApplyRenderer(particles, color, true);
        particles.gameObject.AddComponent<CultivationVfxAutoDestroy>().Initialize(3.5f);
        particles.Play();
        return particles;
    }

    public static ParticleSystem CreateCastBurst(Vector3 position, Color color, float scale = 1f)
    {
        var go = new GameObject("CastBurst");
        go.transform.position = position;

        var particles = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = 0.6f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);
        main.startColor = color;
        main.maxParticles = 140;
        main.gravityModifier = 0.25f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(Mathf.RoundToInt(45f * Mathf.Max(0.5f, scale)), 10, 120)) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(FadeGradient());

        ApplyRenderer(particles, color, true);
        particles.gameObject.AddComponent<CultivationVfxAutoDestroy>().Initialize(2.5f);
        particles.Play();
        return particles;
    }

    public static Light PlayFlash(Transform parent, Color color, float intensity = 8f, float duration = 1.4f)
    {
        var go = new GameObject("BreakthroughFlash");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = 14f;
        light.intensity = intensity;

        var flash = go.AddComponent<CultivationVfxFlash>();
        flash.Initialize(light, intensity, duration);
        return light;
    }

    private static ParticleSystem CreateSystem(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var particles = go.AddComponent<ParticleSystem>();
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return particles;
    }

    private static void ApplyRenderer(ParticleSystem particles, Color color, bool additive)
    {
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        Material material = CreateParticleMaterial(color, additive);
        if (material == null) return;

        renderer.sharedMaterial = material;

        var owner = particles.gameObject.AddComponent<CultivationVfxMaterialOwner>();
        owner.material = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.alignment = ParticleSystemRenderSpace.World;
    }

    private static Gradient FadeGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.25f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }
}

/// <summary>Уничтожает рантайм-материал вместе с объектом VFX, чтобы не копить утечки.</summary>
[DisallowMultipleComponent]
public class CultivationVfxMaterialOwner : MonoBehaviour
{
    public Material material;

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}

/// <summary>Удаляет объект VFX через заданное время (для одноразовых эффектов).</summary>
[DisallowMultipleComponent]
public class CultivationVfxAutoDestroy : MonoBehaviour
{
    private float lifetime = 3f;

    public void Initialize(float seconds)
    {
        lifetime = Mathf.Max(0.1f, seconds);
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}

/// <summary>Короткая вспышка света прорыва, сама себя удаляет.</summary>
[DisallowMultipleComponent]
public class CultivationVfxFlash : MonoBehaviour
{
    private Light target;
    private float startIntensity;
    private float duration;
    private float elapsed;

    public void Initialize(Light light, float intensity, float flashDuration)
    {
        target = light;
        startIntensity = intensity;
        duration = Mathf.Max(0.1f, flashDuration);
        elapsed = 0f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        if (target != null)
            target.intensity = Mathf.Lerp(startIntensity, 0f, t * t);

        if (t >= 1f)
            Destroy(gameObject);
    }
}
