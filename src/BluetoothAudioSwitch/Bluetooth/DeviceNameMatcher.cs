using System.Text;

namespace BluetoothAudioSwitch.Bluetooth;

internal enum MatchKind
{
    Found,
    NotFound,
    Ambiguous,
}

internal sealed record MatchResult<T>(MatchKind Kind, T? Match, IReadOnlyList<T> Candidates);

/// <summary>
/// Forgiving device-name matching, so shortcuts and typed commands work with
/// "chris buds4", "Chris's Buds4" or "Chris’s Buds4" alike.
/// </summary>
internal static class DeviceNameMatcher
{
    /// <summary>
    /// Tries, in order: exact match, substring match, then word-prefix match
    /// (every query word is the start of some word in the name). The first
    /// tier that yields any result decides the outcome.
    /// </summary>
    public static MatchResult<T> Match<T>(IEnumerable<T> items, Func<T, string> getName, string query)
    {
        var list = items.ToList();
        var normalizedQuery = Normalize(query);
        if (normalizedQuery.Length == 0)
        {
            return new MatchResult<T>(MatchKind.NotFound, default, []);
        }

        var queryWords = Words(normalizedQuery);
        Func<string, bool>[] tiers =
        [
            name => name == normalizedQuery,
            name => name.Contains(normalizedQuery, StringComparison.Ordinal),
            name => queryWords.All(q => Words(name).Any(w => w.StartsWith(q, StringComparison.Ordinal))),
        ];

        foreach (var tier in tiers)
        {
            var matches = list.Where(item => tier(Normalize(getName(item)))).ToList();
            if (matches.Count == 1)
            {
                return new MatchResult<T>(MatchKind.Found, matches[0], matches);
            }

            if (matches.Count > 1)
            {
                return new MatchResult<T>(MatchKind.Ambiguous, default, matches);
            }
        }

        return new MatchResult<T>(MatchKind.NotFound, default, []);
    }

    public static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasSpace = true;
        foreach (var ch in value.Trim())
        {
            var c = ch switch
            {
                '‘' or '’' or 'ʼ' or '`' or '´' => '\'',
                _ => char.ToLowerInvariant(ch),
            };

            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                }

                lastWasSpace = true;
                continue;
            }

            builder.Append(c);
            lastWasSpace = false;
        }

        return builder.ToString().TrimEnd();
    }

    private static string[] Words(string normalized) => normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
