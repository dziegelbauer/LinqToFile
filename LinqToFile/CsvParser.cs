using LinqToFile.Infrastructure;

namespace LinqToFile;

// One record and one StreamReader buffer per enumeration. No strings for numeric fields.
internal sealed class CsvParser(TextReader reader)
{
    internal CsvRecord Record { get; } = new();
    private long line = 1;
    private long recordNumber;

    internal bool Read()
    {
        while (true)
        {
            Record.Reset();
            Record.LineNumber = line;
            Record.RecordNumber = recordNumber + 1;
            var state = State.Start;
            var hasInput = false;
            while (true)
            {
                var next = reader.Read();
                if (next < 0)
                {
                    Record.FailureLineNumber = line;
                    if (state == State.Quoted) throw Error("An unterminated quoted field was found.");
                    if (!hasInput) return false;
                    Record.EndField();
                    recordNumber++;
                    return true;
                }

                var c = (char)next;
                if (state == State.Quoted)
                {
                    if (c == '"') state = State.Closed;
                    else
                    {
                        Record.Append(c);
                        if (c == '\r')
                        {
                            if (reader.Peek() == '\n') Record.Append((char)reader.Read());
                            line++;
                        }
                        else if (c == '\n') line++;
                    }
                    continue;
                }

                if (c is '\r' or '\n')
                {
                    Record.FailureLineNumber = line;
                    if (c == '\r' && reader.Peek() == '\n') reader.Read();
                    line++;
                    if (!hasInput) break; // Skip an entirely blank physical line.
                    Record.EndField();
                    recordNumber++;
                    return true;
                }

                hasInput = true;
                if (c == ',')
                {
                    Record.EndField();
                    state = State.Start;
                }
                else if (c == '"')
                {
                    if (state == State.Start) state = State.Quoted;
                    else if (state == State.Closed)
                    {
                        Record.Append('"');
                        state = State.Quoted;
                    }
                    else throw Error("A quote was found inside an unquoted field.");
                }
                else
                {
                    if (state == State.Closed) throw Error("Unexpected text after a closing quote.");
                    Record.Append(c);
                    state = State.Unquoted;
                }
            }
        }
    }

    private CsvFormatException Error(string message) =>
        new(message, Record.RecordNumber, Record.LineNumber, line, Record.Count + 1);

    private enum State { Start, Unquoted, Quoted, Closed }
}
