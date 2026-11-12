using UnityEngine;

/// <summary>
/// Аура Ци вокруг игрока: цвет = активный Дао, интенсивность = реалм.
/// auraPrefab — сюда назначаются префабы All In 1 Vfx Toolkit; без него строится плейсхолдер.
/// </summary>
[DisallowMultipleComponent]
public class DaoAuraController : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private DaoSystem dao;
    [SerializeField] private CultivationSystem cultivation;

    [Header("Префаб (опционально)")]
    [Tooltip("Префаб ауры из All In 1 Vfx Toolkit. Пусто = процедурный плейсхолдер.")]
    [SerializeField] private ParticleSystem auraPrefab;
    [SerializeField] private Vector3 localOffset = Vector3.zero;

    [Header("Интенсивность")]
    [SerializeField, Min(0f)] private float baseEmissionRate = 18f;
    [SerializeField, Min(0f)] private float emissionPerRealm = 12f;
    [SerializeField, Min(0f)] private float emissionPerStage = 4f;

    private ParticleSystem aura;
    private Color currentColor = Color.white;

    public ParticleSystem Aura => aura;

    public void Initialize(DaoSystem daoSystem, CultivationSystem cultivationSystem)
    {
        Unsubscribe();
        dao = daoSystem;
        cultivation = cultivationSystem;
        Subscribe();
        Rebuild();
    }

    private void OnEnable()
    {
        Subscribe();
        if (aura == null) Rebuild();
        else Apply();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (dao != null)
        {
            dao.OnActiveDaoChanged -= HandleActiveDaoChanged;
            dao.OnActiveDaoChanged += HandleActiveDaoChanged;
        }

        if (cultivation != null)
        {
            cultivation.OnRealmChanged -= HandleRealmChanged;
            cultivation.OnRealmChanged += HandleRealmChanged;
            cultivation.OnStageChanged -= HandleStageChanged;
            cultivation.OnStageChanged += HandleStageChanged;
        }
    }

    private void Unsubscribe()
    {
        if (dao != null) dao.OnActiveDaoChanged -= HandleActiveDaoChanged;
        if (cultivation != null)
        {
            cultivation.OnRealmChanged -= HandleRealmChanged;
            cultivation.OnStageChanged -= HandleStageChanged;
        }
    }

    private void HandleActiveDaoChanged(DaoType type) => Apply();

    private void HandleRealmChanged(CultivationRealm realm) => Apply();

    private void HandleStageChanged(CultivationRealm realm, int stage) => Apply();

    public void Rebuild()
    {
        if (aura != null)
        {
            Destroy(aura.gameObject);
            aura = null;
        }

        if (auraPrefab != null)
        {
            aura = Instantiate(auraPrefab, transform);
            aura.transform.localPosition = localOffset;
        }
        else
        {
            aura = CultivationVfxFactory.CreateAura(transform, currentColor);
            aura.transform.localPosition = localOffset;
        }

        Apply();
    }

    public void Apply()
    {
        if (aura == null) return;

        currentColor = ResolveColor();
        int realmIndex = cultivation != null ? cultivation.CurrentRealmIndex : 0;
        int stage = cultivation != null ? cultivation.CurrentStage : 1;

        ParticleSystem.MainModule main = aura.main;
        main.startColor = currentColor;

        ParticleSystem.EmissionModule emission = aura.emission;
        emission.rateOverTime = baseEmissionRate + emissionPerRealm * realmIndex + emissionPerStage * (stage - 1);

        var renderer = aura.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_BaseColor"))
            renderer.sharedMaterial.SetColor("_BaseColor", currentColor);
    }

    public void SetAuraPrefab(ParticleSystem prefab)
    {
        auraPrefab = prefab;
        Rebuild();
    }

    private Color ResolveColor()
    {
        if (dao != null && dao.ActiveDao.HasValue)
        {
            var path = dao.GetDaoPath(dao.ActiveDao.Value);
            if (path != null) return path.Color;
            return DaoTypeInfo.GetColor(dao.ActiveDao.Value);
        }

        return DaoTypeInfo.GetColor(DaoType.Sword);
    }
}
