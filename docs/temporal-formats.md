# Exact date and time formats

Use `[TemporalFormat("format")]` to select one .NET format for a property.
The format controls exact parsing of CSV fields and XLSX text cells.
It also controls CSV output.
There is no fallback to general parsing or to the default writer format.

The annotation supports `DateTime`, `DateTimeOffset`, `DateOnly`, and `TimeOnly`.
It also supports their nullable forms.
Properties without the annotation keep the [built-in conversion rules](conversions.md).

## Declare a model

This model uses a full date, a time with minute precision, and an explicit offset.
It uses the implicit `System` imports from a standard .NET project.

```csharp
using LinqToFile;

namespace SampleModels;

[TabularRecord]
public sealed partial class Appointment
{
    [TemporalFormat("dd MMMM yyyy")]
    public DateOnly Date { get; init; }

    [TemporalFormat("HH:mm")]
    public TimeOnly Start { get; init; }

    [TemporalFormat("yyyy-MM-dd'T'HH:mm:sszzz")]
    public DateTimeOffset RecordedAt { get; init; }

    [TemporalFormat("yyyy-MM-dd HH:mm:ss")]
    public DateTime? Reminder { get; init; }
}
```

Select a culture before using the source.
This example writes `23 novembre 2004` for the date.
It writes `14:30` for a start time that contains seconds.
Here, `path` is a new CSV path:

```csharp
var file = TabularFile.Csv<Appointment>(path)
    .WithCulture(CultureInfo.GetCultureInfo("fr-FR"))
    .WithDelimiter(';');
var appointment = new Appointment
{
    Date = new DateOnly(2004, 11, 23),
    Start = new TimeOnly(14, 30, 59),
    RecordedAt = new DateTimeOffset(2004, 11, 23, 13, 45, 56, TimeSpan.FromMinutes(345)),
    Reminder = new DateTime(2004, 11, 23, 14, 0, 0, DateTimeKind.Unspecified)
};
await file.CreateAsync(new[] { appointment });
var rows = await file.ToListAsync();
```

Add `using System.Globalization;` for `CultureInfo`.
The [runnable sample](../samples/NativeAot/TemporalFormatSample.cs) checks creation, append, insertion, and workbook dates.
The model belongs to a separate assembly. The sample also runs under Native AOT.

## Format rules

Use .NET [custom patterns](https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings) or the supported [standard formats](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings).
Format letters are case-sensitive. For example, `MM` means month and `mm` means minute.
Quote or escape literal text. Quoted letters do not count as date or offset components.
`ddd` and `dddd` name a weekday; they do not supply a day of month.

| Type | Supported standard formats | Custom pattern requirements |
|---|---|---|
| `DateTime` | `d`, `D`, `f`, `F`, `g`, `G`, `s`, `o`, `O` | Include year, month, and day of month. Do not use `z` offsets. `K` can represent the absence of an offset. |
| `DateTimeOffset` | `o`, `O` | Include year, month, day of month, and `zzz` or `K`. Preserve offset hours and minutes. |
| `DateOnly` | `d`, `D`, `o`, `O`, `r`, `R` | Include year, month, and day of month. Do not include time or timezone components. |
| `TimeOnly` | `t`, `T`, `o`, `O`, `r`, `R` | Include time components. Do not include date or timezone components. |

Incomplete dates, such as `MM-dd` or `yyyy-MM`, are rejected.
The library does not fill a missing year from the current date.
Use `TimeOnly` for time-only values.
Formats that convert through UTC or the local timezone are not supported.
Use `zzz` instead of `z` or `zz` for an offset, so offset minutes are preserved.
Fractional seconds support at most seven digits.

The selected format can omit time components or precision.
The writer does not throw because omitted components are nonzero.
Reading the result cannot restore those components.
For example, `HH:mm` drops seconds and fractions; the read value has zero seconds.
Use a format that includes the required precision when values must round-trip without that loss.
Other details, such as two-digit year expansion, follow the BCL and the selected culture.

## Culture and timezone

`WithCulture` supplies the same read-only culture snapshot to parsing and formatting.
The default is `CultureInfo.InvariantCulture`.
Standard formats with intrinsic invariant rules, such as `O`, keep those BCL rules.
The library does not rewrite them as culture-specific patterns.
Changing the caller's culture after configuration does not change the source.

A culture can change the patterns behind `d`, `D`, `t`, and related standards.
`WithCulture` rejects patterns that violate the required date components or timezone rules.
Calendar ranges and value-specific parsing or formatting failures still produce conversion or write errors.

`DateTime` input must have no offset. Its kind remains `Unspecified`.
Writing a `Local` or `Utc` value throws.
`DateTimeOffset` input must contain an explicit offset, including when the format uses `K`.
The result keeps the offset and input clock time.
The library does not assume the machine's local timezone.

## XLSX and empty fields

Native XLSX numeric and ISO date cells keep their existing [target-type and workbook date-system rules](xlsx.md#dates-and-times).
The annotation does not render native cells to display text before parsing.
It changes text conversion only.

Field trimming occurs before exact parsing when enabled.
Without trimming, whitespace must match the format.
An empty nullable field remains null. An empty non-nullable field causes a conversion error.
A null property writes an empty field.
CSV quoting, separators, encoding, and size limits still apply to formatted output.

## Mapping and errors

An override inherits the nearest temporal format unless it declares another one.
`[Column]` can rename the property.
An effective temporal format cannot combine with `[Ignore]` or `[ColumnConverter]`.
This rule also applies to inherited combinations.
The normal setter, init accessor, and write getter requirements still apply.

Invalid annotations, unsupported target types, and incompatible formats produce compiler diagnostic `LTF006`.
Conflicting annotations produce `LTF004`.
The compiler checks pattern structure and performs a BCL syntax check.
Culture-dependent patterns are also checked when `WithCulture` runs.
These checks do not guarantee that every possible value can use the selected calendar or format.

Read failures use `TabularConversionException`.
Write failures use `TabularWriteException`.
These errors retain record, column, property, and applicable worksheet locations.
They omit raw values and inner format exceptions.
