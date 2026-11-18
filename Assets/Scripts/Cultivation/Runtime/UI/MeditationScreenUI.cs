using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Экран медитации: реалм, опыт, Прозрение, безопасность места, накопления сессии,
/// дебафы и кнопка «Медитировать»/«Прервать». Паузу не ставит, в стек меню не входит.
/// Esc закрывает экран через <see cref="GameMenuController"/>.
/// </summary>
[DisallowMultipleComponent]
public class MeditationScreenUI : MonoBehaviour
{
    [Header("Размеры")]
    [SerializeField] private Vector2 windowSize = new Vector2(880f, 700f);
    [SerializeField, Min(3f)] private float refreshInterval = 0.2f;

    private MeditationController meditation;
    private CultivationSystem cultivation;
    private DaoSystem dao;
    private CultivationDebuffController debuffs;
    private MeditationRiskConfig risk;

    private TextMeshProUGUI realmLabel;    private TextMeshProUGUI experienceLabel;
    private TextMeshProUGUI comprehensionLabel;
    private TextMeshProUGUI zoneLabel;
    private TextMeshProUGUI sessionLabel;
    private TextMeshProUGUI multiplierLabel;
    private TextMeshProUGUI debuffLabel;
    private TextMeshProUGUI reasonLabel;
    private TextMeshProUGUI buttonLabel;
    private Button actionButton;

    private float refreshTimer;

    public bool IsOpen { get; private set; }

    /// <summary>Собирает экран под указанным родителем (изначально скрыто).</summary>
    public void Build(MeditationController meditationController, CultivationSystem cultivationSystem,
        DaoSystem daoSystem, CultivationDebuffController debuffController,
        MeditationRiskConfig riskConfig, Transform parent)
    {
        meditation = meditationController;
        cultivation = cultivationSystem;
        dao = daoSystem;
        debuffs = debuffController;
        risk = riskConfig;

        var root = (RectTransform)transform;
        root.SetParent(parent, false);
        CultivationUIFactory.Stretch(root);

        var dim = CultivationUIFactory.CreateImage("Dim", root, new Color(0f, 0f, 0f, 0.6f));
        CultivationUIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var windowRect = CultivationUIFactory.CreateRect("Window", root);
        CultivationUIFactory.Place(windowRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, windowSize);

        var background = CultivationUIFactory.CreatePanel("Background", windowRect,
            new Color(0.07f, 0.08f, 0.1f, 0.97f));
        CultivationUIFactory.Stretch(background.rectTransform);

        var title = CultivationUIFactory.CreateText("Title", windowRect, "Медитация", 26f,
            CultivationUIFactory.Gold);
        CultivationUIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(24f, -18f), new Vector2(400f, 32f));

        var close = CultivationUIFactory.CreateButton("Close", windowRect, "×",
            new Vector2(36f, 36f), Close, CultivationUIFactory.Danger, 22f);
        CultivationUIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-16f, -14f), new Vector2(36f, 36f));

        realmLabel = CreateLine(windowRect, "Realm", 22f, CultivationUIFactory.TextMain, -70f);
        experienceLabel = CreateLine(windowRect, "Experience", 16f, CultivationUIFactory.TextMuted, -104f);
        comprehensionLabel = CreateLine(windowRect, "Comprehension", 16f, CultivationUIFactory.TextMuted, -132f);

        zoneLabel = CreateLine(windowRect, "Zone", 17f, CultivationUIFactory.Success, -170f);
        multiplierLabel = CreateLine(windowRect, "Multiplier", 15f, CultivationUIFactory.TextMuted, -198f);

        sessionLabel = CreateBox(windowRect, "Session", -232f, 180f, CultivationUIFactory.TextMain);

        debuffLabel = CreateBox(windowRect, "Debuffs", -424f, 138f, CultivationUIFactory.TextMuted);

        reasonLabel = CultivationUIFactory.CreateText("Reason", windowRect, string.Empty, 15f,
            CultivationUIFactory.Danger, TextAlignmentOptions.Center);
        CultivationUIFactory.Place(reasonLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 92f), new Vector2(windowSize.x - 80f, 44f));

        actionButton = CultivationUIFactory.CreateButton("Action", windowRect, "Медитировать",
            new Vector2(280f, 54f), ToggleMeditation, CultivationUIFactory.Gold, 20f);
        CultivationUIFactory.Place((RectTransform)actionButton.transform, new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(280f, 54f));
        buttonLabel = actionButton.GetComponentInChildren<TextMeshProUGUI>(true);

        var hint = CultivationUIFactory.CreateText("Hint", windowRect, "Esc — закрыть экран",
            13f, CultivationUIFactory.TextMuted, TextAlignmentOptions.Center);
        CultivationUIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 4f), new Vector2(400f, 18f));

        Subscribe();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    public void Open()
    {
        IsOpen = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        gameObject.SetActive(false);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    private TextMeshProUGUI CreateLine(RectTransform parent, string name, float fontSize, Color color, float y)
    {
        var label = CultivationUIFactory.CreateText(name, parent, string.Empty, fontSize, color,
            TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(28f, y), new Vector2(windowSize.x - 56f, 28f));
        return label;
    }

    private TextMeshProUGUI CreateBox(RectTransform parent, string name, float y, float height, Color color)
    {
        var label = CultivationUIFactory.CreateText(name, parent, string.Empty, 15f, color,
            TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(28f, y), new Vector2(windowSize.x - 56f, height));
        return label;
    }

    private void Subscribe()
    {
        if (meditation != null)
        {
            meditation.OnMeditationStarted -= HandleMeditationChanged;
            meditation.OnMeditationStopped -= HandleMeditationChanged;
            meditation.OnMeditationInterrupted -= HandleMeditationChanged;
            meditation.OnMeditationTick -= HandleTick;

            meditation.OnMeditationStarted += HandleMeditationChanged;
            meditation.OnMeditationStopped += HandleMeditationChanged;
            meditation.OnMeditationInterrupted += HandleMeditationChanged;
            meditation.OnMeditationTick += HandleTick;
        }

        if (debuffs != null)
        {
            debuffs.OnDebuffApplied -= HandleDebuffChanged;
            debuffs.OnDebuffExpired -= HandleDebuffChanged;
            debuffs.OnSeverityChanged -= HandleSeverityChanged;

            debuffs.OnDebuffApplied += HandleDebuffChanged;
            debuffs.OnDebuffExpired += HandleDebuffChanged;
            debuffs.OnSeverityChanged += HandleSeverityChanged;
        }

        if (cultivation != null)
        {
            cultivation.OnRealmChanged -= HandleRealmChanged;
            cultivation.OnStageChanged -= HandleStageChanged;
            cultivation.OnExperienceQiChanged -= HandleExperienceChanged;
            cultivation.OnComprehensionChanged -= HandleComprehensionChanged;

            cultivation.OnRealmChanged += HandleRealmChanged;
            cultivation.OnStageChanged += HandleStageChanged;
            cultivation.OnExperienceQiChanged += HandleExperienceChanged;
            cultivation.OnComprehensionChanged += HandleComprehensionChanged;
        }
    }

    private void Unsubscribe()
    {
        if (meditation != null)
        {
            meditation.OnMeditationStarted -= HandleMeditationChanged;
            meditation.OnMeditationStopped -= HandleMeditationChanged;
            meditation.OnMeditationInterrupted -= HandleMeditationChanged;
            meditation.OnMeditationTick -= HandleTick;
        }

        if (debuffs != null)
        {
            debuffs.OnDebuffApplied -= HandleDebuffChanged;
            debuffs.OnDebuffExpired -= HandleDebuffChanged;
            debuffs.OnSeverityChanged -= HandleSeverityChanged;
        }

        if (cultivation != null)
        {
            cultivation.OnRealmChanged -= HandleRealmChanged;
            cultivation.OnStageChanged -= HandleStageChanged;
            cultivation.OnExperienceQiChanged -= HandleExperienceChanged;
            cultivation.OnComprehensionChanged -= HandleComprehensionChanged;
        }
    }

    private void HandleMeditationChanged()
    {
        if (IsOpen) Refresh();
    }

    private void HandleTick(int experience, int points, float multiplier)
    {
        if (IsOpen) Refresh();
    }

    private void HandleDebuffChanged(ActiveCultivationDebuff debuff)
    {
        if (IsOpen) Refresh();
    }

    private void HandleSeverityChanged(int severity)
    {
        if (IsOpen) Refresh();
    }

    private void HandleRealmChanged(CultivationRealm realm)
    {
        if (IsOpen) Refresh();
    }

    private void HandleStageChanged(CultivationRealm realm, int stage)
    {
        if (IsOpen) Refresh();
    }

    private void HandleExperienceChanged(int current, int max)
    {
        if (IsOpen) Refresh();
    }

    private void HandleComprehensionChanged(int value)
    {
        if (IsOpen) Refresh();
    }

    private void Update()
    {
        if (!IsOpen) return;

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;

        refreshTimer = refreshInterval;
        Refresh();
    }

    private void ToggleMeditation()
    {
        if (meditation == null) return;

        if (meditation.IsMeditating)
        {
            meditation.Interrupt();
            return;
        }

        if (IsBlocked(out string reason))
        {
            reasonLabel.text = reason;
            return;
        }

        if (!meditation.EnterMeditation())
            reasonLabel.text = "Не удалось начать медитацию";

        Refresh();
    }

    private bool IsBlocked(out string reason)
    {
        reason = string.Empty;
        if (debuffs == null) return false;

        if (debuffs.QiRegenMultiplier <= 0f)
        {
            reason = "Ци не восстанавливается — медитация невозможна";
            return true;
        }

        if (debuffs.MeditationBlockRemaining > 0f)
        {
            reason = "Медитация недоступна ещё " +
                     Mathf.CeilToInt(debuffs.MeditationBlockRemaining) + " с";
            return true;
        }

        return false;
    }

    private void Refresh()
    {
        if (meditation == null || cultivation == null) return;

        RefreshRealm();
        RefreshZone();
        RefreshSession();
        RefreshDebuffs();
        RefreshButton();
    }

    private void RefreshRealm()
    {
        CultivationRealm realm = cultivation.CurrentRealm;
        string realmName = realm != null ? realm.RealmName : "Неизвестно";
        if (realmLabel != null)
            realmLabel.text = realmName + " · ступень " + cultivation.CurrentStage +
                              "/" + cultivation.StagesInCurrentRealm;

        if (experienceLabel != null)
        {
            int current = cultivation.ExperienceQi;
            int next = cultivation.ExperienceQiToNextStageInt;
            experienceLabel.text = cultivation.IsAtMaxStage
                ? "Опыт Ци: " + current + " (предел)"
                : "Опыт Ци: " + current + "/" + next + "   (" +
                  Mathf.RoundToInt(cultivation.ExperienceNormalized * 100f) + "%)";
        }

        if (comprehensionLabel != null)
            comprehensionLabel.text = "Прозрение: " + cultivation.Comprehension +
                                      "   ·   шанс прорыва " +
                                      Mathf.RoundToInt(cultivation.ComputeSuccessChance() * 100f) + "%";
    }

    private void RefreshZone()
    {
        bool safe = meditation.IsInSafeZone;
        float multiplier = meditation.CurrentMultiplier;

        if (zoneLabel != null)
        {
            zoneLabel.text = safe
                ? "Безопасное место · множитель ×" + multiplier.ToString("0.0")
                : "Опасная зона · риск прерывания";
            zoneLabel.color = safe ? CultivationUIFactory.Success : CultivationUIFactory.Danger;
        }

        if (multiplierLabel != null)
        {
            multiplierLabel.text = "В секунду: опыт +" + Mathf.RoundToInt(meditation.ExperiencePerSecond) +
                                   "   ·   Дао +" + meditation.DaoPointsPerSecond.ToString("0.#") +
                                   "   ·   Ци +" + meditation.QiRestorePerSecond.ToString("0.#");

            if (dao != null && dao.ActiveDao.HasValue)
                multiplierLabel.text += "   ·   путь " + DaoTypeInfo.GetDisplayName(dao.ActiveDao.Value);

            if (risk != null)
                multiplierLabel.text += "   ·   радиус угрозы " + risk.ThreatRadius.ToString("0.#") + " м";
        }
    }

    private void RefreshSession()
    {
        if (sessionLabel == null) return;

        if (!meditation.IsMeditating)
        {
            MeditationSession snapshot = meditation.Session;
            if (snapshot.AccumulatedExperience > 0 || snapshot.AccumulatedDaoPoints > 0)
            {
                sessionLabel.text = "Прошлая медитация:\n" +
                                    "  опыт Ци +" + snapshot.AccumulatedExperience + "\n" +
                                    "  Дао +" + snapshot.AccumulatedDaoPoints + "\n" +
                                    "  Прозрение +" + snapshot.AccumulatedComprehension;
            }
            else
            {
                sessionLabel.text = "Медитация не начата.\n" +
                                    (meditation.IsInSafeZone
                                        ? "В безопасном месте риск отсутствует."
                                        : "Вне безопасного места прерывание оставит последствия.");
            }

            return;
        }

        MeditationSession session = meditation.Session;
        float seconds = session.IsActive ? Time.time - session.StartedAt : 0f;

        sessionLabel.text = "Идёт медитация · " + Mathf.FloorToInt(seconds / 60f) + ":" +
                            Mathf.FloorToInt(seconds % 60f).ToString("00") + "\n" +
                            "  опыт Ци +" + session.AccumulatedExperience + "\n" +
                            "  Дао +" + session.AccumulatedDaoPoints + "\n" +
                            "  Прозрение +" + session.AccumulatedComprehension;
    }

    private void RefreshDebuffs()
    {
        if (debuffLabel == null) return;

        if (debuffs == null)
        {
            debuffLabel.text = "Дебафы недоступны.";
            debuffLabel.color = CultivationUIFactory.TextMuted;
            return;
        }

        IReadOnlyList<ActiveCultivationDebuff> active = debuffs.Active;
        if (active == null || active.Count == 0)
        {
            debuffLabel.text = debuffs.Severity > 0
                ? "Стадии последствий: " + debuffs.Severity + " (дебаф спал)"
                : "Активных дебафов нет.";
            debuffLabel.color = CultivationUIFactory.Success;
            return;
        }

        debuffLabel.color = CultivationUIFactory.Danger;

        var builder = new System.Text.StringBuilder(256);
        builder.AppendLine("Последствия (" + debuffs.Severity + "/" + StageCount + "):");
        for (int i = 0; i < active.Count; i++)
        {
            ActiveCultivationDebuff debuff = active[i];
            if (debuff == null || debuff.Stage == null) continue;

            builder.AppendLine("  " + debuff.Stage.DisplayName + " · " +
                               Mathf.CeilToInt(debuff.Remaining) + " с");
            builder.AppendLine("    " + DescribeStage(debuff.Stage));
        }

        debuffLabel.text = builder.ToString();
    }

    private int StageCount => risk != null && risk.Stages != null ? risk.Stages.Count : 3;

    private static string DescribeStage(CultivationFailureStage stage)
    {
        return "реген Ци ×" + stage.QiRegenMultiplier.ToString("0.00") +
               " · урон ×" + stage.DamageDealtMultiplier.ToString("0.00") +
               " · получаемый урон ×" + stage.DamageTakenMultiplier.ToString("0.00") +
               " · HP ×" + stage.MaxHealthMultiplier.ToString("0.00");
    }

    private void RefreshButton()
    {
        if (actionButton == null) return;

        bool meditating = meditation.IsMeditating;
        string reason = string.Empty;
        bool blocked = !meditating && IsBlocked(out reason);

        if (buttonLabel != null)
            buttonLabel.text = meditating ? "Прервать" : "Медитировать";

        actionButton.interactable = !blocked;

        var image = actionButton.targetGraphic as Image;
        if (image != null)
        {
            Color accent = meditating ? CultivationUIFactory.Danger : CultivationUIFactory.Gold;
            image.color = new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 1f);
        }

        if (reasonLabel != null)
            reasonLabel.text = blocked ? reason : string.Empty;
    }
}
