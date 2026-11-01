using LinqToFile.Infrastructure;

namespace LinqToFile;

internal sealed class CsvParser
{
    private readonly BufferedCharReader reader;
    private readonly CsvOptions options;
    internal CsvRecord Record { get; }
    private long line = 1;
    private long recordNumber;
    private long sourceLength;

    internal CsvParser(TextReader reader, CsvOptions options)
    {
        this.reader = new(reader, options.BufferSize);
        this.options = options;
        Record = new(Math.Min(options.BufferSize, 1024), options.MaxRecordLength);
    }

    internal bool Read()
    {
        while (true)
        {
            Record.Reset();
            Record.LineNumber = line;
            Record.RecordNumber = recordNumber + 1;
            sourceLength = 0;
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
                    CountSourceCharacter();
                    if (c == options.Quote) state = State.Closed;
                    else
                    {
                        Append(c);
                        if (c == '\r')
                        {
                            if (reader.Peek() == '\n')
                            {
                                reader.Read();
                                CountSourceCharacter();
                                Append('\n');
                            }
                            line++;
                        }
                        else if (c == '\n') line++;
                    }
                    continue;
                }

                if (c is '\r' or '\n')
                {
                    Record.FailureLineNumber = line;
                    if (!hasInput && !options.SkipBlankLines)
                        throw Error("A blank physical line is not permitted.");
                    if (c == '\r' && reader.Peek() == '\n') reader.Read();
                    line++;
                    if (!hasInput) break;
                    Record.EndField();
                    recordNumber++;
                    return true;
                }

                CountSourceCharacter();
                hasInput = true;
                if (c == options.Delimiter)
                {
                    Record.EndField();
                    state = State.Start;
                }
                else if (c == options.Quote)
                {
                    if (state == State.Start) state = State.Quoted;
                    else if (state == State.Closed)
                    {
                        Append(options.Quote);
                        state = State.Quoted;
                    }
                    else throw Error("A quote was found inside an unquoted field.");
                }
                else
                {
                    if (state == State.Closed) throw Error("Unexpected text after a closing quote.");
                    Append(c);
                    state = State.Unquoted;
                }
            }
        }
    }

    private void CountSourceCharacter()
    {
        sourceLength++;
        if (options.MaxRecordLength is int maximum && sourceLength > maximum)
            throw Error("The record exceeds the configured maximum length.");
    }

    private void Append(char value)
    {
        if (options.MaxFieldLength is int maximum && Record.CurrentFieldLength >= maximum)
            throw Error("The field exceeds the configured maximum length.");
        Record.Append(value);
    }

    private CsvFormatException Error(string message)
    {
        int ordinal = Record.Count;
        string? header = ordinal < Record.Headers.Length ? Record.Headers[ordinal] : null;
        string? member = ordinal < Record.TargetMembers.Length ? Record.TargetMembers[ordinal] : null;
        return new(message, Record.RecordNumber, Record.LineNumber, line, ordinal + 1, header, member);
    }

    private enum State { Start, Unquoted, Quoted, Closed }
}
