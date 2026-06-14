namespace BetterRoute.Routing;

/// <summary>
/// Cascaded state available to every component in the matched route chain.
/// Grab it via <c>[CascadingParameter] public RouterState State { get; set; } = default!;</c>.
/// </summary>
public sealed record RouterState(
    IReadOnlyList<MatchedRoute> Matched,
    int CurrentDepth,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Query,
    string Url,
    string Path,
    string? Fragment)
{
    /// <summary>The matched route at <see cref="CurrentDepth"/>.</summary>
    public MatchedRoute Current => Matched[CurrentDepth];

    /// <summary>Returns the first value for <paramref name="key"/>, or null if not present.</summary>
    public string? GetQuery(string key) =>
        Query.TryGetValue(key, out var values) && values.Count > 0 ? values[0] : null;

    /// <summary>Returns all values for <paramref name="key"/>, or an empty list if not present.</summary>
    public IReadOnlyList<string> GetQueryValues(string key) =>
        Query.TryGetValue(key, out var values) ? values : [];

    /// <summary>Returns the same state rebased to a different depth in the matched chain. Used by <c>RouterOutlet</c>.</summary>
    public RouterState AtDepth(int depth) => this with { CurrentDepth = depth };
}
