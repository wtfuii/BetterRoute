namespace BetterRoute.Routing;

/// <summary>
/// One level of a successful match: the route node that matched and the parameters
/// its own segments captured (excluding parameters captured by ancestors).
/// </summary>
public sealed record MatchedRoute(
    RouteDefinition Definition,
    IReadOnlyDictionary<string, string> SegmentParameters);
