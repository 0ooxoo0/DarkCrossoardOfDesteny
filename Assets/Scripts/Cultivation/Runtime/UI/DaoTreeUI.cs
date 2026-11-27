using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Экран дерева Дао: узлы колонками по ярусам, состояния «изучено / доступно / закрыто»,
/// цена в очках и предпосылки. Изучение — DaoSystem.TryUnlockTechnique.
/// </summary>
[DisallowMultipleComponent]
public class DaoTreeUI : MonoBehaviour
{
    [SerializeField] private Vector2 windowSize = new Vector2(1120f, 660f);
    [SerializeField, Min(90f)] private float nodeWidth = 232f;
    [SerializeField, Min(60f)] private float nodeHeight = 96f;

    private DaoSystem dao;
    private CultivationSystem cultivation;

    private RectTransform window;
    private RectTransform content;
    private TextMeshProUGUI headerLabel;
    private TextMeshProUGUI hintLabel;
    private Image[] daoBackgrounds;
    private Button[] daoButtons;

    private DaoType selectedDao = DaoType.Sword;
    private float hintTimer;
    private bool pausedByUs;

    public bool IsOpen { get; private set; }

    /// <summary>Собирает окно под указанным родителем (изначально скрыто).</summary>
    public void Build(DaoSystem daoSystem, CultivationSystem cultivationSystem, Transform parent)
    {
        dao = daoSystem;
        cultivation = cultivationSystem;

        var root = (RectTransform)transform;
        root.SetParent(parent, false);
        CultivationUIFactory.Stretch(root);

        var dim = CultivationUIFactory.CreateImage("Dim", root, new Color(0f, 0f, 0f, 0.55f));
        CultivationUIFactory.Stretch(dim.rectTransform);

        var windowRect = CultivationUIFactory.CreateRect("Window", root);
        CultivationUIFactory.Place(windowRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, windowSize);
        window = windowRect;

        var background = CultivationUIFactory.CreatePanel("Background", windowRect,
            new Color(0.07f, 0.08f, 0.1f, 0.97f));
        CultivationUIFactory.Stretch(background.rectTransform);

        var title = CultivationUIFactory.CreateText("Title", windowRect, "Древо Дао", 26f,
            CultivationUIFactory.Gold);
        CultivationUIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(24f, -18f), new Vector2(300f, 32f));

        headerLabel = CultivationUIFactory.CreateText("Header", windowRect, string.Empty, 14f,
            CultivationUIFactory.TextMuted);
        CultivationUIFactory.Place(headerLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(180f, -22f), new Vector2(620f, 24f));

        var close = CultivationUIFactory.CreateButton("Close", windowRect, "×",
            new Vector2(36f, 36f), Close, CultivationUIFactory.Danger, 22f);
        CultivationUIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-16f, -14f), new Vector2(36f, 36f));

        BuildDaoTabs(windowRect);
        BuildScroll(windowRect);

        hintLabel = CultivationUIFactory.CreateText("Hint", windowRect, string.Empty, 14f,
            CultivationUIFactory.Gold, TextAlignmentOptions.Center);
        CultivationUIFactory.Place(hintLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 10f), new Vector2(windowSize.x - 60f, 26f));

        gameObject.SetActive(false);
        Subscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void BuildDaoTabs(RectTransform parent)
    {
        daoBackgrounds = new Image[DaoTypeInfo.All.Length];
        daoButtons = new Button[DaoTypeInfo.All.Length];

        for (int i = 0; i < DaoTypeInfo.All.Length; i++)
        {
            DaoType type = DaoTypeInfo.All[i];
            var button = CultivationUIFactory.CreateButton("Dao " + type, parent,
                DaoTypeInfo.GetDisplayName(type), new Vector2(190f, 38f), () => SelectDao(type),
                DaoTypeInfo.GetColor(type), 15f);
            CultivationUIFactory.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f + i * 200f, -58f), new Vector2(190f, 38f));

            daoButtons[i] = button;
            daoBackgrounds[i] = button.GetComponent<Image>();
        }
    }

    private void BuildScroll(RectTransform parent)
    {
        var scrollRect = CultivationUIFactory.CreateRect("Scroll", parent);
        CultivationUIFactory.Place(scrollRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(24f, -108f), new Vector2(windowSize.x - 48f, windowSize.y - 136f));

        var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;

        var viewport = CultivationUIFactory.CreateRect("Viewport", scrollRect);
        CultivationUIFactory.Stretch(viewport);
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
        viewport.gameObject.AddComponent<RectMask2D>();

        content = CultivationUIFactory.CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        scroll.viewport = viewport;
        scroll.content = content;
    }

    public void Open()
    {
        IsOpen = true;
        gameObject.SetActive(true);
        GameMenuController.Instance?.OpenPanel(gameObject);
        transform.SetAsLastSibling();
        PauseGame();
        Rebuild();
    }

    /// <summary>Открытие из панели культивации: паузу уже держит она.</summary>
    public void OpenFromPanel()
    {
        IsOpen = true;
        gameObject.SetActive(true);
        GameMenuController.Instance?.OpenPanel(gameObject);
        transform.SetAsLastSibling();
        pausedByUs = false;
        Rebuild();
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        gameObject.SetActive(false);
        GameMenuController.Instance?.NotifyClosed(gameObject);
        ResumeGame();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void SelectDao(DaoType type)
    {
        selectedDao = type;
        if (IsOpen) Rebuild();
    }

    private void PauseGame()
    {
        if (Time.timeScale <= 0f) return;
        Time.timeScale = 0f;
        pausedByUs = true;
    }

    private void ResumeGame()
    {
        if (!pausedByUs) return;
        pausedByUs = false;
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (hintTimer <= 0f) return;

        hintTimer -= Time.unscaledDeltaTime;
        if (hintTimer <= 0f && hintLabel != null) hintLabel.text = string.Empty;
    }

    private void Rebuild()
    {
        if (content == null || dao == null) return;

        CultivationUIFactory.DestroyChildren(content);
        RefreshDaoTabs();

        List<TechniqueData> nodes = CollectTreeNodes();
        if (headerLabel != null)
        {
            headerLabel.text = DaoTypeInfo.GetDisplayName(selectedDao) + "   ·   узлов: " + nodes.Count +
                               "   ·   свободных очков: " + dao.GetFreePoints(selectedDao) +
                               (dao.IsDaoUnlocked(selectedDao) ? string.Empty : "   ·   Дао не открыт");
        }

        // Колонки по ярусам: ярус = rank. Внутри колонки узлы идут сверху вниз.
        var byRank = new Dictionary<int, List<TechniqueData>>();
        int maxRank = 1;

        for (int i = 0; i < nodes.Count; i++)
        {
            TechniqueData node = nodes[i];
            int rank = Mathf.Max(1, node.Rank);
            if (!byRank.TryGetValue(rank, out List<TechniqueData> list))
            {
                list = new List<TechniqueData>();
                byRank[rank] = list;
            }
            list.Add(node);
            maxRank = Mathf.Max(maxRank, rank);
        }

        float columnWidth = nodeWidth + 28f;
        float rowHeight = nodeHeight + 12f;
        int maxRows = 1;
        foreach (var pair in byRank) maxRows = Mathf.Max(maxRows, pair.Value.Count);

        content.sizeDelta = new Vector2(maxRank * columnWidth + 16f, maxRows * rowHeight + 16f);

        foreach (var pair in byRank)
        {
            int rank = pair.Key;
            for (int i = 0; i < pair.Value.Count; i++)
                BuildNode(pair.Value[i], rank, i, columnWidth, rowHeight);
        }
    }

    /// <summary>Узлы дерева выбранного Дао: базовые техники + узлы из префабов.</summary>
    private List<TechniqueData> CollectTreeNodes()
    {
        var result = new List<TechniqueData>();
        var path = dao.GetDaoPath(selectedDao);
        if (path == null) return result;

        var techniques = path.Techniques;
        for (int i = 0; i < techniques.Count; i++)
        {
            TechniqueData technique = techniques[i];
            if (technique == null || technique.Kind == TechniqueKind.Passive) continue;
            if (!result.Contains(technique)) result.Add(technique);
        }

        return result;
    }

    private void BuildNode(TechniqueData technique, int rank, int row, float columnWidth, float rowHeight)
    {
        bool learned = dao.HasLearnedTechnique(technique);
        bool prereqMet = dao.ArePrerequisitesMet(technique);
        bool affordable = dao.CanAfford(technique);
        bool unlockedDao = dao.IsDaoUnlocked(selectedDao);
        bool available = !learned && unlockedDao && prereqMet && affordable;

        Color accent = learned ? CultivationUIFactory.Success
            : available ? CultivationUIFactory.Gold
            : CultivationUIFactory.GoldDim;

        var card = CultivationUIFactory.CreateImage("Node " + technique.TechniqueId, content,
            new Color(0.1f, 0.11f, 0.14f, 0.95f));
        CultivationUIFactory.Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(8f + (rank - 1) * columnWidth, -8f - row * rowHeight),
            new Vector2(nodeWidth, nodeHeight));
        CultivationUIFactory.AddGoldBorder(card, learned ? 0.8f : available ? 0.5f : 0.2f, 1.2f);

        var rankLabel = CultivationUIFactory.CreateText("Rank", card.rectTransform, "ярус " + rank, 12f,
            CultivationUIFactory.TextMuted);
        CultivationUIFactory.Place(rankLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, -6f), new Vector2(120f, 18f));

        var name = CultivationUIFactory.CreateText("Name", card.rectTransform, technique.DisplayName, 16f,
            learned ? CultivationUIFactory.TextMain : accent, TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, -24f), new Vector2(nodeWidth - 20f, 24f));
        name.overflowMode = TextOverflowModes.Ellipsis;

        string costText = learned ? "Изучено" : technique.UnlockCost + " очк.";
        var cost = CultivationUIFactory.CreateText("Cost", card.rectTransform, costText, 13f, accent);
        CultivationUIFactory.Place(cost.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, -48f), new Vector2(nodeWidth - 20f, 18f));

        string status = learned ? string.Empty : dao.GetUnlockBlockReason(technique);
        if (!string.IsNullOrEmpty(status))
        {
            var statusLabel = CultivationUIFactory.CreateText("Status", card.rectTransform, status, 11f,
                CultivationUIFactory.Danger);
            CultivationUIFactory.Place(statusLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(10f, 8f), new Vector2(nodeWidth - 20f, 16f));
            statusLabel.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (learned) return;

        var learn = CultivationUIFactory.CreateButton("Learn", card.rectTransform, "Изучить",
            new Vector2(96f, 26f), () => Learn(technique), CultivationUIFactory.Gold, 13f);
        CultivationUIFactory.Place((RectTransform)learn.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-8f, 6f), new Vector2(96f, 26f));
        learn.interactable = available;
    }

    private void Learn(TechniqueData technique)
    {
        if (dao == null || technique == null) return;

        string reason = dao.GetUnlockBlockReason(technique);
        if (!string.IsNullOrEmpty(reason))
        {
            ShowHint(technique.DisplayName + ": " + reason);
            return;
        }

        if (dao.TryUnlockTechnique(technique))
            ShowHint("Изучено: " + technique.DisplayName + " (−" + technique.UnlockCost + " очк.)");
        else
            ShowHint("Не удалось изучить: " + technique.DisplayName);

        if (IsOpen) Rebuild();
    }

    private void ShowHint(string message)
    {
        if (hintLabel == null) return;
        hintLabel.text = message;
        hintTimer = 4f;
    }

    private void RefreshDaoTabs()
    {
        if (daoBackgrounds == null) return;

        for (int i = 0; i < daoBackgrounds.Length; i++)
        {
            if (daoBackgrounds[i] == null) continue;

            bool selected = DaoTypeInfo.All[i] == selectedDao;
            bool unlocked = dao != null && dao.IsDaoUnlocked(DaoTypeInfo.All[i]);
            Color color = DaoTypeInfo.GetColor(DaoTypeInfo.All[i]);

            daoBackgrounds[i].color = selected
                ? new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 1f)
                : unlocked ? new Color(0.14f, 0.15f, 0.18f, 1f) : new Color(0.09f, 0.09f, 0.1f, 1f);
        }
    }

    private void Subscribe()
    {
        if (dao == null) return;

        dao.OnTechniqueLearned -= HandleTechniqueChanged;
        dao.OnTechniqueLearned += HandleTechniqueChanged;
        dao.OnDaoLevelChanged -= HandleDaoLevelChanged;
        dao.OnDaoLevelChanged += HandleDaoLevelChanged;
        dao.OnDaoUnlocked -= HandleDaoUnlocked;
        dao.OnDaoUnlocked += HandleDaoUnlocked;
        dao.OnDaoPointsChanged -= HandlePointsChanged;
        dao.OnDaoPointsChanged += HandlePointsChanged;
    }

    private void Unsubscribe()
    {
        if (dao == null) return;

        dao.OnTechniqueLearned -= HandleTechniqueChanged;
        dao.OnDaoLevelChanged -= HandleDaoLevelChanged;
        dao.OnDaoUnlocked -= HandleDaoUnlocked;
        dao.OnDaoPointsChanged -= HandlePointsChanged;
    }

    private void HandleTechniqueChanged(TechniqueData technique)
    {
        if (IsOpen) Rebuild();
    }

    private void HandleDaoLevelChanged(DaoType type, int level)
    {
        if (IsOpen) Rebuild();
    }

    private void HandleDaoUnlocked(DaoType type)
    {
        if (IsOpen) Rebuild();
    }

    private void HandlePointsChanged(DaoType type, int points, int cost)
    {
        if (IsOpen) Rebuild();
    }
}
