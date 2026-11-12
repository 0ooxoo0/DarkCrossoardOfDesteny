using UnityEngine;

/// <summary>Пути Дао. Пять стартовых путей MVP.</summary>
public enum DaoType
{
    Sword,
    Fist,
    Body,
    Fire,
    Thunder
}

/// <summary>Названия и палитра путей (используются UI и VFX, если нет DaoPath).</summary>
public static class DaoTypeInfo
{
    public static readonly DaoType[] All =
    {
        DaoType.Sword,
        DaoType.Fist,
        DaoType.Body,
        DaoType.Fire,
        DaoType.Thunder
    };

    public static Color GetColor(DaoType type)
    {
        switch (type)
        {
            case DaoType.Sword: return Hex("#C9D1D9");
            case DaoType.Fist: return Hex("#E8A33D");
            case DaoType.Body: return Hex("#4FA97A");
            case DaoType.Fire: return Hex("#E5533C");
            case DaoType.Thunder: return Hex("#8B7EE8");
            default: return Color.white;
        }
    }

    public static string GetDisplayName(DaoType type)
    {
        switch (type)
        {
            case DaoType.Sword: return "Путь Меча";
            case DaoType.Fist: return "Путь Кулака";
            case DaoType.Body: return "Путь Тела";
            case DaoType.Fire: return "Путь Огня";
            case DaoType.Thunder: return "Путь Грома";
            default: return type.ToString();
        }
    }

    public static string GetChineseName(DaoType type)
    {
        switch (type)
        {
            case DaoType.Sword: return "剑道";
            case DaoType.Fist: return "拳道";
            case DaoType.Body: return "体道";
            case DaoType.Fire: return "火道";
            case DaoType.Thunder: return "雷道";
            default: return string.Empty;
        }
    }

    public static Color Hex(string html)
    {
        return ColorUtility.TryParseHtmlString(html, out Color color) ? color : Color.white;
    }
}
