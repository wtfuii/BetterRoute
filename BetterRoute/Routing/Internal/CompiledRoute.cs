namespace BetterRoute.Routing.Internal;

internal sealed record CompiledSegment(string Value, bool IsParameter);

internal sealed class CompiledRoute
{
    public required RouteDefinition Definition { get; init; }
    public required IReadOnlyList<CompiledSegment> Segments { get; init; }
    public required IReadOnlyList<CompiledRoute> Children { get; init; }

    public static IReadOnlyList<CompiledRoute> Compile(IReadOnlyList<RouteDefinition> routes)
    {
        var result = new List<CompiledRoute>(routes.Count);
        foreach (var route in routes)
        {
            result.Add(new CompiledRoute
            {
                Definition = route,
                Segments = ParseSegments(route.Path),
                Children = route.Children is { Count: > 0 } c ? Compile(c) : [],
            });
        }
        return result;
    }

    private static IReadOnlyList<CompiledSegment> ParseSegments(string path)
    {
        if (string.IsNullOrEmpty(path))
            return [];

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var segments = new CompiledSegment[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            segments[i] = part.StartsWith(':')
                ? new CompiledSegment(part[1..], IsParameter: true)
                : new CompiledSegment(part, IsParameter: false);
        }
        return segments;
    }
}
