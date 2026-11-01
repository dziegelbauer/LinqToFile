# Sources and async queries

These lifetime and query rules apply to CSV and XLSX.
Use `Xlsx<T>` and `XlsxAsync<T>` instead of `Csv<T>` and `CsvAsync<T>` for workbooks.
The [XLSX guide](xlsx.md) explains archive setup, temporary storage, and synchronous ZIP metadata operations.

## Source kinds

| Entry point | Source | Ownership and reuse |
|---|---|---|
| `Csv<T>(path)` | File path | Fresh library-owned stream for each enumeration |
| `Csv<T>(stream, leaveOpen: true)` | Supplied stream | One enumeration, caller retains the stream |
| `Csv<T>(stream, leaveOpen: false)` | Supplied stream | One enumeration, library closes the stream |
| `Csv<T>(factory)` | `Func<Stream>` | Fresh library-owned stream from each factory call |
| `CsvAsync<T>(factory)` | `Func<CancellationToken, ValueTask<Stream>>` | Async factory, fresh library-owned stream for each call |

A factory must return a fresh readable stream on each call.
A null factory is invalid at configuration time.
A null or unreadable factory result fails during enumeration.

Supplied streams start at their current position.
CSV does not seek or rewind.
XLSX treats the current position as the archive origin and seeks within the remaining input.
Non-seekable streams are permitted. XLSX copies them to a temporary file before opening the archive.

## Deferred execution and ownership

A query does not open a file or call a factory until enumeration begins.
A supplied stream's first move claims its enumeration allowance.
All configurations derived from that source share the same allowance.
A failed or cancelled first move still consumes it.

Creating an enumerator does not claim the stream.
Disposing an unused enumerator does not close it.
With `leaveOpen: false`, disposal responsibility begins with the first move.

Library-owned streams close after completion, failure, cancellation, or early exit.
For owned streams, async enumeration uses the stream's asynchronous disposal API.
Caller-owned streams remain open.
A caller that uses an enumerator directly must dispose that enumerator.

Paths and factories permit independent concurrent enumerations.
A supplied-stream source permits one enumeration across sync and async modes.
A new query configuration does not give it another allowance.

## Sync and async query boundaries

A configured source supports standard synchronous LINQ.
It also supports direct `await foreach` through the async enumeration pattern.

`AsAsyncEnumerable()` returns an `IAsyncEnumerable<T>` for BCL async LINQ.
This explicit boundary avoids ambiguous sync and async operator overloads.
The configured source implements `IEnumerable<T>` rather than both interfaces.

The [executable sample](../samples/NativeAot/Program.cs) checks this query:

```csharp
var asyncAdults = await TabularFile.Csv<Person>(path)
    .AsAsyncEnumerable()
    .Where(person => person.Age >= 18)
    .Select(person => person.Name)
    .ToListAsync();
```

Async enumeration uses asynchronous reads.
XLSX additionally uses async archive creation, entry opening, XML reads, and spill-file reads/writes.
The BCL reads the ZIP central directory synchronously when its entries are first accessed.
Seeks, file creation, and metadata properties also remain synchronous.
Cancellation is checked around setup and while XML processes buffered nodes.
It does not wrap synchronous enumeration in `Task.Run`.
An async-only factory source rejects synchronous enumeration without a factory call.
A synchronous factory still opens its stream synchronously when an async query uses it.

## Materialization

| Method on the configured source | Result |
|---|---|
| `ToListAsync(cancellationToken)` | `ValueTask<List<T>>` |
| `ToArrayAsync(cancellationToken)` | `ValueTask<T[]>` |
| `ToDictionaryAsync(keySelector, comparer, cancellationToken)` | Dictionary of keys to records |
| `ToDictionaryAsync(keySelector, valueSelector, comparer, cancellationToken)` | Dictionary of selected keys and values |

The comparer is optional.
The cancellation token is optional.
Dictionary methods return `ValueTask<Dictionary<TKey, TValue>>`.

The sample also checks this dictionary query:

```csharp
var dictionary = await asyncSource.ToDictionaryAsync(person => person.Id, person => person.Name);
```

Duplicate keys cause `ArgumentException`.
The library's duplicate-key message omits the key value.
Null selectors fail immediately without a source read.
User callbacks keep their own exception behavior.
BCL materializers called directly after an explicit query boundary keep BCL error behavior.

List and array materializers use the .NET BCL.
Filtering and projection also use BCL async operators.
The library uses a shared dictionary builder to omit raw keys from duplicate-key errors.

## Cancellation

The async factory and I/O operations receive the enumeration token.
The query also checks cancellation between records.
The parser checks it while it processes buffered input.

Cancellation throws `OperationCanceledException`.
An owned stream still closes.
A supplied stream still consumes its allowance if the first move occurred.
A factory is not called when the token is already cancelled before acquisition.

The token can enter through `GetAsyncEnumerator(token)`, `WithCancellation(token)`, or an async materializer.
`WithCancellation` applies to the result of `AsAsyncEnumerable()`.
