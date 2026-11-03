namespace LinqToFile.Infrastructure;

public sealed partial class TabularRow
{
    /// <summary>Tests only text cells, after configured field trimming.</summary>
    public bool IsNullToken(int ordinal, string token, StringComparison comparison)
    {
        var field = GetField(ordinal);
        return (kinds is null || kinds[ordinal] == CellKind.Text) && field.Equals(token.AsSpan(), comparison);
    }

    /// <summary>Reads an annotated text token or retains native boolean conversion.</summary>
    public bool ReadBooleanTokens(int ordinal, string member, string trueToken, string falseToken, StringComparison comparison)
    {
        if (kinds is not null && kinds[ordinal] != CellKind.Text) return ReadBoolean(ordinal, member);
        var field = GetField(ordinal);
        if (field.Equals(trueToken.AsSpan(), comparison)) return true;
        if (field.Equals(falseToken.AsSpan(), comparison)) return false;
        throw CreateConversionException(ordinal, member);
    }
}

public sealed partial class TabularValue
{
    /// <summary>Rejects non-null output that would be read back as the configured null token.</summary>
    public void RejectNullToken(string token, StringComparison comparison)
    {
        if (dateSystem != TabularDateSystem.None && OutputKind != ExportCellKind.Text) return;
        var field = trimFields ? Text.Trim() : Text;
        if (field.Equals(token.AsSpan(), comparison)) throw new FormatException();
    }
}
