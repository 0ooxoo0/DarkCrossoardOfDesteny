/// <summary>Результат одной попытки крафта: успех, качество, шанс и причина.</summary>
public struct AlchemyResult
{
    public bool Success;
    public PillQuality Quality;
    public int OutputCount;
    public string Message;
    public float Chance;

    public AlchemyResult(bool success, PillQuality quality, int outputCount, string message, float chance)
    {
        Success = success;
        Quality = quality;
        OutputCount = outputCount;
        Message = message;
        Chance = chance;
    }

    /// <summary>Провал без расхода ингредиентов (например, нет места).</summary>
    public static AlchemyResult Fail(string message, float chance = 0f)
    {
        return new AlchemyResult(false, PillQuality.Normal, 0, message, chance);
    }
}
