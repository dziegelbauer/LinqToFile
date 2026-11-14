using System.Globalization;
using System.Numerics;

namespace LinqToFile.Infrastructure;

/// <summary>Reusable scalar formatting storage for generated mappers. Do not retain this object.</summary>
public sealed partial class TabularValue
{
    private char[] buffer = new char[128];
    private int length;
    private string? text;
    private readonly CultureInfo culture;
    private readonly bool trimFields;
    private readonly TabularDateSystem dateSystem;
    internal ExportCellKind OutputKind { get; private set; }
    internal TabularValue(CultureInfo culture, bool trimFields = false, TabularDateSystem dateSystem = TabularDateSystem.None)
    { this.culture = culture; this.trimFields = trimFields; this.dateSystem = dateSystem; }
    internal ReadOnlySpan<char> Text => text is null ? buffer.AsSpan(0, length) : text.AsSpan();
    internal void Reset() { text = null; length = 0; OutputKind = ExportCellKind.Blank; }
    /// <summary>Formats through a statically selected converter into reusable storage, including null values.</summary>
    /// <typeparam name="T">The mapped value type.</typeparam>
    /// <typeparam name="TConverter">The statically selected converter type.</typeparam>
    /// <param name="value">The value to convert or store.</param>
    public void SetConverted<T, TConverter>(T value) where TConverter : IColumnConverter<T>
    {
        text = null;
        OutputKind = ExportCellKind.Text;
        var context = new ColumnConversionContext(culture, TabularCellKind.Text, TabularDateSystem.None);
        while (true)
        {
            if (TConverter.TryFormat(value, buffer, context, out int written))
            {
                if ((uint)written > (uint)buffer.Length) throw new FormatException();
                length = written;
                return;
            }
            if (buffer.Length == Array.MaxLength) throw new FormatException();
            Array.Resize(ref buffer, (int)Math.Min((long)buffer.Length * 2, Array.MaxLength));
        }
    }
    /// <summary>Stores a field value for generated serialization.</summary>
    /// <param name="value">The value to convert or store.</param>
    public void SetText(string? value) { text = value; length = 0; OutputKind = value is null ? ExportCellKind.Blank : ExportCellKind.Text; }
    /// <summary>Appends text to the current generated output field.</summary>
    /// <param name="value">The value to convert or store.</param>
    public void AppendText(string value)
    {
        OutputKind = ExportCellKind.Text;
        if (text is not null) { string previous = text; text = null; AppendText(previous); }
        int required = checked(length + value.Length);
        if (required > buffer.Length) Array.Resize(ref buffer, Math.Max(required, checked(buffer.Length * 2)));
        value.AsSpan().CopyTo(buffer.AsSpan(length)); length = required;
    }
    private void Format<T>(T value, ReadOnlySpan<char> format, IFormatProvider provider) where T : ISpanFormattable
    {
        text = null;
        OutputKind = ExportCellKind.Text;
        while (!value.TryFormat(buffer, out length, format, provider))
            Array.Resize(ref buffer, checked(buffer.Length * 2));
    }
    /// <summary>Stores a field value for generated serialization.</summary>
    /// <typeparam name="T">The mapped value type.</typeparam>
    /// <param name="value">The value to convert or store.</param>
    /// <param name="format">The format used for text conversion.</param>
    public void SetNumber<T>(T value, string format = "G") where T : struct, INumberBase<T>
    {
        Format(T.IsFinite(value) ? value : throw new FormatException(), format,
            dateSystem == TabularDateSystem.None ? culture : CultureInfo.InvariantCulture);
        if (dateSystem != TabularDateSystem.None)
        {
            ValidateExcelNumber(Text);
            OutputKind = ExportCellKind.Number;
        }
    }
    /// <summary>Stores a field value for generated serialization.</summary>
    /// <param name="value">The value to convert or store.</param>
    public void Set(bool value)
    {
        SetText(value ? "true" : "false");
        if (dateSystem != TabularDateSystem.None) { SetText(value ? "1" : "0"); OutputKind = ExportCellKind.Boolean; }
    }
    /// <summary>Stores a field value for generated serialization.</summary>
    /// <param name="value">The value to convert or store.</param>
    public void Set(char value) { text = null; buffer[0] = value; length = 1; OutputKind = ExportCellKind.Text; }
    /// <summary>Stores a field value for generated serialization.</summary>
    /// <param name="value">The value to convert or store.</param>
    public void Set(Guid value) => Format(value, "D", CultureInfo.InvariantCulture);
    /// <summary>Stores a field value for generated serialization.</summary>
    /// <param name="value">The value to convert or store.</param>
    public void Set(DateTime value)
    {
        if (value.Kind != DateTimeKind.Unspecified) throw new FormatException();
        if (dateSystem == TabularDateSystem.None) Format(value, "O", CultureInfo.InvariantCulture);
        else SetCalendar(value.Ticks, ExportCellKind.DateTime);
    }
    /// <summary>Stores a field value for generated serialization.</summary>
    /// <param name="value">The value to convert or store.</param>
    public void Set(DateTimeOffset value) => Format(value, "O", CultureInfo.InvariantCulture);
    /// <summary>Stores a field value for generated serialization.</summary>
    /// <param name="value">The value to convert or store.</param>
    public void Set(DateOnly value)
    {
        if (dateSystem == TabularDateSystem.None) Format(value, "O", CultureInfo.InvariantCulture);
        else SetCalendar(value.ToDateTime(TimeOnly.MinValue).Ticks, ExportCellKind.Date);
    }
    /// <summary>Stores a field value for generated serialization.</summary>
    /// <param name="value">The value to convert or store.</param>
    public void Set(TimeOnly value)
    {
        if (dateSystem == TabularDateSystem.None) Format(value, "O", CultureInfo.InvariantCulture);
        else SetSerial(value.Ticks, ExportCellKind.Time);
    }
    /// <summary>Stores a field value for generated serialization.</summary>
    /// <param name="value">The value to convert or store.</param>
    public void Set(TimeSpan value) => Format(value, "c", CultureInfo.InvariantCulture);
}
