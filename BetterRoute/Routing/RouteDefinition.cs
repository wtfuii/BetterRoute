namespace BetterRoute.Routing;

/// <summary>
/// Declarative description of one node in the route tree.
/// Pass a list of these (with nested <see cref="Children"/>) to <c>&lt;BetterRouter Routes="..."/&gt;</c>.
/// </summary>
/// <param name="Path">
/// Path template for this node, relative to its parent. May contain multiple segments
/// (e.g. <c>"posts/:postId"</c>). A <c>:name</c> segment captures a path parameter named <c>name</c>.
/// Use <c>""</c> for an index/default child.
/// </param>
/// <param name="Component">The component type rendered when this route matches. Set to <c>null</c>
/// when <paramref name="RedirectTo"/> or <paramref name="RedirectToFactory"/> is used instead.</param>
/// <param name="Children">Optional nested routes, matched against the remaining URL after this node consumes its segments.</param>
/// <param name="Name">Optional name for the route. Reserved for future named-route resolution.</param>
/// <param name="Components">
/// Optional named components keyed by outlet name. The default outlet renders
/// <see cref="Component"/>; named outlets render components from this dictionary.
/// Use with <c>&lt;RouterOutlet Name="..."/&gt;</c> to populate multiple regions
/// (e.g. sidebar, modal) from a single route node.
/// </param>
/// <param name="RedirectTo">
/// Optional static redirect target. When this route matches, the browser is redirected
/// to this URL. May contain <c>:param</c> placeholders that are substituted with captured
/// parameter values. Relative paths (<c>"../sibling"</c>) are resolved against the current URL.
/// </param>
/// <param name="RedirectToFactory">
/// Optional dynamic redirect factory. Called at match time with the provisional
/// <see cref="RouterState"/>. Return a redirect target URL, or <c>null</c> to treat
/// the match as not-found.
/// </param>
/// <param name="Aliases">
/// Optional alternative paths that render the same component without changing the URL.
/// Each alias shares the same <see cref="Component"/> and <see cref="Children"/> by reference.
/// </param>
public sealed record RouteDefinition(
    string Path,
    Type? Component = null,
    IReadOnlyList<RouteDefinition>? Children = null,
    string? Name = null,
    IReadOnlyDictionary<string, Type>? Components = null,
    string? RedirectTo = null,
    Func<RouterState, string?>? RedirectToFactory = null,
    IReadOnlyList<string>? Aliases = null)
{
    /// <summary>
    /// Optional per-route guard executed before entering this route.
    /// Runs only for route nodes that are new to the matched chain
    /// (i.e., not reused from the previous navigation by reference
    /// equality of the <c>RouteDefinition</c> instance).
    /// </summary>
    public NavigationGuard? BeforeEnter { get; init; }
}
