using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Полноэкранная панель культивации (Tab / C): портрет, реалм, опыт, Прозрение,
/// пять созвездий Дао и кнопки «Медитировать / Прорыв / Техники».
/// </summary>
[DisallowMultipleComponent]
public class CultivationUI : MonoBehaviour
{
    private const int DaoCount = 5;

    private CultivationSystem cultivation;
    private DaoSystem dao;
    private MeditationController meditation;
    private BreakthroughSequence breakthrough;
    private CultivationConfig config;
    private TechniqueListUI techniqueList;
    private UiManager uiManager;
    private bool pausedByUs;

    private TextMeshProUGUI realmLabel;
    private TextMeshProUGUI stageLabel;
    private TextMeshProUGUI xpValueLabel;
    private TextMeshProUGUI comprehensionLabel;
    private TextMeshProUGUI bonusLabel;
    private TextMeshProUGUI hintLabel;
    private TextMeshProUGUI breakthroughLabel;
    private Slider xpBar;
    private Image portraitGlow;
    private Button breakthroughButton;

    private readonly Image[] cardBackgrounds = new Image[DaoCount];
    private readonly Outline[] cardOutlines = new Outline[DaoCount];
    private readonly TextMeshProUGUI[] cardLevels = new TextMeshProUGUI[DaoCount];
    private readonly List<Image>[] cardStars = new List<Image>[DaoCount];
    private readonly DaoType[] cardTypes = new DaoType[DaoCount];

    private float hintTimer;
    private float refreshTimer;

    public bool IsOpen { get; private set; }
    public event Action OnClosed;

    public void Build(CultivationSystem cultivationSystem, DaoSystem daoSystem, MeditationController meditationController,
        BreakthroughSequence breakthroughSequence, CultivationConfig cultivationConfig, TechniqueListUI techniques,
        UiManager manager)
    {
        cultivation = cultivationSystem;
        dao = daoSystem;
        meditation = meditationController;
        breakthrough = breakthroughSequence;
        config = cultivationConfig;
        techniqueList = techniques;
        uiManager = manager;

        for (int i = 0; i < DaoCount; i++)
            cardStars[i] = new List<Image>();

        BuildHierarchy();
        Subscribe();
        Refresh();
        gameObject.SetActive(false);
    }

    private void BuildHierarchy()
    {
        var root = (RectTransform)transform;
        CultivationUIFactory.Stretch(root);

        var dim = CultivationUIFactory.CreateImage("Dim", root, new Color(0.02f, 0.02f, 0.03f, 0.78f));
        CultivationUIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var windowImage = CultivationUIFactory.CreatePanel("Window", root, new Color(0.05f, 0.055f, 0.07f, 0.97f));
        CultivationUIFactory.Place(windowImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(1480f, 840f));

        var window = windowImage.rectTransform;
        AddInkTexture(window);

        var title = CultivationUIFactory.CreateText("Title", window, "КУЛЬТИВАЦИЯ", 36f, CultivationUIFactory.TextMain,
            TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(56f, -34f), new Vector2(700f, 48f));
        title.characterSpacing = 14f;

        var subtitle = CultivationUIFactory.CreateText("Subtitle", window, "ПУТЬ ДАО · ТИШИНА ВНУТРИ", 14f,
            CultivationUIFactory.TextMuted, TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(58f, -84f), new Vector2(700f, 22f));
        subtitle.characterSpacing = 6f;

        var closeButton = CultivationUIFactory.CreateButton("Close", window, "×", new Vector2(52f, 52f), Close,
            CultivationUIFactory.Gold, 30f);
        CultivationUIFactory.Place((RectTransform)closeButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-36f, -30f), new Vector2(52f, 52f));

        BuildPortrait(window);
        BuildDaoCards(window);
        BuildFooter(window);

        hintLabel = CultivationUIFactory.CreateText("Hint", window, string.Empty, 16f, CultivationUIFactory.Gold,
            TextAlignmentOptions.Center);
        CultivationUIFactory.Place(hintLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 24f), new Vector2(1200f, 26f));
    }

    private void AddInkTexture(RectTransform window)
    {
        var topLine = CultivationUIFactory.CreateImage("TopLine", window, new Color(0.78f, 0.64f, 0.36f, 0.35f));
        topLine.rectTransform.anchorMin = new Vector2(0f, 1f);
        topLine.rectTransform.anchorMax = new Vector2(1f, 1f);
        topLine.rectTransform.pivot = new Vector2(0.5f, 1f);
        topLine.rectTransform.offsetMin = new Vector2(40f, -6f);
        topLine.rectTransform.offsetMax = new Vector2(-40f, -3f);

        var separator = CultivationUIFactory.CreateImage("Separator", window, new Color(0.78f, 0.64f, 0.36f, 0.25f));
        CultivationUIFactory.Place(separator.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(56f, -492f), new Vector2(1368f, 2f));
    }

    private void BuildPortrait(RectTransform window)
    {
        var frame = CultivationUIFactory.CreateImage("PortraitFrame", window, new Color(0.03f, 0.035f, 0.045f, 0.95f));
        CultivationUIFactory.Place(frame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(56f, -140f), new Vector2(300f, 340f));
        CultivationUIFactory.AddGoldBorder(frame, 0.4f);

        portraitGlow = CultivationUIFactory.CreateImage("PortraitGlow", frame.rectTransform, new Color(1f, 1f, 1f, 0.18f),
            CultivationUIFactory.RadialSprite, false);
        CultivationUIFactory.Place(portraitGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 10f), new Vector2(320f, 320f));
        portraitGlow.raycastTarget = false;

        var portrait = CultivationUIFactory.CreateImage("Portrait", frame.rectTransform,
            new Color(0.82f, 0.84f, 0.86f, 0.9f), CultivationUIFactory.SilhouetteSprite, false);
        CultivationUIFactory.Place(portrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -30f), new Vector2(220f, 275f));
        portrait.raycastTarget = false;

        realmLabel = CultivationUIFactory.CreateText("Realm", window, "—", 30f, CultivationUIFactory.TextMain,
            TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(realmLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(56f, -500f), new Vector2(460f, 44f));

        stageLabel = CultivationUIFactory.CreateText("Stage", window, "—", 17f, CultivationUIFactory.TextMuted,
            TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(stageLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(58f, -544f), new Vector2(460f, 26f));

        var xpCaption = CultivationUIFactory.CreateText("XpCaption", window, "ОПЫТ ЦИ", 13f, CultivationUIFactory.GoldDim,
            TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(xpCaption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(58f, -588f), new Vector2(300f, 22f));
        xpCaption.characterSpacing = 4f;

        xpBar = CultivationUIFactory.CreateBar("XpBar", window, CultivationUIFactory.Gold, out _, out _);
        CultivationUIFactory.Place((RectTransform)xpBar.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(56f, -614f), new Vector2(440f, 26f));

        xpValueLabel = CultivationUIFactory.CreateText("XpValue", window, "0 / 0", 15f, CultivationUIFactory.TextMain,
            TextAlignmentOptions.TopRight);
        CultivationUIFactory.Place(xpValueLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(56f, -646f), new Vector2(440f, 24f));

        comprehensionLabel = CultivationUIFactory.CreateText("Comprehension", window, "Прозрение: 0", 18f,
            CultivationUIFactory.TextMain, TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(comprehensionLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(58f, -690f), new Vector2(460f, 26f));

        bonusLabel = CultivationUIFactory.CreateText("Bonuses", window, string.Empty, 14f, CultivationUIFactory.TextMuted,
            TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(bonusLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(58f, -722f), new Vector2(460f, 48f));
    }

    private void BuildDaoCards(RectTransform window)
    {
        var header = CultivationUIFactory.CreateText("DaoHeader", window, "СОЗВЕЗДИЯ ДАО", 13f, CultivationUIFactory.GoldDim,
            TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(520f, -120f), new Vector2(400f, 20f));
        header.characterSpacing = 6f;

        for (int i = 0; i < DaoCount; i++)
        {
            DaoType type = DaoTypeInfo.All[i];
            cardTypes[i] = type;

            Color color = ResolveColor(type);
            var card = CultivationUIFactory.CreateImage("Dao_" + type, window, new Color(0.07f, 0.075f, 0.09f, 0.85f));
            CultivationUIFactory.Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(520f + i * 186f, -146f), new Vector2(172f, 330f));
            cardOutlines[i] = card.gameObject.AddComponent<Outline>();
            cardOutlines[i].effectColor = new Color(color.r, color.g, color.b, 0.3f);
            cardOutlines[i].effectDistance = new Vector2(1.4f, -1.4f);
            cardOutlines[i].useGraphicAlpha = false;
            cardBackgrounds[i] = card;

            var cardButton = card.gameObject.AddComponent<Button>();
            cardButton.targetGraphic = card;
            cardButton.transition = Selectable.Transition.None;
            cardButton.navigation = new Navigation { mode = Navigation.Mode.None };
            DaoType captured = type;
            cardButton.onClick.AddListener(() => TryActivateDao(captured));

            var name = CultivationUIFactory.CreateText("Name", card.rectTransform, GetDaoName(type), 15f, color,
                TextAlignmentOptions.Top);
            CultivationUIFactory.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -16f), new Vector2(160f, 40f));

            if (config != null && config.ShowChineseNames)
            {
                var chinese = CultivationUIFactory.CreateText("Chinese", card.rectTransform, DaoTypeInfo.GetChineseName(type),
                    14f, new Color(color.r, color.g, color.b, 0.8f), TextAlignmentOptions.Top);
                CultivationUIFactory.Place(chinese.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -58f), new Vector2(160f, 20f));
                if (config.CalligraphyFont != null) chinese.font = config.CalligraphyFont;
            }

            for (int star = 0; star < 10; star++)
            {
                int row = star / 2;
                int column = star % 2;

                var starImage = CultivationUIFactory.CreateImage("Star_" + star, card.rectTransform,
                    new Color(0.2f, 0.2f, 0.24f, 0.8f), CultivationUIFactory.RadialSprite, false);
                CultivationUIFactory.Place(starImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-30f + column * 60f, -96f - row * 40f), new Vector2(30f, 30f));
                starImage.raycastTarget = false;
                cardStars[i].Add(starImage);
            }

            cardLevels[i] = CultivationUIFactory.CreateText("Level", card.rectTransform, "0 / 10", 15f,
                CultivationUIFactory.TextMain, TextAlignmentOptions.Bottom);
            CultivationUIFactory.Place(cardLevels[i].rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 32f), new Vector2(160f, 24f));

            var points = CultivationUIFactory.CreateText("Points", card.rectTransform, string.Empty, 12f,
                CultivationUIFactory.TextMuted, TextAlignmentOptions.Bottom);
            CultivationUIFactory.Place(points.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 12f), new Vector2(160f, 18f));
            points.name = "Points";
        }
    }

    private void BuildFooter(RectTransform window)
    {
        var meditateButton = CultivationUIFactory.CreateButton("Meditate", window, "Медитировать", new Vector2(296f, 64f),
            ToggleMeditation, CultivationUIFactory.Gold, 24f);
        CultivationUIFactory.Place((RectTransform)meditateButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(522f, 56f), new Vector2(296f, 64f));

        breakthroughButton = CultivationUIFactory.CreateButton("Breakthrough", window, "Прорыв", new Vector2(296f, 64f),
            StartBreakthrough, CultivationUIFactory.Hex("#E5533C"), 24f);
        CultivationUIFactory.Place((RectTransform)breakthroughButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(842f, 56f), new Vector2(296f, 64f));
        breakthroughLabel = breakthroughButton.GetComponentInChildren<TextMeshProUGUI>();

        var techniquesButton = CultivationUIFactory.CreateButton("Techniques", window, "Техники", new Vector2(296f, 64f),
            OpenTechniques, CultivationUIFactory.Gold, 24f);
        CultivationUIFactory.Place((RectTransform)techniquesButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(1162f, 56f), new Vector2(296f, 64f));
    }

    private void Subscribe()
    {
        if (cultivation != null)
        {
            cultivation.OnRealmChanged += HandleRealmChanged;
            cultivation.OnStageChanged += HandleStageChanged;
            cultivation.OnExperienceQiChanged += HandleExperienceChanged;
            cultivation.OnComprehensionChanged += HandleComprehensionChanged;
            cultivation.OnBreakthroughAttempted += HandleBreakthroughAttempted;
        }

        if (dao != null)
        {
            dao.OnActiveDaoChanged += HandleActiveDaoChanged;
            dao.OnDaoLevelChanged += HandleDaoLevelChanged;
            dao.OnDaoUnlocked += HandleDaoUnlocked;
            dao.OnDaoPointsChanged += HandleDaoPointsChanged;
        }

        if (breakthrough != null)
        {
            breakthrough.OnBreakthroughStarted += HandleSequenceStarted;
            breakthrough.OnBreakthroughResult += HandleSequenceResult;
        }
    }

    private void OnDestroy()
    {
        ResumeGame();

        if (cultivation != null)
        {
            cultivation.OnRealmChanged -= HandleRealmChanged;
            cultivation.OnStageChanged -= HandleStageChanged;
            cultivation.OnExperienceQiChanged -= HandleExperienceChanged;
            cultivation.OnComprehensionChanged -= HandleComprehensionChanged;
            cultivation.OnBreakthroughAttempted -= HandleBreakthroughAttempted;
        }

        if (dao != null)
        {
            dao.OnActiveDaoChanged -= HandleActiveDaoChanged;
            dao.OnDaoLevelChanged -= HandleDaoLevelChanged;
            dao.OnDaoUnlocked -= HandleDaoUnlocked;
            dao.OnDaoPointsChanged -= HandleDaoPointsChanged;
        }

        if (breakthrough != null)
        {
            breakthrough.OnBreakthroughStarted -= HandleSequenceStarted;
            breakthrough.OnBreakthroughResult -= HandleSequenceResult;
        }
    }

    private void HandleRealmChanged(CultivationRealm realm) { if (IsOpen) Refresh(); }
    private void HandleStageChanged(CultivationRealm realm, int stage) { if (IsOpen) Refresh(); }
    private void HandleExperienceChanged(int current, int max) { if (IsOpen) Refresh(); }
    private void HandleComprehensionChanged(int value) { if (IsOpen) Refresh(); }
    private void HandleBreakthroughAttempted(BreakthroughResult result) { if (IsOpen) Refresh(); }
    private void HandleActiveDaoChanged(DaoType type) { if (IsOpen) Refresh(); }
    private void HandleDaoLevelChanged(DaoType type, int level) { if (IsOpen) Refresh(); }
    private void HandleDaoUnlocked(DaoType type) { if (IsOpen) Refresh(); }
    private void HandleDaoPointsChanged(DaoType type, int points, int cost) { if (IsOpen) Refresh(); }
    private void HandleSequenceStarted() { if (IsOpen) Refresh(); }
    private void HandleSequenceResult(BreakthroughResult result, string message) { if (IsOpen) Refresh(); }

    public void Open()
    {
        if (uiManager != null && uiManager.IsOpen) return;

        IsOpen = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        PauseGame();
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        gameObject.SetActive(false);
        ResumeGame();
        OnClosed?.Invoke();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
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

        // Не снимаем паузу, если открыт другой UI (UiManager).
        if (uiManager == null || !uiManager.IsOpen)
            Time.timeScale = 1f;
    }

    private void Update()
    {
        if (!IsOpen) return;

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.2f;
            Refresh();
        }

        if (hintTimer > 0f)
        {
            hintTimer -= Time.unscaledDeltaTime;
            if (hintTimer <= 0f && hintLabel != null)
                hintLabel.text = string.Empty;
        }
    }

    private void Refresh()
    {
        if (cultivation == null || dao == null) return;

        var realm = cultivation.CurrentRealm;
        if (realmLabel != null) realmLabel.text = realm != null ? realm.RealmName : "—";

        if (stageLabel != null)
        {
            stageLabel.text = "Ступень " + cultivation.CurrentStage + " / " + cultivation.StagesInCurrentRealm +
                              "   ·   Реалм " + (cultivation.CurrentRealmIndex + 1) + " / " + cultivation.RealmsCount;
        }

        float experience = cultivation.ExperienceQi;
        float cap = cultivation.ExperienceQiToNextStage;
        if (xpBar != null) xpBar.value = cap <= 0f ? 0f : Mathf.Clamp01(experience / cap);
        if (xpValueLabel != null)
            xpValueLabel.text = Mathf.RoundToInt(experience) + " / " + Mathf.RoundToInt(cap);

        if (comprehensionLabel != null)
        {
            comprehensionLabel.text = "Прозрение: " + cultivation.Comprehension +
                                      "   ·   шанс прорыва " + Mathf.RoundToInt(cultivation.ComputeSuccessChance() * 100f) + "%";
        }

        if (bonusLabel != null)
        {
            bonusLabel.text = "Бонусы реалма: Ци +" + cultivation.TotalQiBonus +
                              "   ·   HP +" + cultivation.TotalHpBonus +
                              "   ·   Реген +" + cultivation.TotalRegenBonus.ToString("0.0");
        }

        if (portraitGlow != null && dao.ActiveDao.HasValue)
        {
            Color activeColor = ResolveColor(dao.ActiveDao.Value);
            portraitGlow.color = new Color(activeColor.r, activeColor.g, activeColor.b, 0.22f);
        }

        RefreshCards();
        RefreshButtons();
    }

    private void RefreshCards()
    {
        for (int i = 0; i < DaoCount; i++)
        {
            DaoType type = cardTypes[i];
            bool unlocked = dao.IsDaoUnlocked(type);
            bool active = dao.ActiveDao.HasValue && dao.ActiveDao.Value == type;
            int level = dao.GetLevel(type);
            int maxLevel = dao.GetMaxLevel(type);
            Color color = ResolveColor(type);

            if (cardLevels[i] != null)
                cardLevels[i].text = unlocked ? level + " / " + maxLevel : "закрыт";

            var pointsLabel = cardLevels[i] != null ? cardLevels[i].transform.parent.Find("Points") : null;
            if (pointsLabel != null)
            {
                var label = pointsLabel.GetComponent<TextMeshProUGUI>();
                if (label != null)
                    label.text = unlocked ? "очки " + dao.GetPoints(type) : "нет доступа";
            }

            for (int star = 0; star < cardStars[i].Count; star++)
            {
                var image = cardStars[i][star];
                if (image == null) continue;
                bool lit = unlocked && star < level;
                image.color = lit ? color : new Color(0.16f, 0.17f, 0.2f, 0.9f);
            }

            if (cardBackgrounds[i] != null)
            {
                cardBackgrounds[i].color = active
                    ? new Color(color.r * 0.22f + 0.05f, color.g * 0.22f + 0.05f, color.b * 0.22f + 0.06f, 0.95f)
                    : new Color(0.07f, 0.075f, 0.09f, 0.85f);
            }

            if (cardOutlines[i] != null)
            {
                float alpha = active ? 0.95f : (unlocked ? 0.32f : 0.1f);
                cardOutlines[i].effectColor = new Color(color.r, color.g, color.b, alpha);
            }
        }
    }

    private void RefreshButtons()
    {
        if (breakthroughButton == null) return;

        bool ready = cultivation.CanAttemptBreakthrough(out _);
        bool running = breakthrough != null && breakthrough.IsRunning;
        breakthroughButton.interactable = ready && !running;

        if (breakthroughLabel != null)
        {
            if (running) breakthroughLabel.text = "Прорыв...";
            else if (ready) breakthroughLabel.text = "Прорыв · " + Mathf.RoundToInt(cultivation.ComputeSuccessChance() * 100f) + "%";
            else breakthroughLabel.text = "Прорыв";
        }

    }

    private void ToggleMeditation()
    {
        if (meditation == null) return;

        if (meditation.IsMeditating)
        {
            meditation.ExitMeditation();
            return;
        }

        if (meditation.EnterMeditation())
            Close();
    }

    private void StartBreakthrough()
    {
        if (breakthrough == null) return;

        if (!breakthrough.StartBreakthrough())
        {
            ShowHint("Прорыв невозможен: " + GetBreakthroughReason());
            return;
        }

        Close();
    }

    private string GetBreakthroughReason()
    {
        if (cultivation == null) return "нет данных культивации";
        cultivation.CanAttemptBreakthrough(out string reason);
        return string.IsNullOrEmpty(reason) ? "неизвестная причина" : reason;
    }

    private void OpenTechniques()
    {
        if (techniqueList == null) return;
        techniqueList.Open();
    }

    private void TryActivateDao(DaoType type)
    {
        if (dao == null) return;

        if (!dao.IsDaoUnlocked(type))
        {
            ShowHint("Путь ещё не открыт. Разблокировка — через семена Дао (Промт B).");
            return;
        }

        if (!dao.CanSwitchDao)
        {
            ShowHint("Смена Дао доступна только у алтаря.");
            return;
        }

        if (dao.SetActiveDao(type))
        {
            ShowHint("Активный путь: " + GetDaoName(type));
            Refresh();
        }
    }

    private void ShowHint(string message)
    {
        if (hintLabel == null) return;
        hintLabel.text = message;
        hintTimer = 2.5f;
    }

    private string GetDaoName(DaoType type)
    {
        var path = dao != null ? dao.GetDaoPath(type) : null;
        return path != null ? path.DisplayName : DaoTypeInfo.GetDisplayName(type);
    }

    private Color ResolveColor(DaoType type)
    {
        var path = dao != null ? dao.GetDaoPath(type) : null;
        return path != null ? path.Color : DaoTypeInfo.GetColor(type);
    }
}
