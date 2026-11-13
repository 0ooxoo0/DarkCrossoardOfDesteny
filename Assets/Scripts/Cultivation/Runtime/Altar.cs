using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Алтарь Дао: пока игрок внутри объёма — разрешена смена Дао (`DaoSystem.SetNearAltar`),
/// по E открывается панель культивации, а `MeditationZone` на этом же объекте даёт ×2 к медитации.
/// Компонент аддитивный: существующие системы не меняет, только вызывает их публичный API.
/// </summary>
[DisallowMultipleComponent]
public class Altar : MonoBehaviour
{
    [Header("Ссылки (необязательно — найдутся сами)")]
    [SerializeField] private DaoSystem dao;
    [SerializeField] private QiComponent qi;

    [Header("Взаимодействие")]
    [SerializeField] private bool openCultivationPanelOnUse = true;
    [Tooltip("Клавиша открытия панели культивации, пока игрок стоит у алтаря.")]
    [SerializeField] private KeyCode useKey = KeyCode.E;

    [Header("Медитация")]
    [Tooltip("Добавить MeditationZone на этот же объект (×2 к скорости медитации).")]
    [SerializeField] private bool addMeditationZone = true;
    [SerializeField, Min(1f)] private float meditationMultiplier = 2f;

    [Header("Индикация (опционально)")]
    [Tooltip("Префаб VFX активации (All In 1 Vfx Toolkit). Пусто — только свет и лог.")]
    [SerializeField] private GameObject activateVfxPrefab;
    [SerializeField] private Light altarLight;

    private bool playerInside;

    public bool PlayerInside => playerInside;
    public DaoSystem Dao => dao;

    private void Awake()
    {
        EnsureTrigger();

        if (addMeditationZone && GetComponent<MeditationZone>() == null)
        {
            MeditationZone zone = gameObject.AddComponent<MeditationZone>();
            zone.SetMultiplier(meditationMultiplier);
        }
    }

    private void Update()
    {
        if (!playerInside) return;
        if (!openCultivationPanelOnUse) return;
        if (!CultivationInput.Pressed(Key.E, useKey)) return;

        CultivationUIRoot ui = CultivationUIRoot.Instance;
        if (ui == null || ui.CultivationPanel == null)
        {
            Debug.Log("[Altar] Панель культивации ещё не собрана (CultivationBootstrap не отработал).");
            return;
        }

        ui.CultivationPanel.Open();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        playerInside = true;
        ResolveReferences(other);
        ApplyAltarState(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;

        playerInside = false;
        ApplyAltarState(false);
    }

    private void OnDisable()
    {
        if (!playerInside) return;
        playerInside = false;
        ApplyAltarState(false);
    }

    private void OnDestroy()
    {
        if (dao != null) dao.SetNearAltar(false);
    }

    /// <summary>Внешние системы (телепорт, катсцена) могут включить/выключить близость вручную.</summary>
    public void SetPlayerInside(bool value)
    {
        playerInside = value;
        ApplyAltarState(value);
    }

    private void ApplyAltarState(bool active)
    {
        if (dao != null) dao.SetNearAltar(active);
        if (altarLight != null) altarLight.enabled = active;

        if (!active) return;

        if (activateVfxPrefab != null)
        {
            GameObject vfx = Instantiate(activateVfxPrefab, transform.position + Vector3.up, Quaternion.identity);
            Destroy(vfx, 4f);
        }

        Debug.Log("[Altar] Смена Дао разрешена. E — панель культивации, M — медитация (×" +
                  meditationMultiplier.ToString("0.#") + ").");
    }

    private void ResolveReferences(Collider other)
    {
        if (dao == null) dao = other.GetComponentInParent<DaoSystem>();
        if (dao == null) dao = FindFirstObjectByType<DaoSystem>();
        if (qi == null) qi = other.GetComponentInParent<QiComponent>();
    }

    private void EnsureTrigger()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger == null) trigger = gameObject.AddComponent<BoxCollider>();
        if (!trigger.isTrigger) trigger.isTrigger = true;

        // У игрока NavMeshAgent без Rigidbody — без кинематического тела OnTriggerEnter не сработает.
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    private static bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        return other.GetComponentInParent<PlayerController>() != null;
    }
}
