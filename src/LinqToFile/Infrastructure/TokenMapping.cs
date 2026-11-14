namespace LinqToFile.Infrastructure;

public sealed partial class TabularRow
{
    /// <summary>Tests only text cells, after configured field trimming.</summary>
    /// <param name="ordinal">The zero-based field or mapper position.</param>
    /// <param name="token">The literal text token.</param>
    /// <param name="comparison">The token comparison rule.</param>
    /// <returns>True when the field matches the requested condition; otherwise, false.</returns>
    public bool IsNullToken(int ordinal, string token, StringComparison comparison)
        => IsTextToken(ordinal, token, comparison);

    /// <summary>Tests a literal token only in text cells, after configured field trimming.</summary>
    /// <param name="ordinal">The zero-based field or mapper position.</param>
    /// <param name="token">The literal text token.</param>
    /// <param name="comparison">The token comparison rule.</param>
    /// <returns>True when the field matches the requested condition; otherwise, false.</returns>
    public bool IsTextToken(int ordinal, string token, StringComparison comparison)
    {
        var field = GetField(ordinal);
        return (kinds is null || kinds[ordinal] == CellKind.Text) && field.Equals(token.AsSpan(), comparison);
    }

    /// <summary>Reads an annotated text token or retains native boolean conversion.</summary>
    /// <param name="ordinal">The zero-based field or mapper position.</param>
    /// <param name="member">The target property name used in error diagnostics.</param>
    /// <param name="trueToken">The literal true token.</param>
    /// <param name="falseToken">The literal false token.</param>
    /// <param name="comparison">The token comparison rule.</param>
    /// <returns>The converted field value.</returns>
    /// <exception cref="TabularConversionException">The field is not valid for the requested type or format.</exception>
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
    /// <param name="token">The literal text token.</param>
    /// <param name="comparison">The token comparison rule.</param>
    public void RejectNullToken(string token, StringComparison comparison)
    {
        if (dateSystem != TabularDateSystem.None && OutputKind != ExportCellKind.Text) return;
        var field = trimFields ? Text.Trim() : Text;
        if (field.Equals(token.AsSpan(), comparison)) throw new FormatException();
    }
}
