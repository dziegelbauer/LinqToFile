using LinqToFile.Infrastructure;

namespace LinqToFile.Reading;

internal static class HeaderlessSchema<T> where T : class, ITabularRecord<T>
{
    internal static void ValidateIndexes()
    {
        var indexes = T.Mapper.ColumnIndexes;
        if (indexes.Length != T.Mapper.Columns.Length)
            throw new ArgumentException("Every mapped property must have a ColumnIndex annotation.");
        foreach (int index in indexes)
            if (index < 0) throw new ArgumentException("Every mapped property must have a ColumnIndex annotation.");
    }

    internal static (string[] Names, int[] Ordinals, string?[] Members) BindNames(TabularOptions options)
    {
        var names = new string[options.ColumnNames.Length];
        var lookup = new Dictionary<string, int>(names.Length, options.HeaderComparison);
        for (int i = 0; i < names.Length; i++)
        {
            string name = options.ColumnNames[i];
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Column names cannot be null, empty, or whitespace-only.");
            if (options.TrimHeaders) name = name.Trim();
            if (!lookup.TryAdd(name, i)) throw new ArgumentException("Duplicate column names are not allowed.");
            names[i] = name;
        }
        SchemaBinding<T>.ValidateMapping(options.HeaderComparison);
        var columns = T.Mapper.Columns;
        var members = T.Mapper.Members;
        var ordinals = new int[columns.Length];
        var targets = new string?[names.Length];
        for (int i = 0; i < columns.Length; i++)
        {
            if (!lookup.TryGetValue(columns[i], out ordinals[i]))
                throw new ArgumentException("A mapped column is missing from the supplied column names.");
            targets[ordinals[i]] = members[i];
        }
        return (names, ordinals, targets);
    }

    internal static void ValidateNames(TabularOptions options)
    {
        if (options.BindingMode == ColumnBindingMode.SuppliedNames) BindNames(options);
    }

    internal static int[] PrepareNames(TabularRow row, TabularOptions options)
    {
        var binding = BindNames(options);
        row.Headers = binding.Names;
        row.TargetMembers = binding.Members;
        ConfigureRow(row, options);
        return binding.Ordinals;
    }

    internal static int[] BindIndexes(TabularRow row, TabularOptions options)
    {
        ValidateIndexes();
        var indexes = T.Mapper.ColumnIndexes;
        var members = T.Mapper.Members;
        row.TargetMembers = new string?[row.Count];
        ConfigureRow(row, options);
        for (int i = 0; i < indexes.Length; i++)
        {
            int index = indexes[i];
            if (index >= row.Count)
                throw row.WithLocation(new TabularSchemaException("A mapped column index is outside the first data record.",
                    row.RecordNumber, row.LineNumber, row.FailureLineNumber, index == int.MaxValue ? null : index + 1, member: members[i]));
            row.TargetMembers[index] = members[i];
        }
        return [.. indexes];
    }

    private static void ConfigureRow(TabularRow row, TabularOptions options)
    {
        row.Culture = options.Culture;
        row.TrimFields = options.TrimFields;
    }
}
