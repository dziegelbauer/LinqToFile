# Custom column converters

Use `[ColumnConverter(typeof(MyConverter))]` to change conversion for one property.
A converter can map a custom domain type or replace a built-in conversion.
The same annotation applies to CSV reads, XLSX reads, and CSV writes.
Properties without this annotation keep the [built-in rules](conversions.md).

## Declare a converter

Implement `IColumnConverter<T>` for the complete property type.
For an `int?` property, implement `IColumnConverter<int?>`.
For a nullable reference property, use the same nullable annotation in the interface.
The converter must be an accessible, closed class or struct that is not abstract.
It does not need a public constructor.
Its methods are static. The library does not create converter instances.
Explicit static interface implementations also work.

This model reads text codes such as `P-42` and native XLSX numbers such as `42`.
It writes codes as `P-42`.
The example uses the implicit `System` imports from a standard .NET project.

```csharp
using System.Globalization;
using LinqToFile;

namespace SampleModels;

public readonly record struct ProductCode(int Value);

public readonly struct ProductCodeConverter : IColumnConverter<ProductCode>
{
    public static bool TryParse(ReadOnlySpan<char> text, ColumnConversionContext context, out ProductCode value)
    {
        value = default;
        if (context.CellKind == TabularCellKind.Text)
        {
            if (!text.StartsWith("P-", StringComparison.Ordinal)) return false;
            text = text[2..];
        }
        else if (context.CellKind != TabularCellKind.Number) return false;

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number <= 0)
            return false;
        value = new ProductCode(number);
        return true;
    }

    public static bool TryFormat(ProductCode value, Span<char> destination, ColumnConversionContext context, out int charsWritten)
    {
        charsWritten = 0;
        if (value.Value <= 0) throw new FormatException();
        if (destination.Length < 2 || !value.Value.TryFormat(destination[2..], out int count, provider: CultureInfo.InvariantCulture))
            return false;
        destination[0] = 'P';
        destination[1] = '-';
        charsWritten = count + 2;
        return true;
    }
}

[TabularRecord]
public sealed partial class Product
{
    [Column("product_code"), ColumnConverter(typeof(ProductCodeConverter))]
    public ProductCode Code { get; init; }

    public string Name { get; init; } = "";
}
```

Use normal queries and [CSV write methods](csv-writing.md) with this model.
Here, `path` is a new CSV path:

```csharp
var products = TabularFile.Csv<Product>(path);
await products.CreateAsync(new[] { new Product { Code = new ProductCode(42), Name = "Tea" } });
await products.AppendAsync(new Product { Code = new ProductCode(7), Name = "Coffee" });
await products.InsertAsync(1, new Product { Code = new ProductCode(9), Name = "Milk" });
var selected = await products.AsAsyncEnumerable().Where(p => p.Code.Value > 8).ToListAsync();
```

The [runnable sample](../samples/NativeAot/ConverterSample.cs) checks both file formats.
Its model belongs to a separate assembly. It also runs during Native AOT and package checks.

## Input and context

`TryParse` receives the decoded, unescaped field as `ReadOnlySpan<char>`.
Configured field trimming occurs before this call.
The converter receives empty fields, including for nullable properties.
It decides whether an empty field means null, a default value, or an error.
Quoted and unquoted empty fields have the same meaning.
The converter must create owned values for a returned record.
Do not retain the input span or references to parser storage.

`ColumnConversionContext` supplies these properties:

| Property | Meaning |
|---|---|
| `Culture` | The source's read-only culture snapshot, from `WithCulture` or the invariant default |
| `CellKind` | `Text`, `Number`, `Boolean`, or `IsoDate` from `TabularCellKind` |
| `DateSystem` | `None`, `Excel1900`, or `Excel1904` from `TabularDateSystem` |

CSV reads and writes use `Text` and `None`.
XLSX input uses the actual cell kind and workbook date system.
Text and shared-string cells use `Text`. Formula cells use their cached result kind.
Native numbers retain invariant XML text, even when `Culture` selects another culture.
Native booleans use `true` or `false`. ISO date cells retain their date text.
The converter decides which kinds it accepts and how to interpret numeric dates.
It does not receive Excel display formats or formatted cell text.
Parser and schema validation still apply before conversion.

## Output and failures

`TryFormat` receives the property value, including null, and a reusable destination span.
It receives the configured culture, `Text`, and `None` in the context.
Write only the field value. The library adds CSV quotes, escapes, separators, and record endings.
Configured encoding and field/record limits still apply.

Return `true` and set `charsWritten` to the number of characters written.
Return `false` only when the destination is too small.
The library then grows its buffer and calls the method again.
Throw for a value that cannot be formatted.
Do not rely on the number of calls or retain the destination span.
Keep static converter methods safe for concurrent source enumerations.

`TryParse` returns `false` for invalid input.
Failed parsing and converter exceptions produce `TabularConversionException` on reads.
Formatting failures produce `TabularWriteException` on writes.
These exceptions keep column, property, and record location data.
XLSX errors also keep worksheet and cell location data.
They omit raw values and inner converter exceptions.
An `OutOfMemoryException` is not wrapped.
Cancellation exceptions thrown inside a converter are treated as conversion failures.
Use the operation's cancellation token for normal cancellation.

## Mapping and compilation

An override inherits the nearest converter annotation unless it declares another converter.
`[Column]` can rename a converted property.
`[Ignore]` cannot apply with an effective converter annotation.
The normal public setter or init accessor rules still apply.
Writing also requires an accessible getter.

Invalid converter mappings produce compiler diagnostic `LTF005`.
The generator checks type compatibility and accessibility before emitting mapping code.
Converter code must also compile under normal C# interface rules.
Converters can live in another referenced assembly when their types are accessible.
An open generic converter, such as `typeof(Converter<>)`, is not valid.
For nullable references, prefer a converter that explicitly implements the nullable interface.
`typeof` does not preserve nullable reference annotations on generic type arguments.

Generated mapping uses static interface calls with concrete converter types.
It needs no runtime reflection or registration.
Converters must also use AOT-compatible code when the application targets Native AOT.
