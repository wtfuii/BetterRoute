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
/// <param name="Component">The component type rendered when this route matches.</param>
/// <param name="Children">Optional nested routes, matched against the remaining URL after this node consumes its segments.</param>
/// <param name="Name">Optional name for the route. Reserved for future named-route resolution.</param>
public sealed record RouteDefinition(
    string Path,
    Type Component,
    IReadOnlyList<RouteDefinition>? Children = null,
    string? Name = null)
{
    /// <summary>
    /// Optional per-route guard executed before entering this route.
    /// Runs only for route nodes that are new to the matched chain
    /// (i.e., not reused from the previous navigation by reference
    /// equality of the <c>RouteDefinition</c> instance).
    /// </summary>
    public NavigationGuard? BeforeEnter { get; init; }
}
