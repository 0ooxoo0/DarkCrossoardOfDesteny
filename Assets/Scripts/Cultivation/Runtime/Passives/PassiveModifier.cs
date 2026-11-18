/// <summary>Сработавший пассив: вид эффекта, величина прибавки и источник.</summary>
public struct PassiveModifier
{
    public PassiveEffectKind Kind;
    public float Value;
    public TechniqueData Source;

    public PassiveModifier(PassiveEffectKind kind, float value, TechniqueData source)
    {
        Kind = kind;
        Value = value;
        Source = source;
    }
}
