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

/// <summary>Uses a compile-time converter for the property's complete type.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ColumnConverterAttribute(Type converterType) : Attribute
{
    public Type ConverterType { get; } = converterType;
}
