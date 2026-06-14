namespace BetterRoute.Routing.Internal;

internal static class RouteMatcher
{
    public static bool TryMatch(
        string path,
        IReadOnlyList<CompiledRoute> tree,
        out IReadOnlyList<MatchedRoute> matched)
    {
        var segments = SplitPath(path);
        var chain = new List<MatchedRoute>();
        if (MatchLevel(tree, segments, offset: 0, chain))
        {
            matched = chain;
            return true;
        }
        matched = [];
        return false;
    }

    private static bool MatchLevel(
        IReadOnlyList<CompiledRoute> candidates,
        string[] segments,
        int offset,
        List<MatchedRoute> chain)
    {
        // Prefer literal-segment routes over parameter routes when paths overlap.
        var ordered = candidates
            .OrderByDescending(c => c.Segments.Count > 0 && !c.Segments[0].IsParameter);

        foreach (var candidate in ordered)
        {
            if (!TryConsume(candidate, segments, offset, out var captured, out var consumed))
                continue;

            var newOffset = offset + consumed;
            chain.Add(new MatchedRoute(candidate.Definition, captured));

            if (newOffset == segments.Length)
            {
                if (candidate.Children.Count == 0)
                    return true;

                // Allow falling through to an empty-path index child.
                var indexChild = candidate.Children.FirstOrDefault(c => c.Segments.Count == 0);
                if (indexChild is not null)
                {
                    chain.Add(new MatchedRoute(
                        indexChild.Definition,
                        EmptyParameters));
                    return true;
                }

                return true;
            }

            if (candidate.Children.Count > 0 &&
                MatchLevel(candidate.Children, segments, newOffset, chain))
            {
                return true;
            }

            chain.RemoveAt(chain.Count - 1);
        }

        return false;
    }

    private static bool TryConsume(
        CompiledRoute route,
        string[] segments,
        int offset,
        out IReadOnlyDictionary<string, string> captured,
        out int consumed)
    {
        var required = route.Segments.Count;

        if (required == 0)
        {
            captured = EmptyParameters;
            consumed = 0;
            return true;
        }

        if (offset + required > segments.Length)
        {
            captured = EmptyParameters;
            consumed = 0;
            return false;
        }

        Dictionary<string, string>? bag = null;
        for (var i = 0; i < required; i++)
        {
            var template = route.Segments[i];
            var urlPart = segments[offset + i];
            if (template.IsParameter)
            {
                bag ??= new Dictionary<string, string>(StringComparer.Ordinal);
                bag[template.Value] = Uri.UnescapeDataString(urlPart);
            }
            else if (!string.Equals(template.Value, urlPart, StringComparison.OrdinalIgnoreCase))
            {
                captured = EmptyParameters;
                consumed = 0;
                return false;
            }
        }

        captured = bag ?? EmptyParameters;
        consumed = required;
        return true;
    }

    private static string[] SplitPath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
            return [];
        return path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyParameters =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
