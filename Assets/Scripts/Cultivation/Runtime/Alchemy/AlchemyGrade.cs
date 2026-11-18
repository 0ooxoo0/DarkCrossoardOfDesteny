using System;
using UnityEngine;

/// <summary>Название грейда алхимика (12 ступеней).</summary>
public enum AlchemyGradeName
{
    Common = 0,
    Earth = 1,
    Heaven = 2,
    Mysterious = 3,
    Spirit = 4,
    Saint = 5,
    SaintKing = 6,
    Origin = 7,
    OriginKing = 8,
    DaoSource = 9,
    Emperor = 10,
    OpenHeaven = 11
}

/// <summary>Подранг внутри грейда. <see cref="None"/> — у открывающего Небеса.</summary>
public enum AlchemySubRank
{
    Low = 0,
    Middle = 1,
    High = 2,
    Great = 3,
    None = 4
}

/// <summary>
/// Грейд алхимика: имя и подранг. Сравнение идёт по <see cref="RankIndex"/>,
/// чтобы рецепты и требования не зависели от двух полей сразу.
/// </summary>
[Serializable]
public struct AlchemyGrade : IComparable<AlchemyGrade>, IEquatable<AlchemyGrade>
{
    [SerializeField] private AlchemyGradeName gradeName;
    [SerializeField] private AlchemySubRank subRank;

    public AlchemyGradeName Name => gradeName;
    public AlchemySubRank SubRank => subRank;

    /// <summary>Монотонный номер ступени: имя × 3 + подранг (Great и None дают +2).</summary>
    public int RankIndex => (int)gradeName * 3 + Math.Min((int)subRank, 2);

    public bool IsMax => gradeName == AlchemyGradeName.OpenHeaven;

    public string DisplayName
    {
        get
        {
            string grade = GetGradeName(gradeName);
            if (gradeName == AlchemyGradeName.OpenHeaven) return grade;
            return grade + " · " + GetSubRankName(subRank);
        }
    }

    public static AlchemyGrade CommonLow => new AlchemyGrade(AlchemyGradeName.Common, AlchemySubRank.Low);

    public AlchemyGrade(AlchemyGradeName name, AlchemySubRank rank)
    {
        gradeName = name;
        subRank = rank;
    }

    /// <summary>Следующая ступень: подранг вверх, затем грейд вверх.</summary>
    public AlchemyGrade Next()
    {
        if (IsMax) return this;

        if (gradeName == AlchemyGradeName.Emperor)
        {
            switch (subRank)
            {
                case AlchemySubRank.Low: return new AlchemyGrade(gradeName, AlchemySubRank.Middle);
                case AlchemySubRank.Middle: return new AlchemyGrade(gradeName, AlchemySubRank.High);
                case AlchemySubRank.High: return new AlchemyGrade(gradeName, AlchemySubRank.Great);
                default: return new AlchemyGrade(AlchemyGradeName.OpenHeaven, AlchemySubRank.None);
            }
        }

        switch (subRank)
        {
            case AlchemySubRank.Low: return new AlchemyGrade(gradeName, AlchemySubRank.Middle);
            case AlchemySubRank.Middle: return new AlchemyGrade(gradeName, AlchemySubRank.High);
            default: return new AlchemyGrade((AlchemyGradeName)((int)gradeName + 1), AlchemySubRank.Low);
        }
    }

    public int CompareTo(AlchemyGrade other) => RankIndex.CompareTo(other.RankIndex);

    public bool Equals(AlchemyGrade other) => gradeName == other.gradeName && subRank == other.subRank;
    public override bool Equals(object obj) => obj is AlchemyGrade other && Equals(other);
    public override int GetHashCode() => RankIndex;
    public override string ToString() => DisplayName;

    public static bool operator ==(AlchemyGrade left, AlchemyGrade right) => left.Equals(right);
    public static bool operator !=(AlchemyGrade left, AlchemyGrade right) => !left.Equals(right);
    public static bool operator <(AlchemyGrade left, AlchemyGrade right) => left.RankIndex < right.RankIndex;
    public static bool operator >(AlchemyGrade left, AlchemyGrade right) => left.RankIndex > right.RankIndex;
    public static bool operator <=(AlchemyGrade left, AlchemyGrade right) => left.RankIndex <= right.RankIndex;
    public static bool operator >=(AlchemyGrade left, AlchemyGrade right) => left.RankIndex >= right.RankIndex;

    private static string GetGradeName(AlchemyGradeName name)
    {
        switch (name)
        {
            case AlchemyGradeName.Common: return "Обычный";
            case AlchemyGradeName.Earth: return "Земной";
            case AlchemyGradeName.Heaven: return "Небесный";
            case AlchemyGradeName.Mysterious: return "Таинственный";
            case AlchemyGradeName.Spirit: return "Духовный";
            case AlchemyGradeName.Saint: return "Святой";
            case AlchemyGradeName.SaintKing: return "Святой король";
            case AlchemyGradeName.Origin: return "Изначальный";
            case AlchemyGradeName.OriginKing: return "Король истока";
            case AlchemyGradeName.DaoSource: return "Источник Дао";
            case AlchemyGradeName.Emperor: return "Императорский";
            case AlchemyGradeName.OpenHeaven: return "Открывающий Небеса";
            default: return name.ToString();
        }
    }

    private static string GetSubRankName(AlchemySubRank rank)
    {
        switch (rank)
        {
            case AlchemySubRank.Low: return "Низкий";
            case AlchemySubRank.Middle: return "Средний";
            case AlchemySubRank.High: return "Высокий";
            case AlchemySubRank.Great: return "Великий";
            default: return string.Empty;
        }
    }
}
