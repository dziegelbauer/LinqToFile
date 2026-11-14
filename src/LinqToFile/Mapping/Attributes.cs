namespace LinqToFile;

/// <summary>Requests a compile-time record mapper.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TabularRecordAttribute : Attribute;

/// <summary>Maps a property to a literal column header.</summary>
/// <param name="name">The literal column header.</param>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ColumnAttribute(string name) : Attribute
{
    /// <summary>Gets the literal column header.</summary>
    public string Name { get; } = name;
}

/// <summary>Maps a property to a zero-based field position for headerless CSV reads.</summary>
/// <param name="index">The zero-based field position.</param>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ColumnIndexAttribute(int index) : Attribute
{
    /// <summary>Gets the zero-based headerless field position.</summary>
    public int Index { get; } = index;
}

/// <summary>Excludes a property from mapping.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class IgnoreAttribute : Attribute;

/// <summary>Uses a true/false token pair for text reads and CSV output, with optional input aliases.</summary>
/// <param name="trueToken">The literal true token.</param>
/// <param name="falseToken">The literal false token.</param>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class BooleanTokensAttribute(string trueToken, string falseToken) : Attribute
{
    /// <summary>Gets the primary true token used for input and CSV output.</summary>
    public string TrueToken { get; } = trueToken;
    /// <summary>Gets the primary false token used for input and CSV output.</summary>
    public string FalseToken { get; } = falseToken;
    /// <summary>Gets or sets additional input-only true tokens. The default is empty.</summary>
    public string[] TrueAliases { get; set; } = [];
    /// <summary>Gets or sets additional input-only false tokens. The default is empty.</summary>
    public string[] FalseAliases { get; set; } = [];
    /// <summary>Gets or sets Ordinal or OrdinalIgnoreCase token matching. The default is OrdinalIgnoreCase.</summary>
    public StringComparison Comparison { get; set; } = StringComparison.OrdinalIgnoreCase;
}

/// <summary>Recognizes a text token and optional input aliases as null, and writes the token for null values.</summary>
/// <param name="token">The literal text token.</param>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class NullTokenAttribute(string token) : Attribute
{
    /// <summary>Gets the primary null token used for text input and null output.</summary>
    public string Token { get; } = token;
    /// <summary>Gets or sets additional input-only null tokens. The default is empty.</summary>
    public string[] Aliases { get; set; } = [];
    /// <summary>Gets or sets Ordinal or OrdinalIgnoreCase token matching. The default is Ordinal.</summary>
    public StringComparison Comparison { get; set; } = StringComparison.Ordinal;
}

/// <summary>Uses one exact .NET temporal format for text reads and CSV output.</summary>
/// <param name="format">The exact temporal format.</param>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class TemporalFormatAttribute(string format) : Attribute
{
    /// <summary>Gets the exact temporal format.</summary>
    public string Format { get; } = format;
}

/// <summary>Uses a compile-time converter for the property's complete type.</summary>
/// <param name="converterType">The converter type that implements IColumnConverter for the property type.</param>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ColumnConverterAttribute(Type converterType) : Attribute
{
    /// <summary>Gets the statically selected converter type.</summary>
    public Type ConverterType { get; } = converterType;
}
