namespace BetterRoute.Routing.Internal;

/// <summary>
/// A single entry in the <see cref="NamedRouteIndex"/>.
/// </summary>
/// <param name="FullPathTemplate">
/// The full path template with ancestor segments joined by <c>/</c>,
/// e.g. <c>"users/:userId/posts/:postId"</c>. No leading slash.
/// </param>
/// <param name="Definition">The original <see cref="RouteDefinition"/>.</param>
internal sealed record NamedRouteEntry(string FullPathTemplate, RouteDefinition Definition);

/// <summary>
/// Read-only index of named routes built during tree compilation.
/// Stored on <see cref="RouterState"/> and used by its
/// <c>ResolveUrl</c> / <c>NavigateTo</c> methods.
/// </summary>
internal sealed class NamedRouteIndex
{
    private readonly Dictionary<string, NamedRouteEntry> _routes;

    /// <summary>
    /// Creates the index from a dictionary populated during compilation.
    /// </summary>
    internal NamedRouteIndex(Dictionary<string, NamedRouteEntry> routes)
    {
        _routes = routes;
    }

    /// <summary>Looks up a named route by its <see cref="RouteDefinition.Name"/>.</summary>
    /// <exception cref="InvalidOperationException">No route is registered with <paramref name="name"/>.</exception>
    internal NamedRouteEntry Get(string name)
    {
        if (_routes.TryGetValue(name, out var entry))
            return entry;

        throw new InvalidOperationException(
            $"No route found with name \"{name}\".");
    }

    /// <summary>Number of registered names.</summary>
    internal int Count => _routes.Count;

    /// <summary>Shared empty instance used when no routes have names.</summary>
    internal static readonly NamedRouteIndex Empty =
        new(new Dictionary<string, NamedRouteEntry>(StringComparer.Ordinal));
}
