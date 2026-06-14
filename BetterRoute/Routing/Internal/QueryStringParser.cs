namespace BetterRoute.Routing.Internal;

/// <summary>
/// Parses a URL query string (<c>key1=value1&key2=value2</c>) into a
/// read-only dictionary mapping each key to all values for that key.
/// Bare keys (no <c>=</c>) map to an empty-string value, matching URLSearchParams behavior.
/// Plus (<c>+</c>) is treated as a literal character (RFC 3986), not decoded as space
/// (this differs from <c>application/x-www-form-urlencoded</c> and URLSearchParams, which decode <c>+</c> as space).
/// </summary>
public static class QueryStringParser
{
    /// <summary>
    /// Parses a URL query string into a read-only dictionary.
    /// </summary>
    /// <param name="queryString">The raw query string (without leading <c>?</c>), or <c>null</c>.</param>
    /// <returns>
    /// A dictionary mapping each key to all values for that key.
    /// Returns an empty dictionary for <c>null</c> or empty input.
    /// </returns>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Parse(string? queryString)
    {
        if (string.IsNullOrEmpty(queryString))
            return Empty;

        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var pair in queryString.Split('&'))
        {
            if (pair.Length == 0)
                continue;

            var eqIndex = pair.IndexOf('=');
            string key, value;
            if (eqIndex >= 0)
            {
                key = Uri.UnescapeDataString(pair[..eqIndex]);
                if (key.Length == 0)
                    continue;
                value = Uri.UnescapeDataString(pair[(eqIndex + 1)..]);
            }
            else
            {
                // Bare key: present with empty-string value (matches URLSearchParams behavior).
                key = Uri.UnescapeDataString(pair);
                if (key.Length == 0)
                    continue;
                value = string.Empty;
            }

            if (result.TryGetValue(key, out var existing))
            {
                if (existing is List<string> list)
                {
                    list.Add(value);
                }
                else
                {
                    result[key] = new List<string>(existing) { value };
                }
            }
            else
            {
                result[key] = new List<string> { value };
            }
        }

        return result.Count == 0 ? Empty : result;
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Empty =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
}
