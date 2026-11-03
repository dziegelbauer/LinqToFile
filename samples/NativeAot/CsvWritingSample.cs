using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using LinqToFile;
using SampleModels;

internal static class CsvWritingSample
{
    internal static async Task RunAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "LinqToFile-Create-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "people.csv");
            var file = TabularFile.Csv<Person>(path)
                .WithDelimiter(';')
                .WithQuote('\'')
                .WithNewLine(LineEnding.Lf);
            long written = await file.CreateAsync(new[]
            {
                new Person { Id = 1, Name = "Ada; 'Lovelace'" },
                new Person { Id = 2, Name = "Grace" }
            });
            var rows = await file.ToArrayAsync();
            if (written != 2 || rows.Length != 2 || rows[0].Name != "Ada; 'Lovelace'" || rows[1].Id != 2)
                throw new InvalidOperationException("CSV creation sample failed.");

            long appended = await file.AppendAsync(new Person { Id = 3, Name = "Katherine" });
            appended += file.AppendRange(new[]
            {
                new Person { Id = 4, Name = "Dorothy" },
                new Person { Id = 5, Name = "Mary" }
            });
            if (appended != 3 || file.Count() != 5)
                throw new InvalidOperationException("CSV append sample failed.");
            if (file.Append(new Person { Id = 6, Name = "Margaret" }) != 1 ||
                await file.AppendRangeAsync(People()) != 1 ||
                await file.WithAppendValidation(false).AppendRangeAsync(new[] { new Person { Id = 7, Name = "Annie" } }) != 1)
                throw new InvalidOperationException("CSV append overloads failed.");

            long inserted = await file.InsertAsync(1, new Person { Id = 10, Name = "Rosalind" });
            inserted += file.InsertRange(2, new[]
            {
                new Person { Id = 11, Name = "Hedy" },
                new Person { Id = 12, Name = "Jean" }
            });
            if (inserted != 3 || !file.Skip(1).Take(3).Select(row => row.Id).SequenceEqual(new[] { 10, 11, 12 }))
                throw new InvalidOperationException("CSV insertion sample failed.");
            if (file.Insert(0, new Person { Id = 13, Name = "Frances" }) != 1 ||
                await file.InsertRangeAsync(0, new[] { new Person { Id = 14, Name = "Evelyn" } }) != 1 ||
                await file.InsertRangeAsync(file.LongCount(), People()) != 1)
                throw new InvalidOperationException("CSV insertion overloads failed.");

            var direct = TabularFile.Csv<Person>(Path.Combine(directory, "direct.csv")).WithInPlaceWrites();
            written = await direct.CreateAsync(People());
            if (written != 1 || direct.Single().Id != 3) throw new InvalidOperationException("Direct async creation failed.");
            var quick = direct.WithAppendValidation(false);
            if (quick.Append(new Person { Id = 8, Name = "Joan" }) != 1 ||
                await quick.AppendRangeAsync(People()) != 1 || direct.Count() != 3)
                throw new InvalidOperationException("Direct quick append failed.");
            if (await quick.InsertRangeAsync(1, People()) != 1 || direct.ElementAt(1).Id != 3 ||
                direct.Insert(0, new Person { Id = 15, Name = "Betty" }) != 1)
                throw new InvalidOperationException("Direct insertion failed.");
            var headers = TabularFile.Csv<Person>(Path.Combine(directory, "headers.csv"));
            if (headers.Create() != 0 || headers.Any()) throw new InvalidOperationException("Header creation failed.");
            var replacement = headers.WithExistingFileBehavior(ExistingFileBehavior.Overwrite);
            if (await replacement.CreateAsync(People()) != 1 || headers.Single().Id != 3)
                throw new InvalidOperationException("Explicit CSV overwrite failed.");

            var scalar = new ScalarRecord
            {
                UInt8 = byte.MaxValue, Int8 = sbyte.MinValue, Int16 = short.MinValue, UInt16 = ushort.MaxValue,
                Int32 = int.MinValue, UInt32 = uint.MaxValue, Int64 = long.MinValue, UInt64 = ulong.MaxValue,
                Int128 = Int128.MinValue, UInt128 = UInt128.MaxValue, Half = Half.Epsilon, Single = float.Epsilon,
                Double = double.MaxValue, Decimal = decimal.MaxValue, Character = '雪', Boolean = true,
                Id = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
                Timestamp = new DateTime(2026, 11, 2, 1, 2, 3).AddTicks(1234567),
                TimestampOffset = new DateTimeOffset(2026, 11, 2, 1, 2, 3, TimeSpan.FromMinutes(345)).AddTicks(7654321),
                Date = DateOnly.MinValue, Time = TimeOnly.MaxValue, Duration = TimeSpan.MinValue,
                State = SampleStatus.Ready, Access = SamplePermission.Read | SamplePermission.Write, Name = " raw 雪😀 "
            };
            var scalars = TabularFile.Csv<ScalarRecord>(Path.Combine(directory, "scalars.csv"))
                .WithEncoding(new UnicodeEncoding(false, true, true))
                .WithCulture(CultureInfo.GetCultureInfo("th-TH"))
                .WithBufferSize(1);
            if (scalars.Create(new[] { scalar }) != 1) throw new InvalidOperationException();
            var result = scalars.Single();
            if (result.Int128 != scalar.Int128 || result.UInt128 != scalar.UInt128 ||
                result.Half != scalar.Half || result.Single != scalar.Single || result.Double != scalar.Double ||
                result.Decimal != scalar.Decimal || result.Timestamp != scalar.Timestamp ||
                result.Timestamp.Kind != DateTimeKind.Unspecified ||
                result.TimestampOffset.Ticks != scalar.TimestampOffset.Ticks ||
                result.TimestampOffset.Offset != scalar.TimestampOffset.Offset ||
                result.Date != scalar.Date || result.Time != scalar.Time || result.Duration != scalar.Duration ||
                result.Access != scalar.Access || result.State != scalar.State || result.Name != scalar.Name)
                throw new InvalidOperationException("Cross-assembly scalar serialization failed.");
            if (await scalars.AppendAsync(scalar) != 1 || scalars.Count() != 2 ||
                scalars.Last().TimestampOffset.Offset != scalar.TimestampOffset.Offset)
                throw new InvalidOperationException("Cross-assembly scalar append failed.");
            if (await scalars.InsertAsync(1, scalar) != 1 || scalars.Count() != 3 ||
                scalars.ElementAt(1).TimestampOffset.Offset != scalar.TimestampOffset.Offset)
                throw new InvalidOperationException("Cross-assembly scalar insertion failed.");
            Console.WriteLine("CSV writing: creation, append, insertion, sync/async inputs, both modes, quick append, and scalar round trips passed.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static async IAsyncEnumerable<Person> People([EnumeratorCancellation] CancellationToken token = default)
    {
        await Task.Yield();
        token.ThrowIfCancellationRequested();
        yield return new Person { Id = 3, Name = "Katherine" };
    }
}
