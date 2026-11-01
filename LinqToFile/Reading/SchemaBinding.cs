using LinqToFile.Infrastructure;

namespace LinqToFile.Reading;

internal static class SchemaBinding<T> where T : class, ITabularRecord<T>
{
    internal static void ValidateMapping(StringComparer comparer)
    {
        var columns = T.Mapper.Columns;
        for (int i = 0; i < columns.Length; i++)
        for (int j = 0; j < i; j++)
            if (comparer.Equals(columns[i], columns[j]))
                throw new ArgumentException("Multiple mapped properties have equal column names under the selected comparison.", nameof(comparer));
    }

    internal static int[] Bind(TabularRow row, TabularOptions options)
    {
        var mapper = T.Mapper;
        row.TrimFields = options.TrimHeaders;
        var headers = new string[row.Count];
        var headerMap = new Dictionary<string, int>(row.Count, options.HeaderComparison);
        for (int i = 0; i < headers.Length; i++)
        {
            var header = row.GetField(i).ToString();
            if (string.IsNullOrWhiteSpace(header))
                throw new TabularSchemaException("A header is empty or whitespace-only.", 1, row.LineNumber, row.FailureLineNumber, i + 1);
            if (!headerMap.TryAdd(header, i))
                throw new TabularSchemaException("A duplicate header was found.", 1, row.LineNumber, row.FailureLineNumber, i + 1, header);
            headers[i] = header;
        }
        row.Headers = headers;
        row.Culture = options.Culture;
        row.TargetMembers = new string?[headers.Length];

        var names = mapper.Columns;
        var members = mapper.Members;
        var ordinals = new int[names.Length];
        var used = new HashSet<string>(options.HeaderComparison);
        for (int i = 0; i < names.Length; i++)
        {
            if (!used.Add(names[i]))
                throw new TabularSchemaException("Multiple properties map to the same column.", 1, row.LineNumber, row.FailureLineNumber, header: names[i], member: members[i]);
            if (!headerMap.TryGetValue(names[i], out ordinals[i]))
                throw new TabularSchemaException("A mapped column is missing.", 1, row.LineNumber, row.FailureLineNumber, header: names[i], member: members[i]);
            row.TargetMembers[ordinals[i]] = members[i];
        }
        row.TrimFields = options.TrimFields;
        return ordinals;
    }
}
