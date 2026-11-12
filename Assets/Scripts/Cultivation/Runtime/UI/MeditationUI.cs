using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Лёгкий оверлей медитации: виньетка, счётчики опыта и кнопка «Прервать».
/// </summary>
[DisallowMultipleComponent]
public class MeditationUI : MonoBehaviour
{
    private MeditationController meditation;
    private CultivationSystem cultivation;
    private DaoSystem dao;

    private TextMeshProUGUI multiplierLabel;
    private TextMeshProUGUI experienceLabel;
    private TextMeshProUGUI comprehensionLabel;
    private TextMeshProUGUI daoLabel;
    private Image glowImage;

    private float refreshTimer;

    public void Build(MeditationController meditationController, CultivationSystem cultivationSystem, DaoSystem daoSystem)
    {
        meditation = meditationController;
        cultivation = cultivationSystem;
        dao = daoSystem;

        BuildHierarchy();
        Subscribe();
        gameObject.SetActive(false);
    }

    private void BuildHierarchy()
    {
        var root = (RectTransform)transform;
        CultivationUIFactory.Stretch(root);

        var dim = CultivationUIFactory.CreateImage("Dim", root, new Color(0.02f, 0.02f, 0.03f, 0.25f));
        CultivationUIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = false;

        var vignette = CultivationUIFactory.CreateImage("Vignette", root, new Color(0.01f, 0.01f, 0.015f, 0.95f),
            CultivationUIFactory.VignetteSprite, false);
        CultivationUIFactory.Stretch(vignette.rectTransform);
        vignette.raycastTarget = false;

        glowImage = CultivationUIFactory.CreateImage("Glow", root, new Color(1f, 1f, 1f, 0.16f),
            CultivationUIFactory.RadialSprite, false);
        CultivationUIFactory.Place(glowImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 90f), new Vector2(1100f, 1100f));
        glowImage.raycastTarget = false;

        var title = CultivationUIFactory.CreateText("Title", root, "МЕДИТАЦИЯ", 44f, CultivationUIFactory.TextMain,
            TextAlignmentOptions.Top);
        CultivationUIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -110f), new Vector2(900f, 60f));
        title.characterSpacing = 18f;

        multiplierLabel = CultivationUIFactory.CreateText("Multiplier", root, "×1.0", 26f, CultivationUIFactory.Gold,
            TextAlignmentOptions.Top);
        CultivationUIFactory.Place(multiplierLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -176f), new Vector2(400f, 36f));

        experienceLabel = CultivationUIFactory.CreateText("Experience", root, string.Empty, 23f,
            CultivationUIFactory.TextMain, TextAlignmentOptions.Center);
        CultivationUIFactory.Place(experienceLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 40f), new Vector2(900f, 34f));

        comprehensionLabel = CultivationUIFactory.CreateText("Comprehension", root, string.Empty, 17f,
            CultivationUIFactory.TextMuted, TextAlignmentOptions.Center);
        CultivationUIFactory.Place(comprehensionLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 4f), new Vector2(900f, 28f));

        daoLabel = CultivationUIFactory.CreateText("Dao", root, string.Empty, 17f, CultivationUIFactory.TextMuted,
            TextAlignmentOptions.Center);
        CultivationUIFactory.Place(daoLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -32f), new Vector2(900f, 28f));

        var stopButton = CultivationUIFactory.CreateButton("Stop", root, "ПРЕРВАТЬ", new Vector2(260f, 60f),
            StopMeditation, CultivationUIFactory.Danger, 20f);
        CultivationUIFactory.Place((RectTransform)stopButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 130f), new Vector2(260f, 60f));

        var hint = CultivationUIFactory.CreateText("Hint", root, "Урон прерывает медитацию", 14f,
            new Color(0.7f, 0.68f, 0.62f, 0.8f), TextAlignmentOptions.Bottom);
        CultivationUIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 92f), new Vector2(700f, 24f));
    }

    private void Subscribe()
    {
        if (meditation == null) return;
        meditation.OnMeditationStarted += HandleStarted;
        meditation.OnMeditationStopped += HandleStopped;
        meditation.OnMeditationTick += HandleTick;
        meditation.OnMeditationInterrupted += HandleInterrupted;
    }

    private void OnDestroy()
    {
        if (meditation == null) return;
        meditation.OnMeditationStarted -= HandleStarted;
        meditation.OnMeditationStopped -= HandleStopped;
        meditation.OnMeditationTick -= HandleTick;
        meditation.OnMeditationInterrupted -= HandleInterrupted;
    }

    private void HandleStarted()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    private void HandleStopped()
    {
        gameObject.SetActive(false);
    }

    private void HandleInterrupted()
    {
        gameObject.SetActive(false);
    }

    private void HandleTick(int experience, int points, float multiplier)
    {
        Refresh();
    }

    private void StopMeditation()
    {
        if (meditation != null) meditation.Interrupt();
    }

    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;

        refreshTimer = 0.25f;
        Refresh();
    }

    private void Refresh()
    {
        if (meditation == null || cultivation == null) return;

        float multiplier = meditation.CurrentMultiplier;
        if (multiplierLabel != null) multiplierLabel.text = "×" + multiplier.ToString("0.0");

        if (experienceLabel != null)
            experienceLabel.text = "Опыт Ци: +" + Mathf.RoundToInt(meditation.ExperiencePerSecond) + " / сек";

        if (comprehensionLabel != null)
        {
            comprehensionLabel.text = "Прозрение: " + cultivation.Comprehension +
                                     "   ·   шанс прорыва " + Mathf.RoundToInt(cultivation.ComputeSuccessChance() * 100f) + "%";
        }

        if (daoLabel != null && dao != null)
        {
            Color color = CultivationUIFactory.Gold;
            if (dao.ActiveDao.HasValue)
            {
                var path = dao.GetDaoPath(dao.ActiveDao.Value);
                color = path != null ? path.Color : DaoTypeInfo.GetColor(dao.ActiveDao.Value);
                daoLabel.text = "Путь: " + (path != null ? path.DisplayName : DaoTypeInfo.GetDisplayName(dao.ActiveDao.Value)) +
                                "  ·  Дао +" + meditation.DaoPointsPerSecond.ToString("0.#") +
                                " / сек  ·  Ци +" + meditation.QiRestorePerSecond.ToString("0.#") + " / сек";
            }
            else
            {
                daoLabel.text = "Активный путь не выбран";
            }
            daoLabel.color = new Color(color.r, color.g, color.b, 0.85f);

            if (glowImage != null)
                glowImage.color = new Color(color.r, color.g, color.b, 0.16f);
        }
    }
}
