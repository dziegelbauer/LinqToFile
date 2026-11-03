namespace LinqToFile;

/// <summary>Requests a compile-time record mapper.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TabularRecordAttribute : Attribute;

/// <summary>Maps a property to a literal column header.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ColumnAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>Excludes a property from mapping.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class IgnoreAttribute : Attribute;

/// <summary>Uses one true/false token pair for text reads and CSV output.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class BooleanTokensAttribute(string trueToken, string falseToken) : Attribute
{
    public string TrueToken { get; } = trueToken;
    public string FalseToken { get; } = falseToken;
    public StringComparison Comparison { get; set; } = StringComparison.OrdinalIgnoreCase;
}

/// <summary>Recognizes one text token as null and writes that token for null values.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class NullTokenAttribute(string token) : Attribute
{
    public string Token { get; } = token;
    public StringComparison Comparison { get; set; } = StringComparison.Ordinal;
}

/// <summary>Uses one exact .NET temporal format for text reads and CSV output.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class TemporalFormatAttribute(string format) : Attribute
{
    public string Format { get; } = format;
}

/// <summary>Uses a compile-time converter for the property's complete type.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ColumnConverterAttribute(Type converterType) : Attribute
{
    public Type ConverterType { get; } = converterType;
}
