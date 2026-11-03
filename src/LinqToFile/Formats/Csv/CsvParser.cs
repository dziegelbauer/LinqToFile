using LinqToFile.Infrastructure;
using LinqToFile.Reading;

namespace LinqToFile.Formats.Csv;

// One parser state machine; only buffer refills differ between sync and async reads.
internal sealed class CsvParser
{
    private readonly BufferedCharReader reader;
    private readonly CsvOptions options;
    internal TabularRow Record { get; }
    internal long RecordByteOffset { get; private set; }
    internal bool EndsWithTerminator { get; private set; }
    private LineEnding? recordEnding;
    internal LineEnding? ReadRecordEnding() => recordEnding == LineEnding.Cr && reader.Peek() == '\n'
        ? LineEnding.CrLf : recordEnding;
    internal async ValueTask<LineEnding?> ReadRecordEndingAsync(CancellationToken token) =>
        recordEnding == LineEnding.Cr && await reader.PeekAsync(token).ConfigureAwait(false) == '\n'
            ? LineEnding.CrLf : recordEnding;
    private long line = 1;
    private long recordNumber;
    private long sourceLength;
    private long characterLine = 1;
    private State state;
    private bool hasInput;
    private bool needsReset = true;
    private bool skipLf;
    private bool quotedCr;

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
            var result = ParseAvailable(default);
            if (result != ParseResult.NeedInput) return result == ParseResult.Record;
            reader.Fill();
        }
    }

    internal async ValueTask<bool> ReadAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = ParseAvailable(cancellationToken);
            if (result != ParseResult.NeedInput) return result == ParseResult.Record;
            await reader.FillAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private ParseResult ParseAvailable(CancellationToken cancellationToken)
    {
        if (needsReset) ResetRecord();
        int processed = 0;
        while (reader.TryRead(out var c))
        {
            EndsWithTerminator = c is '\r' or '\n';
            if ((processed++ & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
            characterLine = line;
            if (skipLf)
            {
                skipLf = false;
                if (c == '\n') continue;
            }

            if (state == State.Quoted)
            {
                bool followsCr = quotedCr;
                quotedCr = false;
                if (c == '\n' && followsCr) characterLine--;
                CountSourceCharacter();
                if (c == options.Quote) state = State.Closed;
                else
                {
                    Append(c);
                    if (c == '\r') { line++; quotedCr = true; }
                    else if (c == '\n' && !followsCr) line++;
                }
                continue;
            }

            if (c is '\r' or '\n')
            {
                Record.FailureLineNumber = line;
                if (!hasInput && !options.SkipBlankLines) throw Error("A blank physical line is not permitted.");
                if (c == '\r') skipLf = true;
                line++;
                if (!hasInput)
                {
                    ResetRecord();
                    continue;
                }
                recordEnding = c == '\r' ? LineEnding.Cr : LineEnding.Lf;
                CompleteRecord();
                return ParseResult.Record;
            }

            if (!hasInput) RecordByteOffset = reader.LastByteOffset;
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
        if (!reader.Ended) return ParseResult.NeedInput;
        characterLine = line;
        Record.FailureLineNumber = line;
        if (state == State.Quoted) throw Error("An unterminated quoted field was found.");
        if (!hasInput) return ParseResult.End;
        CompleteRecord();
        return ParseResult.Record;
    }

    private void ResetRecord()
    {
        Record.Reset();
        Record.LineNumber = line;
        Record.RecordNumber = recordNumber + 1;
        sourceLength = 0;
        recordEnding = null;
        state = State.Start;
        hasInput = false;
        quotedCr = false;
        needsReset = false;
    }

    private void CompleteRecord()
    {
        Record.EndField();
        recordNumber++;
        needsReset = true;
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
        return new(message, Record.RecordNumber, Record.LineNumber, characterLine, ordinal + 1, header, member);
    }

    private enum State { Start, Unquoted, Quoted, Closed }
    private enum ParseResult { NeedInput, Record, End }
}
