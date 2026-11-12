using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Горизонтальная шкала Ци. Встаёт под HP-бар (PlayerInfo/HP), иначе — левый нижний угол.
/// Цвет шкалы = цвет активного Дао. Чужие элементы HUD не трогаются.
/// </summary>
[DisallowMultipleComponent]
public class QiBarUI : MonoBehaviour
{
    private QiComponent qi;
    private DaoSystem dao;

    private Slider slider;
    private Image fillImage;
    private Image backgroundImage;
    private TextMeshProUGUI valueLabel;

    private float targetValue = 1f;
    private float displayValue = 1f;

    public void Build(QiComponent qiComponent, DaoSystem daoSystem, Transform hudParent, RectTransform hpBar)
    {
        qi = qiComponent;
        dao = daoSystem;

        var root = (RectTransform)transform;
        root.SetParent(hudParent, false);

        if (hpBar != null)
        {
            var hpRect = hpBar;
            var hpParent = hpRect.parent as RectTransform;
            if (hpParent != null)
            {
                transform.SetParent(hpParent, false);
                CultivationUIFactory.Place(root, hpRect.anchorMin, hpRect.pivot,
                    hpRect.anchoredPosition + new Vector2(0f, -40f),
                    new Vector2(Mathf.Max(200f, hpRect.sizeDelta.x), 22f));
            }
        }
        else
        {
            // HP-бара в проекте нет — встаём в левый верхний угол с отступом.
            CultivationUIFactory.Place(root, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -40f), new Vector2(420f, 30f));
        }

        slider = CultivationUIFactory.CreateBar("Bar", root, DaoTypeInfo.GetColor(DaoType.Sword), out backgroundImage, out fillImage);
        var sliderRect = (RectTransform)slider.transform;
        CultivationUIFactory.Stretch(sliderRect);

        valueLabel = CultivationUIFactory.CreateText("Value", root, "0 / 0", 16f, CultivationUIFactory.TextMain,
            TextAlignmentOptions.MidlineRight);
        var labelRect = valueLabel.rectTransform;
        CultivationUIFactory.Stretch(labelRect);
        labelRect.offsetMax = new Vector2(-10f, 0f);

        var caption = CultivationUIFactory.CreateText("Caption", root, "ЦИ", 14f, new Color(0.85f, 0.82f, 0.74f, 0.9f),
            TextAlignmentOptions.MidlineLeft);
        var captionRect = caption.rectTransform;
        CultivationUIFactory.Stretch(captionRect);
        captionRect.offsetMin = new Vector2(10f, 0f);
        caption.characterSpacing = 8f;

        Subscribe();
        Refresh(true);
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (qi != null) qi.OnQiChanged += HandleQiChanged;
        if (dao != null) dao.OnActiveDaoChanged += HandleActiveDaoChanged;
    }

    private void Unsubscribe()
    {
        if (qi != null) qi.OnQiChanged -= HandleQiChanged;
        if (dao != null) dao.OnActiveDaoChanged -= HandleActiveDaoChanged;
    }

    private void HandleQiChanged(int current, int max)
    {
        targetValue = max <= 0 ? 0f : Mathf.Clamp01((float)current / max);
        if (valueLabel != null)
            valueLabel.text = current + " / " + max;
    }

    private void HandleActiveDaoChanged(DaoType type)
    {
        if (fillImage == null) return;
        Color color = ResolveDaoColor(type);
        fillImage.color = color;
        if (backgroundImage != null)
            backgroundImage.color = Color.Lerp(new Color(0.05f, 0.06f, 0.08f, 0.95f), color, 0.12f);
    }

    private Color ResolveDaoColor(DaoType type)
    {
        if (dao != null)
        {
            var path = dao.GetDaoPath(type);
            if (path != null) return path.Color;
        }
        return DaoTypeInfo.GetColor(type);
    }

    private void Refresh(bool immediate)
    {
        if (qi == null) return;

        HandleQiChanged(qi.Current, qi.Max);
        if (dao != null && dao.ActiveDao.HasValue)
            HandleActiveDaoChanged(dao.ActiveDao.Value);
        else
            HandleActiveDaoChanged(DaoType.Sword);

        if (immediate)
        {
            displayValue = targetValue;
            if (slider != null) slider.value = displayValue;
        }
    }

    private void Update()
    {
        if (slider == null) return;
        if (Mathf.Abs(displayValue - targetValue) < 0.0005f)
        {
            displayValue = targetValue;
            slider.value = displayValue;
            return;
        }

        displayValue = Mathf.Lerp(displayValue, targetValue, Time.unscaledDeltaTime * 9f);
        slider.value = displayValue;
    }
}
