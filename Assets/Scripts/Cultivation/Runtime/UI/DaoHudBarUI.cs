using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Вертикальная полоса Дао у левого края: школа, уровень, очки и пять узлов.
/// Клик открывает экран медитации.
/// </summary>
[DisallowMultipleComponent]
public class DaoHudBarUI : MonoBehaviour
{
    [Header("Размеры")]
    [SerializeField] private Vector2 barSize = new Vector2(72f, 420f);
    [SerializeField] private Vector2 margin = new Vector2(16f, 0f);
    [SerializeField, Min(20f)] private float barWidth = 18f;

    private DaoSystem dao;
    private CultivationSystem cultivation;
    private MeditationScreenUI meditationScreen;

    private Image background;
    private Image fill;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI levelLabel;
    private TextMeshProUGUI pointsLabel;
    private readonly Image[] nodes = new Image[5];

    /// <summary>Собирает полосу под указанным родителем.</summary>
    public void Build(DaoSystem daoSystem, CultivationSystem cultivationSystem,
        MeditationScreenUI screen, Transform parent)
    {
        dao = daoSystem;
        cultivation = cultivationSystem;
        meditationScreen = screen;

        var root = (RectTransform)transform;
        root.SetParent(parent, false);
        CultivationUIFactory.Place(root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            margin, barSize);

        background = CultivationUIFactory.CreatePanel("Background", root,
            new Color(0.05f, 0.06f, 0.08f, 0.85f));
        CultivationUIFactory.Stretch(background.rectTransform);

        var button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        button.onClick.AddListener(OpenMeditationScreen);

        titleLabel = CultivationUIFactory.CreateText("Dao", root, "Дао", 14f,
            CultivationUIFactory.Gold, TextAlignmentOptions.Top);
        CultivationUIFactory.Place(titleLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(4f, -8f), new Vector2(barSize.x - 8f, 36f));

        levelLabel = CultivationUIFactory.CreateText("Level", root, string.Empty, 13f,
            CultivationUIFactory.TextMain, TextAlignmentOptions.Center);
        CultivationUIFactory.Place(levelLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(4f, -46f), new Vector2(barSize.x - 8f, 20f));

        BuildBar(root);
        BuildNodes(root);

        Subscribe();
        Refresh();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void BuildBar(RectTransform root)
    {
        float barHeight = barSize.y - 190f;
        var barRect = CultivationUIFactory.CreateRect("Progress", root);
        CultivationUIFactory.Place(barRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -70f), new Vector2(barWidth, barHeight));

        var track = CultivationUIFactory.CreateImage("Track", barRect, new Color(0.02f, 0.03f, 0.04f, 0.95f));
        CultivationUIFactory.Stretch(track.rectTransform);
        CultivationUIFactory.AddGoldBorder(track, 0.35f, 1f);

        fill = CultivationUIFactory.CreateImage("Fill", barRect, CultivationUIFactory.Gold);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        var fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        pointsLabel = CultivationUIFactory.CreateText("Points", root, string.Empty, 11f,
            CultivationUIFactory.TextMuted, TextAlignmentOptions.Center);
        CultivationUIFactory.Place(pointsLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 96f), new Vector2(barSize.x - 6f, 18f));
    }

    private void BuildNodes(RectTransform root)
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            var node = CultivationUIFactory.CreateImage("Node " + i, root,
                CultivationUIFactory.GoldDim);
            CultivationUIFactory.Place(node.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 74f - i * 20f), new Vector2(14f, 14f));
            nodes[i] = node;
        }
    }

    private void Subscribe()
    {
        if (dao == null) return;

        dao.OnActiveDaoChanged -= HandleDaoChanged;
        dao.OnActiveDaoChanged += HandleDaoChanged;
        dao.OnDaoLevelChanged -= HandleLevelChanged;
        dao.OnDaoLevelChanged += HandleLevelChanged;
        dao.OnDaoPointsChanged -= HandlePointsChanged;
        dao.OnDaoPointsChanged += HandlePointsChanged;
        dao.OnDaoUnlocked -= HandleDaoChanged;
        dao.OnDaoUnlocked += HandleDaoChanged;
    }

    private void Unsubscribe()
    {
        if (dao == null) return;

        dao.OnActiveDaoChanged -= HandleDaoChanged;
        dao.OnDaoLevelChanged -= HandleLevelChanged;
        dao.OnDaoPointsChanged -= HandlePointsChanged;
        dao.OnDaoUnlocked -= HandleDaoChanged;
    }

    private void HandleDaoChanged(DaoType type) => Refresh();
    private void HandleLevelChanged(DaoType type, int level) => Refresh();
    private void HandlePointsChanged(DaoType type, int points, int cost) => Refresh();

    private void Refresh()
    {
        if (dao == null || titleLabel == null) return;

        DaoType? active = dao.ActiveDao;
        if (!active.HasValue)
        {
            titleLabel.text = "Дао\nне выбрано";
            levelLabel.text = string.Empty;
            pointsLabel.text = string.Empty;
            if (fill != null) fill.fillAmount = 0f;
            SetFillHeight(0f);
            RefreshNodes(DaoType.Sword);
            return;
        }

        DaoType type = active.Value;
        Color color = DaoTypeInfo.GetColor(type);

        titleLabel.text = DaoTypeInfo.GetDisplayName(type);
        int level = dao.GetLevel(type);
        levelLabel.text = "ур. " + level;

        int next = dao.GetPointsForNextLevel();
        int into = dao.GetPointsIntoLevel(type);
        pointsLabel.text = dao.IsMaxLevel(type) ? "макс" : into + "/" + next;

        if (fill != null) fill.color = color;
        SetFillHeight(dao.IsMaxLevel(type) || next <= 0 ? 1f : Mathf.Clamp01((float)into / next));
        RefreshNodes(type);
    }

    private void SetFillHeight(float normalized)
    {
        if (fill != null) fill.fillAmount = Mathf.Clamp01(normalized);
    }

    private void RefreshNodes(DaoType active)
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] == null) continue;

            DaoType type = DaoTypeInfo.All[i];
            bool unlocked = dao != null && dao.IsDaoUnlocked(type);
            if (unlocked && type == active) nodes[i].color = DaoTypeInfo.GetColor(type);
            else nodes[i].color = unlocked ? CultivationUIFactory.GoldDim : new Color(0.12f, 0.13f, 0.15f, 1f);
        }
    }

    private void OpenMeditationScreen()
    {
        if (meditationScreen == null) return;
        if (meditationScreen.IsOpen) meditationScreen.Close();
        else meditationScreen.Open();
    }
}
