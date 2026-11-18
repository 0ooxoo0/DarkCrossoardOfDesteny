using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Компактный список активных дебафов культивации под виталами.
/// Строки создаются один раз, текст обновляется по таймеру, без пересоздания
/// объектов каждый кадр.
/// </summary>
[DisallowMultipleComponent]
public class DebuffPanelUI : MonoBehaviour
{
    [Header("Вид")]
    [SerializeField] private Vector2 panelSize = new Vector2(360f, 0f);
    [SerializeField] private Vector2 panelOffset = new Vector2(40f, -150f);
    [SerializeField, Min(12f)] private float rowHeight = 34f;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;

    private CultivationDebuffController debuffs;
    private readonly List<Row> rows = new List<Row>();
    private RectTransform panelRect;
    private float timer;

    private sealed class Row
    {
        public GameObject Root;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Effects;
    }

    /// <summary>Собирает панель под указанным родителем.</summary>
    public void Build(CultivationDebuffController controller, Transform parent)
    {
        debuffs = controller;

        panelRect = (RectTransform)transform;
        panelRect.SetParent(parent, false);
        CultivationUIFactory.Place(panelRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
            panelOffset, new Vector2(panelSize.x, 0f));

        Refresh();
    }

    private void Update()
    {
        if (debuffs == null) return;

        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;

        timer = refreshInterval;
        Refresh();
    }

    private void Refresh()
    {
        if (panelRect == null) return;

        IReadOnlyList<ActiveCultivationDebuff> active = debuffs != null ? debuffs.Active : null;
        int count = active != null ? active.Count : 0;

        EnsureRows(count);

        for (int i = 0; i < rows.Count; i++)
        {
            bool visible = i < count;
            if (rows[i].Root.activeSelf != visible)
                rows[i].Root.SetActive(visible);

            if (!visible) continue;
            FillRow(rows[i], active[i]);
        }

        float height = count * rowHeight;
        if (!Mathf.Approximately(panelRect.sizeDelta.y, height))
            panelRect.sizeDelta = new Vector2(panelSize.x, height);
    }

    private void EnsureRows(int count)
    {
        while (rows.Count < count)
        {
            int index = rows.Count;

            var rowRect = CultivationUIFactory.CreateRect("Debuff " + index, panelRect);
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -index * rowHeight);
            rowRect.sizeDelta = new Vector2(0f, rowHeight);

            var title = CultivationUIFactory.CreateText("Title", rowRect, string.Empty, 14f,
                CultivationUIFactory.Danger, TextAlignmentOptions.TopLeft);
            CultivationUIFactory.Stretch(title.rectTransform);
            title.rectTransform.offsetMax = new Vector2(0f, -2f);

            var effects = CultivationUIFactory.CreateText("Effects", rowRect, string.Empty, 11f,
                CultivationUIFactory.TextMuted, TextAlignmentOptions.BottomLeft);
            CultivationUIFactory.Stretch(effects.rectTransform);
            effects.rectTransform.offsetMin = new Vector2(0f, 2f);

            rows.Add(new Row
            {
                Root = rowRect.gameObject,
                Title = title,
                Effects = effects
            });
        }
    }

    private static void FillRow(Row row, ActiveCultivationDebuff debuff)
    {
        if (row == null || debuff == null || debuff.Stage == null) return;

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, debuff.Remaining));
        row.Title.text = $"{debuff.Stage.DisplayName} · {seconds} с";
        row.Effects.text = DescribeEffects(debuff.Stage);
    }

    private static string DescribeEffects(CultivationFailureStage stage)
    {
        var parts = new List<string>(4);

        AppendPercent(parts, "Ци-реген", stage.QiRegenMultiplier);
        AppendPercent(parts, "Урон", stage.DamageDealtMultiplier);
        AppendPercent(parts, "HP", stage.MaxHealthMultiplier);

        if (!Mathf.Approximately(stage.DamageTakenMultiplier, 1f))
            AppendPercent(parts, "Получаемый урон", stage.DamageTakenMultiplier);

        return string.Join("   ", parts);
    }

    private static void AppendPercent(List<string> parts, string label, float multiplier)
    {
        int percent = Mathf.RoundToInt((multiplier - 1f) * 100f);
        if (percent == 0) return;

        // Минус — типографский, чтобы не путать с дефисом в шрифте.
        parts.Add($"{label} {(percent < 0 ? "−" : "+")}{Mathf.Abs(percent)}%");
    }
}
