using System.Text;

namespace BluetoothAudioSwitch.Bluetooth;

internal sealed record MatchFound<T>(T Match);

internal sealed record AmbiguousMatch<T>(IReadOnlyList<T> Candidates);

internal sealed record NoMatch;

internal union MatchResult<T>(MatchFound<T>, AmbiguousMatch<T>, NoMatch);

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
    public static MatchResult<T> Match<T>(
        IEnumerable<T> items,
        Func<T, string> getName,
        string query
    )
    {
        var list = items.ToList();
        var normalizedQuery = Normalize(query);
        if (normalizedQuery.Length == 0)
        {
            return new NoMatch();
        }

        var queryWords = Words(normalizedQuery);
        Func<string, bool>[] tiers =
        [
            name => name == normalizedQuery,
            name => name.Contains(normalizedQuery, StringComparison.Ordinal),
            name =>
                queryWords.All(q =>
                    Words(name).Any(w => w.StartsWith(q, StringComparison.Ordinal))
                ),
        ];

        foreach (var tier in tiers)
        {
            var matches = list.Where(item => tier(Normalize(getName(item)))).ToList();
            if (matches.Count == 1)
            {
                return new MatchFound<T>(matches[0]);
            }

            if (matches.Count > 1)
            {
                return new AmbiguousMatch<T>(matches);
            }
        }

        return new NoMatch();
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

    private static string[] Words(string normalized) =>
        normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
