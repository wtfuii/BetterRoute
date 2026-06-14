namespace BetterRoute.Routing.Internal;

internal static class RouteMatcher
{
    /// <summary>
    /// Attempts to match a URL <paramref name="path"/> against a compiled route tree.
    /// Returns a <see cref="MatchResult"/> that indicates success, a redirect, or not-found.
    /// </summary>
    public static MatchResult TryMatch(
        string path,
        IReadOnlyList<CompiledRoute> tree)
    {
        var segments = SplitPath(path);
        var chain = new List<MatchedRoute>();
        var result = MatchLevel(tree, segments, offset: 0, chain);
        return result ?? new MatchResult.NotFound();
    }

    private static MatchResult? MatchLevel(
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
                // Check for a redirect on the candidate itself.
                var redirectResult = CheckRedirect(candidate, chain);
                if (redirectResult is not null)
                    return redirectResult;

                if (candidate.Children.Count == 0)
                    return new MatchResult.Success(chain.ToList());

                // Allow falling through to an empty-path index child.
                var indexChild = candidate.Children.FirstOrDefault(c => c.Segments.Count == 0);
                if (indexChild is not null)
                {
                    chain.Add(new MatchedRoute(
                        indexChild.Definition,
                        EmptyParameters));

                    // Check for a redirect on the index child.
                    var idxRedirect = CheckRedirect(indexChild, chain);
                    if (idxRedirect is not null)
                        return idxRedirect;

                    return new MatchResult.Success(chain.ToList());
                }

                return new MatchResult.Success(chain.ToList());
            }

            // Recurse into children; propagate any result (Success, redirect, or null).
            if (candidate.Children.Count > 0)
            {
                var childResult = MatchLevel(candidate.Children, segments, newOffset, chain);
                if (childResult is not null)
                    return childResult;
            }

            chain.RemoveAt(chain.Count - 1);
        }

        return null;
    }

    /// <summary>
    /// If <paramref name="route"/> has a <see cref="RouteDefinition.RedirectTo"/> or
    /// <see cref="RouteDefinition.RedirectToFactory"/>, returns the appropriate
    /// <see cref="MatchResult"/>. Otherwise returns <c>null</c>.
    /// </summary>
    private static MatchResult? CheckRedirect(CompiledRoute route, List<MatchedRoute> chain)
    {
        if (route.Definition.RedirectTo is { } redirectTo)
            return new MatchResult.StaticRedirect(chain.ToList(), redirectTo);

        if (route.Definition.RedirectToFactory is { } factory)
            return new MatchResult.DynamicRedirect(chain.ToList(), factory);

        return null;
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
