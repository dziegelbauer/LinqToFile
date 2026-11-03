using System.Buffers;
using System.Globalization;
using System.Numerics;

namespace LinqToFile.Reading;

// Exact decimal coefficient/exponent view. No double conversion and no giant powers for extreme exponents.
internal readonly ref struct ExactDecimal
{
    private readonly ReadOnlySpan<char> significant;
    private readonly int digits;
    private readonly long power;
    internal bool Negative { get; }
    internal bool IsZero => digits == 0;
    internal bool IsIntegral => IsZero || power >= 0;
    private long Order => digits + power;

    private ExactDecimal(ReadOnlySpan<char> significant, int digits, long power, bool negative)
    { this.significant = significant; this.digits = digits; this.power = power; Negative = negative && digits != 0; }

    internal static bool TryParse(ReadOnlySpan<char> value, out ExactDecimal number)
    {
        number = default;
        value = value.Trim();
        int i = 0;
        bool negative = false;
        if (i < value.Length && value[i] is '+' or '-') negative = value[i++] == '-';
        int first = -1, last = -1, firstDigit = 0, lastDigit = 0, total = 0, fractional = 0;
        bool point = false;
        while (i < value.Length)
        {
            char c = value[i];
            if (char.IsAsciiDigit(c))
            {
                if (c != '0')
                {
                    if (first < 0) { first = i; firstDigit = total; }
                    last = i; lastDigit = total;
                }
                total++;
                if (point) fractional++;
                i++;
            }
            else if (c == '.' && !point) { point = true; i++; }
            else break;
        }
        if (total == 0) return false;
        long exponent = 0;
        if (i < value.Length && value[i] is 'e' or 'E')
        {
            i++;
            bool minus = false;
            if (i < value.Length && value[i] is '+' or '-') minus = value[i++] == '-';
            int start = i;
            while (i < value.Length && char.IsAsciiDigit(value[i]))
            {
                exponent = Math.Min(10_000_000_000L, exponent * 10 + value[i++] - '0');
            }
            if (i == start) return false;
            if (minus) exponent = -exponent;
        }
        if (i != value.Length) return false;
        if (first < 0) { number = default; return true; }
        number = new(value[first..(last + 1)], lastDigit - firstDigit + 1,
            exponent - fractional + total - lastDigit - 1, negative);
        return true;
    }

    private BigInteger Coefficient()
    {
        char[]? rented = null;
        Span<char> buffer = digits <= 256 ? stackalloc char[digits] : (rented = ArrayPool<char>.Shared.Rent(digits));
        try
        {
            int index = 0;
            foreach (char c in significant) if (c != '.') buffer[index++] = c;
            return BigInteger.Parse(buffer[..index], NumberStyles.None, CultureInfo.InvariantCulture);
        }
        finally { if (rented is not null) ArrayPool<char>.Shared.Return(rented); }
    }

    internal bool TryInteger(out BigInteger result)
    {
        result = default;
        if (IsZero) return true;
        if (!IsIntegral || Order > 39) return false;
        result = Coefficient() * BigInteger.Pow(10, (int)power);
        if (Negative) result = -result;
        return true;
    }

    internal bool TryTicks(out long ticks, long minimum = long.MinValue, long maximum = long.MaxValue)
    {
        ticks = 0;
        if (IsZero || Order < -12) return true;
        if (Order > 8 || power < -int.MaxValue) return false;
        BigInteger scaled = Coefficient() * TimeSpan.TicksPerDay;
        if (power >= 0) scaled *= BigInteger.Pow(10, (int)power);
        else
        {
            BigInteger divisor = BigInteger.Pow(10, (int)-power);
            scaled = BigInteger.DivRem(scaled, divisor, out var remainder);
            BigInteger unrounded = Negative ? -scaled : scaled;
            if (unrounded < minimum || unrounded > maximum || !remainder.IsZero &&
                (Negative ? unrounded == minimum : unrounded == maximum)) return false;
            int comparison = (remainder * 2).CompareTo(divisor);
            if (comparison > 0 || comparison == 0 && !scaled.IsEven) scaled++;
        }
        if (Negative) scaled = -scaled;
        if (scaled < minimum || scaled > maximum) return false;
        ticks = (long)scaled;
        return true;
    }

    internal bool IsLessThanOne => IsZero || Order <= 0;
    internal bool IsDaySixty
    {
        get
        {
            if (Negative || Order != 2) return false;
            int first = significant[0] - '0';
            int second = 0;
            for (int i = 1; i < significant.Length; i++)
                if (significant[i] != '.') { second = significant[i] - '0'; break; }
            return first == 6 && second == 0;
        }
    }
}
