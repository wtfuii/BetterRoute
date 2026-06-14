using BetterRoute.Routing;

namespace BetterRoute.Routing.Internal;

/// <summary>
/// Result of a route-matching operation. A closed discriminated union
/// that replaces the previous <c>bool TryMatch(...)</c> pattern.
/// </summary>
internal abstract record MatchResult
{
    /// <summary>A route matched successfully. The component should be rendered.</summary>
    public sealed record Success(IReadOnlyList<MatchedRoute> Matched) : MatchResult;

    /// <summary>
    /// A route with <see cref="RouteDefinition.RedirectTo"/> matched.
    /// The <see cref="RedirectTemplate"/> still contains <c>:param</c>
    /// placeholders that must be substituted by the caller using captured parameters.
    /// </summary>
    public sealed record StaticRedirect(
        IReadOnlyList<MatchedRoute> Matched,
        string RedirectTemplate) : MatchResult;

    /// <summary>
    /// A route with <see cref="RouteDefinition.RedirectToFactory"/> matched.
    /// The caller must build a provisional <see cref="RouterState"/> and invoke
    /// the factory. If the factory returns <c>null</c>, the match is treated as not-found.
    /// </summary>
    public sealed record DynamicRedirect(
        IReadOnlyList<MatchedRoute> Matched,
        Func<RouterState, string?> Factory) : MatchResult;

    /// <summary>No route matched the URL.</summary>
    public sealed record NotFound : MatchResult;

    private MatchResult() { }
}
