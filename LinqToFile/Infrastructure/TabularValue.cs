using System.Globalization;
using System.Numerics;

namespace LinqToFile.Infrastructure;

/// <summary>Reusable scalar formatting storage for generated mappers. Do not retain this object.</summary>
public sealed class TabularValue
{
    private char[] buffer = new char[128];
    private int length;
    private string? text;
    private readonly CultureInfo culture;
    internal TabularValue(CultureInfo culture) => this.culture = culture;
    internal ReadOnlySpan<char> Text => text is null ? buffer.AsSpan(0, length) : text.AsSpan();
    internal void Reset() { text = null; length = 0; }
    public void SetText(string? value) { text = value; length = 0; }
    public void AppendText(string value)
    {
        if (text is not null) { string previous = text; text = null; AppendText(previous); }
        int required = checked(length + value.Length);
        if (required > buffer.Length) Array.Resize(ref buffer, Math.Max(required, checked(buffer.Length * 2)));
        value.AsSpan().CopyTo(buffer.AsSpan(length)); length = required;
    }
    private void Format<T>(T value, ReadOnlySpan<char> format, IFormatProvider provider) where T : ISpanFormattable
    {
        text = null;
        while (!value.TryFormat(buffer, out length, format, provider))
            Array.Resize(ref buffer, checked(buffer.Length * 2));
    }
    public void SetNumber<T>(T value, string format = "G") where T : struct, INumberBase<T> =>
        Format(T.IsFinite(value) ? value : throw new FormatException(), format, culture);
    public void Set(bool value) => SetText(value ? "true" : "false");
    public void Set(char value) { text = null; buffer[0] = value; length = 1; }
    public void Set(Guid value) => Format(value, "D", CultureInfo.InvariantCulture);
    public void Set(DateTime value)
    {
        if (value.Kind != DateTimeKind.Unspecified) throw new FormatException();
        Format(value, "O", CultureInfo.InvariantCulture);
    }
    public void Set(DateTimeOffset value) => Format(value, "O", CultureInfo.InvariantCulture);
    public void Set(DateOnly value) => Format(value, "O", CultureInfo.InvariantCulture);
    public void Set(TimeOnly value) => Format(value, "O", CultureInfo.InvariantCulture);
    public void Set(TimeSpan value) => Format(value, "c", CultureInfo.InvariantCulture);
}
