namespace Kosh.Core.ValueObjects;

public readonly record struct GroupId(string Value)
{
    public override string ToString() => Value;

    public static GroupId New()
    {
        return new GroupId(Guid.NewGuid().ToString());
    }
}