using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Модальная панель прорыва: прогресс-бар, шанс, результат.
/// </summary>
[DisallowMultipleComponent]
public class BreakthroughUI : MonoBehaviour
{
    private BreakthroughSequence breakthrough;
    private CultivationSystem cultivation;

    private Slider progressBar;
    private TextMeshProUGUI statusLabel;
    private TextMeshProUGUI chanceLabel;
    private TextMeshProUGUI realmLabel;
    private Button cancelButton;

    private Coroutine hideRoutine;

    public void Build(BreakthroughSequence sequence, CultivationSystem cultivationSystem)
    {
        breakthrough = sequence;
        cultivation = cultivationSystem;

        BuildHierarchy();
        Subscribe();
        gameObject.SetActive(false);
    }

    private void BuildHierarchy()
    {
        var root = (RectTransform)transform;
        CultivationUIFactory.Stretch(root);

        var dim = CultivationUIFactory.CreateImage("Dim", root, new Color(0.02f, 0.02f, 0.03f, 0.85f));
        CultivationUIFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var windowImage = CultivationUIFactory.CreatePanel("Window", root, new Color(0.05f, 0.055f, 0.07f, 0.98f));
        CultivationUIFactory.Place(windowImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(780f, 340f));
        var window = windowImage.rectTransform;

        var title = CultivationUIFactory.CreateText("Title", window, "ПРОРЫВ", 34f, CultivationUIFactory.TextMain,
            TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(40f, -28f), new Vector2(500f, 44f));
        title.characterSpacing = 14f;

        realmLabel = CultivationUIFactory.CreateText("Realm", window, string.Empty, 16f, CultivationUIFactory.TextMuted,
            TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(realmLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(42f, -74f), new Vector2(700f, 24f));

        progressBar = CultivationUIFactory.CreateBar("Progress", window, CultivationUIFactory.Hex("#E5533C"), out _, out _);
        CultivationUIFactory.Place((RectTransform)progressBar.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(40f, -122f), new Vector2(700f, 34f));

        statusLabel = CultivationUIFactory.CreateText("Status", window, string.Empty, 18f, CultivationUIFactory.TextMain,
            TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(statusLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(42f, -168f), new Vector2(700f, 60f));

        chanceLabel = CultivationUIFactory.CreateText("Chance", window, string.Empty, 14f, CultivationUIFactory.TextMuted,
            TextAlignmentOptions.TopLeft);
        CultivationUIFactory.Place(chanceLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(42f, -232f), new Vector2(700f, 50f));

        cancelButton = CultivationUIFactory.CreateButton("Cancel", window, "Прервать", new Vector2(200f, 50f),
            CancelSequence, CultivationUIFactory.Danger, 18f);
        CultivationUIFactory.Place((RectTransform)cancelButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-40f, 20f), new Vector2(200f, 50f));
    }

    private void Subscribe()
    {
        if (breakthrough == null) return;
        breakthrough.OnBreakthroughStarted += HandleStarted;
        breakthrough.OnBreakthroughProgress += HandleProgress;
        breakthrough.OnBreakthroughResult += HandleResult;
    }

    private void OnDestroy()
    {
        if (breakthrough == null) return;
        breakthrough.OnBreakthroughStarted -= HandleStarted;
        breakthrough.OnBreakthroughProgress -= HandleProgress;
        breakthrough.OnBreakthroughResult -= HandleResult;
    }

    private void HandleStarted()
    {
        StopHideRoutine();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (progressBar != null) progressBar.value = 0f;
        if (statusLabel != null)
        {
            statusLabel.text = "Ци сжимается в даньтяне...";
            statusLabel.color = CultivationUIFactory.TextMain;
        }

        if (chanceLabel != null)
        {
            int chance = Mathf.RoundToInt(breakthrough.LastChance * 100f);
            chanceLabel.text = "Шанс успеха: " + chance + "%   ·   бонус Прозрения учтён";
        }

        if (realmLabel != null && cultivation != null)
            realmLabel.text = cultivation.GetProgressLabel();

        if (cancelButton != null)
            cancelButton.gameObject.SetActive(breakthrough.CanBeCancelled);
    }

    private void HandleProgress(float progress)
    {
        if (progressBar != null) progressBar.value = progress;

        if (statusLabel != null)
        {
            int percent = Mathf.RoundToInt(progress * 100f);
            statusLabel.text = "Прорыв... " + percent + "%";
        }
    }

    private void HandleResult(BreakthroughResult result, string message)
    {
        StopHideRoutine();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (progressBar != null) progressBar.value = result == BreakthroughResult.NotReady ? 0f : 1f;

        if (statusLabel != null)
        {
            statusLabel.text = message;
            statusLabel.color = result == BreakthroughResult.Success
                ? CultivationUIFactory.Success
                : result == BreakthroughResult.Failed ? CultivationUIFactory.Danger : CultivationUIFactory.TextMuted;
        }

        if (chanceLabel != null)
            chanceLabel.text = result == BreakthroughResult.Success
                ? "Небеса приняли ваш прорыв."
                : result == BreakthroughResult.Failed
                    ? "Небеса отвергли попытку. Прозрение повысит шанс."
                    : string.Empty;

        if (cancelButton != null)
            cancelButton.gameObject.SetActive(false);

        float delay = result == BreakthroughResult.Success ? 3.2f : 2.6f;
        hideRoutine = StartCoroutine(HideAfter(delay));
    }

    private IEnumerator HideAfter(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        hideRoutine = null;
        gameObject.SetActive(false);
    }

    private void StopHideRoutine()
    {
        if (hideRoutine == null) return;
        StopCoroutine(hideRoutine);
        hideRoutine = null;
    }

    private void CancelSequence()
    {
        if (breakthrough != null) breakthrough.CancelBreakthrough();
    }
}
