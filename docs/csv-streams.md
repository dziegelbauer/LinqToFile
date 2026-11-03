# Export CSV to streams

Use `TabularFile.CsvWriter<T>` to export typed records to a writable stream.
It works with memory, HTTP response bodies, compression streams, and other writable destinations.
Each `Write` or `WriteAsync` call writes a complete CSV document: one header and optional data rows.

The usual model annotations, converters, temporal formats, and column tokens apply.
The destination does not need to support reading or seeking.
Existing `TabularFile.Csv<T>(stream)` overloads still read data; the writer API is separate.

## Export to a supplied stream

This example uses the `InventoryItem` model from the [column token guide](column-tokens.md).
The writer leaves a supplied stream open by default:

```csharp
using var stream = new MemoryStream();
var writer = TabularFile.CsvWriter<InventoryItem>(stream)
    .WithDelimiter(';');
long written = await writer.WriteAsync(new[]
{
    new InventoryItem { Id = 1, Available = true, Note = "fresh", ExpiresOn = new DateOnly(2026, 12, 31) }
});
stream.Position = 0;
var rows = await TabularFile.Csv<InventoryItem>(stream)
    .WithDelimiter(';')
    .ToListAsync();
```

Writing starts at the stream's current position.
The library does not seek, rewind, truncate, or inspect existing content.
Existing bytes before that position remain untouched.
Ordinary writes can overwrite bytes at that position; any remaining tail is not removed.
Prepare a seekable destination yourself when you need to replace its content.

A supplied-stream writer allows one export across all derived configurations.
Creating a writer or changing its options does not consume that allowance.
An export attempt claims it after argument validation, including attempts that fail or are already cancelled.
Invalid arguments do not consume it.
Overlapping or later attempts throw `InvalidOperationException`.
Separate writer instances do not coordinate access to the same underlying stream.

Use `leaveOpen: false` to transfer stream disposal to the export operation.
Keep a supplied stream under your own lifetime management if no export is started.
The configuration object does not hold an open writer session and is not disposable.

## Export through a factory

Factories open a fresh stream for each call, so the same configuration can export repeatedly.
For an existing directory:

```csharp
var exporter = TabularFile.CsvWriter<InventoryItem>(() =>
    File.Create(Path.Combine(directory, Guid.NewGuid() + ".csv")));
await exporter.WriteAsync(rows);
await exporter.WriteAsync(rows);
```

The library owns factory-returned streams and disposes them after success, failure, or cancellation.
Async exports await asynchronous disposal before reporting success.
A factory can return a wrapper such as `GZipStream`; disposal then finalizes that owned wrapper.
Use the wrapper's own `leaveOpen` option when another component owns its underlying stream.

Use `TabularFile.CsvWriterAsync<T>(factory)` for a factory with the signature `Func<CancellationToken, ValueTask<Stream>>`.
It supports `WriteAsync` only. A synchronous call throws without invoking the factory.
An ordinary `Func<Stream>` factory supports both sync and async exports.
It is called synchronously even during `WriteAsync`.

Factory-backed writers allow concurrent exports with independent encoders and buffers.
The factory must support those concurrent calls and return an independent stream each time.
It must not reuse one stream for multiple exports.
No path or cross-process locking is added around a user factory.

The [runnable sample](../samples/NativeAot/StreamWritingSample.cs) checks supplied streams, repeated factories, async-only output, and compression.
It also runs in the package and Native AOT checks.

## Operations and options

| Operation | Output |
|---|---|
| `Write()` / `WriteAsync(token)` | Header only; returns zero |
| `Write(record)` / `WriteAsync(record, token)` | Header and one row |
| `Write(records)` | Header and streamed `IEnumerable<T>` rows |
| `WriteAsync(records, token)` | Header and streamed `IEnumerable<T>` or `IAsyncEnumerable<T>` rows |

Successful methods return the number of data rows as `long` or `ValueTask<long>`.
The header is not counted. An empty sequence still writes the header.
Input is processed once without collecting the whole sequence.
Generated column names are sorted with ordinal comparison, as with new CSV files.

Options return a new configuration and leave the original unchanged:

- `WithEncoding` and `WithCulture` snapshot the supplied values.
- `WithDelimiter`, `WithQuote`, and `WithNewLine` select CSV syntax.
- `WithFieldTrimming` controls the read-back null-token collision check.
- `WithHeaderTrimming` and `WithHeaderComparison` validate generated header compatibility.
- `WithMaxFieldLength` and `WithMaxRecordLength` enforce the same output limits as [file writing](csv-writing.md).

The default encoding is strict UTF-8 without a BOM; the default record ending is LF.
If the selected encoding supplies a preamble, it is written once at the start of this export.
This applies even when the underlying stream is already at a nonzero position.
No BOM detection or existing-file encoding inference occurs.

Stream writers do not expose path-only replacement, quick-append, or in-place write options.
Use [CSV file writing](csv-writing.md) for file creation, append, and insertion.

## Completion, cancellation, and errors

Successful exports finalize their encoder and flush the destination before returning.
`WriteAsync` uses asynchronous output and flushing with the operation's cancellation token.
It does not call synchronous output methods on the supplied destination.
This cannot guarantee that a custom stream, synchronous factory, or input callback avoids its own synchronous work.

Flushing does not provide crash durability or finish a caller-owned compression wrapper.
When `leaveOpen` is true, its caller still disposes that wrapper to complete its format.

Cancellation remains active after writing starts, including async input and the final flush.
Cancelled or failed exports can leave partial headers, rows, or encoded content.
There is no rollback, temporary-file staging, or complete-document guarantee.
The library does not flush pending CSV encoder data after a failure.
Owned stream disposal still runs and can perform its own format cleanup.
Existing file-path cancellation and replacement behavior is unchanged.

Model, schema, conversion, and output-limit errors keep the existing private diagnostics.
I/O, factory, and input-enumeration exceptions retain their .NET types.
A stream cleanup error does not replace an existing export error, except for `OutOfMemoryException`.
If disposal fails after otherwise successful output, that failure is reported instead of a successful count.
