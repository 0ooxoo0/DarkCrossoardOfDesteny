using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Нижняя панель быстрых техник 1–5 (демо-исполнитель, пока нет AbilitySystem).
/// Пустой слот открывает список техник; занятый — кастует.
/// </summary>
[DisallowMultipleComponent]
public class CultivationQuickBarUI : MonoBehaviour
{
    private const int Slots = CultivationCastController.SlotCount;

    private CultivationCastController caster;
    private TechniqueListUI techniques;
    private DaoSystem dao;

    private readonly Image[] backgrounds = new Image[Slots];
    private readonly Outline[] outlines = new Outline[Slots];
    private readonly TextMeshProUGUI[] names = new TextMeshProUGUI[Slots];
    private readonly TextMeshProUGUI[] cooldowns = new TextMeshProUGUI[Slots];
    private readonly TextMeshProUGUI[] costs = new TextMeshProUGUI[Slots];

    private TextMeshProUGUI toastLabel;
    private float toastTimer;
    private float refreshTimer;

    public void Build(CultivationCastController castController, TechniqueListUI techniqueList, DaoSystem daoSystem)
    {
        caster = castController;
        techniques = techniqueList;
        dao = daoSystem;

        BuildHierarchy();

        if (caster != null)
        {
            caster.OnSlotChanged += HandleSlotChanged;
            caster.OnCast += HandleCast;
            caster.OnMessage += HandleMessage;
        }

        Refresh();
    }

    private void OnDestroy()
    {
        if (caster == null) return;
        caster.OnSlotChanged -= HandleSlotChanged;
        caster.OnCast -= HandleCast;
        caster.OnMessage -= HandleMessage;
    }

    private void BuildHierarchy()
    {
        var root = (RectTransform)transform;
        CultivationUIFactory.Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 34f), new Vector2(628f, 150f));

        var hint = CultivationUIFactory.CreateText("Hint", root, "1–5 — техники   ·   Tab — культивация   ·   M — медитация",
            13f, CultivationUIFactory.TextMuted, TextAlignmentOptions.Bottom);
        CultivationUIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 124f), new Vector2(700f, 22f));

        for (int i = 0; i < Slots; i++)
        {
            int index = i;

            var slot = CultivationUIFactory.CreateImage("Slot_" + (i + 1), root, new Color(0.06f, 0.065f, 0.08f, 0.92f));
            CultivationUIFactory.Place(slot.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(i * 126f, 0f), new Vector2(116f, 116f));
            backgrounds[i] = slot;

            outlines[i] = slot.gameObject.AddComponent<Outline>();
            outlines[i].effectColor = new Color(CultivationUIFactory.Gold.r, CultivationUIFactory.Gold.g, CultivationUIFactory.Gold.b, 0.35f);
            outlines[i].effectDistance = new Vector2(1.4f, -1.4f);
            outlines[i].useGraphicAlpha = false;

            var button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = slot;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => HandleSlotClicked(index));

            var key = CultivationUIFactory.CreateText("Key", slot.rectTransform, (i + 1).ToString(), 13f,
                CultivationUIFactory.Gold, TextAlignmentOptions.TopLeft);
            CultivationUIFactory.Place(key.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(8f, -6f), new Vector2(30f, 20f));

            names[i] = CultivationUIFactory.CreateText("Name", slot.rectTransform, string.Empty, 14f,
                CultivationUIFactory.TextMain, TextAlignmentOptions.Center);
            CultivationUIFactory.Place(names[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 2f), new Vector2(106f, 54f));

            costs[i] = CultivationUIFactory.CreateText("Cost", slot.rectTransform, string.Empty, 11f,
                CultivationUIFactory.TextMuted, TextAlignmentOptions.Bottom);
            CultivationUIFactory.Place(costs[i].rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 6f), new Vector2(108f, 18f));

            cooldowns[i] = CultivationUIFactory.CreateText("Cooldown", slot.rectTransform, string.Empty, 26f,
                CultivationUIFactory.TextMain, TextAlignmentOptions.Center);
            CultivationUIFactory.Place(cooldowns[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(110f, 60f));
        }

        toastLabel = CultivationUIFactory.CreateText("Toast", root, string.Empty, 15f, CultivationUIFactory.Gold,
            TextAlignmentOptions.Bottom);
        CultivationUIFactory.Place(toastLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 150f), new Vector2(900f, 26f));
    }

    private void HandleSlotClicked(int index)
    {
        if (caster == null) return;

        if (caster.GetSlot(index) == null)
        {
            if (techniques != null) techniques.Open();
            return;
        }

        caster.TryCast(index);
    }

    private void HandleSlotChanged(int index, TechniqueData technique)
    {
        Refresh();
        if (technique != null)
            ShowToast(technique.DisplayName + " → слот " + (index + 1));
    }

    private void HandleCast(int index, TechniqueData technique) => Refresh();

    private void HandleMessage(string message) => ShowToast(message);

    private void ShowToast(string message)
    {
        if (toastLabel == null) return;
        toastLabel.text = message;
        toastTimer = 2.5f;
    }

    private void Update()
    {
        if (caster != null && caster.IsSuppressed)
        {
            gameObject.SetActive(false);
            return;
        }

        if (toastTimer > 0f)
        {
            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer <= 0f && toastLabel != null)
                toastLabel.text = string.Empty;
        }

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;

        refreshTimer = 0.1f;
        RefreshCooldowns();
    }

    private void Refresh()
    {
        if (caster == null) return;

        for (int i = 0; i < Slots; i++)
        {
            TechniqueData technique = caster.GetSlot(i);
            bool hasTechnique = technique != null;
            Color accent = hasTechnique ? ResolveColor(technique.DaoType) : CultivationUIFactory.GoldDim;

            if (names[i] != null)
                names[i].text = hasTechnique ? technique.DisplayName : "пусто";
            if (names[i] != null)
                names[i].color = hasTechnique ? CultivationUIFactory.TextMain : CultivationUIFactory.TextMuted;

            if (costs[i] != null)
                costs[i].text = hasTechnique ? "Ци " + technique.QiCost + " · КД " + technique.Cooldown.ToString("0.#") : string.Empty;

            if (backgrounds[i] != null)
                backgrounds[i].color = hasTechnique
                    ? new Color(accent.r * 0.16f + 0.05f, accent.g * 0.16f + 0.05f, accent.b * 0.16f + 0.06f, 0.95f)
                    : new Color(0.06f, 0.065f, 0.08f, 0.92f);

            if (outlines[i] != null)
                outlines[i].effectColor = new Color(accent.r, accent.g, accent.b, hasTechnique ? 0.75f : 0.25f);
        }

        RefreshCooldowns();
    }

    private void RefreshCooldowns()
    {
        if (caster == null) return;

        for (int i = 0; i < Slots; i++)
        {
            float remaining = caster.GetCooldown(i);
            if (cooldowns[i] == null) continue;

            cooldowns[i].text = remaining > 0.05f ? remaining.ToString("0.0") : string.Empty;
            cooldowns[i].color = new Color(1f, 1f, 1f, 0.85f);
        }
    }

    private Color ResolveColor(DaoType type)
    {
        if (dao != null)
        {
            var path = dao.GetDaoPath(type);
            if (path != null) return path.Color;
        }
        return DaoTypeInfo.GetColor(type);
    }
}
