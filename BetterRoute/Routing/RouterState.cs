namespace BetterRoute.Routing;

/// <summary>
/// Cascaded state available to every component in the matched route chain.
/// Grab it via <c>[CascadingParameter] public RouterState State { get; set; } = default!;</c>.
/// </summary>
public sealed record RouterState(
    IReadOnlyList<MatchedRoute> Matched,
    int CurrentDepth,
    IReadOnlyDictionary<string, string> Parameters,
    string Url,
    string Path)
{
    /// <summary>The matched route at <see cref="CurrentDepth"/>.</summary>
    public MatchedRoute Current => Matched[CurrentDepth];

    /// <summary>Returns the same state rebased to a different depth in the matched chain. Used by <c>RouterOutlet</c>.</summary>
    public RouterState AtDepth(int depth) => this with { CurrentDepth = depth };
}
