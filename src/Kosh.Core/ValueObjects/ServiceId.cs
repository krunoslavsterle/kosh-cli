namespace Kosh.Core.ValueObjects;

public readonly record struct ServiceId(string Value)
{
    public override string ToString() => Value;
}