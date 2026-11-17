using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD игрока: здоровье, Ци и текущий реалм. Собирается процедурно,
/// чтобы не зависеть от ручной разметки сцены. Полоса Ци переиспользует
/// <see cref="QiBarUI"/> и встаёт под полосой HP.
/// </summary>
[DisallowMultipleComponent]
public class PlayerVitalsUI : MonoBehaviour
{
    [Header("Вид")]
    [SerializeField] private Color healthColor = new Color(0.71f, 0.26f, 0.18f, 1f);
    [SerializeField] private Color panelColor = new Color(0.05f, 0.06f, 0.08f, 0.55f);
    [SerializeField] private Vector2 panelSize = new Vector2(360f, 100f);
    [SerializeField, Min(4f)] private float barHeight = 24f;

    private PlayerHealth health;
    private CultivationSystem cultivation;

    private Slider healthSlider;
    private TextMeshProUGUI healthLabel;
    private TextMeshProUGUI realmLabel;

    /// <summary>Полоса Ци, созданная внутри панели.</summary>
    public QiBarUI QiBar { get; private set; }

    /// <summary>Собирает панель под указанным родителем.</summary>
    public void Build(PlayerHealth healthComponent, QiComponent qiComponent,
        CultivationSystem cultivationSystem, DaoSystem daoSystem, Transform parent)
    {
        health = healthComponent;
        cultivation = cultivationSystem;

        var root = (RectTransform)transform;
        root.SetParent(parent, false);
        CultivationUIFactory.Place(root, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(40f, -40f), panelSize);

        var background = CultivationUIFactory.CreatePanel("Background", root, panelColor);
        CultivationUIFactory.Stretch(background.rectTransform);

        realmLabel = CultivationUIFactory.CreateText("Realm", root, string.Empty, 15f,
            CultivationUIFactory.TextMain);
        CultivationUIFactory.Place(realmLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, -4f), new Vector2(panelSize.x - 20f, 20f));

        var hpRect = CultivationUIFactory.CreateRect("HP", root);
        CultivationUIFactory.Place(hpRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, -26f), new Vector2(panelSize.x - 20f, barHeight));

        Image backgroundImage;
        Image fillImage;
        healthSlider = CultivationUIFactory.CreateBar("Bar", hpRect, healthColor,
            out backgroundImage, out fillImage);
        CultivationUIFactory.Stretch((RectTransform)healthSlider.transform);

        healthLabel = CultivationUIFactory.CreateText("Value", hpRect, "HP —", 14f,
            CultivationUIFactory.TextMain, TextAlignmentOptions.MidlineRight);
        CultivationUIFactory.Stretch(healthLabel.rectTransform);
        healthLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);

        // QiBarUI сам встаёт под переданной полосой HP и рисует подпись «Ци».
        var qiRect = CultivationUIFactory.CreateRect("Qi Bar", root);
        QiBar = qiRect.gameObject.AddComponent<QiBarUI>();
        QiBar.Build(qiComponent, daoSystem, root, hpRect);

        Subscribe();
        RefreshHealth();
        RefreshRealm();
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnHealthChanged -= HandleHealthChanged;

        if (cultivation != null)
        {
            cultivation.OnRealmChanged -= HandleRealmChanged;
            cultivation.OnStageChanged -= HandleStageChanged;
        }
    }

    private void Subscribe()
    {
        if (health != null)
            health.OnHealthChanged += HandleHealthChanged;

        if (cultivation != null)
        {
            cultivation.OnRealmChanged += HandleRealmChanged;
            cultivation.OnStageChanged += HandleStageChanged;
        }
    }

    private void HandleHealthChanged(int current, int max)
    {
        RefreshHealth();
    }

    private void HandleRealmChanged(CultivationRealm realm)
    {
        RefreshRealm();
    }

    private void HandleStageChanged(CultivationRealm realm, int stage)
    {
        RefreshRealm();
    }

    private void RefreshHealth()
    {
        if (health == null || healthSlider == null || healthLabel == null) return;

        int current = health.Current;
        int max = health.Max;

        healthSlider.value = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        healthLabel.text = $"HP {current}/{max}";
    }

    private void RefreshRealm()
    {
        if (realmLabel == null) return;

        if (cultivation == null || cultivation.CurrentRealm == null)
        {
            realmLabel.text = string.Empty;
            return;
        }

        realmLabel.text = $"{cultivation.CurrentRealm.RealmName} · {cultivation.CurrentStage} ступень";
    }
}
