namespace BetterRoute.Routing.Internal;

internal sealed record CompiledSegment(string Value, bool IsParameter);

internal sealed class CompiledRoute
{
    public required RouteDefinition Definition { get; init; }
    public required IReadOnlyList<CompiledSegment> Segments { get; init; }
    public required IReadOnlyList<CompiledRoute> Children { get; init; }

    /// <summary>
    /// Compiles a tree of <see cref="RouteDefinition"/> into <see cref="CompiledRoute"/> nodes.
    /// Expands aliases into synthetic entries and validates redirect configurations.
    /// </summary>
    /// <param name="routes">The route definitions to compile.</param>
    /// <param name="ancestorParameters">
    /// Parameter names available from ancestor routes. Internal — callers should omit this.
    /// </param>
    public static IReadOnlyList<CompiledRoute> Compile(
        IReadOnlyList<RouteDefinition> routes,
        IReadOnlySet<string>? ancestorParameters = null)
    {
        return Compile(routes, ancestorParameters, nameIndex: null, ancestorPath: null);
    }

    /// <summary>
    /// Compiles a tree of <see cref="RouteDefinition"/> into <see cref="CompiledRoute"/> nodes
    /// and populates <paramref name="index"/> with any named routes found in the tree.
    /// </summary>
    internal static IReadOnlyList<CompiledRoute> Compile(
        IReadOnlyList<RouteDefinition> routes,
        out NamedRouteIndex index)
    {
        var dict = new Dictionary<string, NamedRouteEntry>(StringComparer.Ordinal);
        var compiled = Compile(routes, ancestorParameters: null, dict, ancestorPath: null);
        index = dict.Count > 0
            ? new NamedRouteIndex(dict)
            : NamedRouteIndex.Empty;
        return compiled;
    }

    private static IReadOnlyList<CompiledRoute> Compile(
        IReadOnlyList<RouteDefinition> routes,
        IReadOnlySet<string>? ancestorParameters,
        Dictionary<string, NamedRouteEntry>? nameIndex,
        string? ancestorPath)
    {
        var result = new List<CompiledRoute>(routes.Count);
        var ancesParams = ancestorParameters ?? new HashSet<string>(StringComparer.Ordinal);

        foreach (var route in routes)
        {
            var ownSegments = ParseSegments(route.Path);
            var ownParams = GetParameterNames(ownSegments);

            // Validate the route configuration.
            ValidateRoute(route, ancesParams, ownParams);

            // Build the set of known parameters for children (ancestors + own).
            var childParams = Union(ancesParams, ownParams);

            // Compute the full path for this route's children
            // and for name indexing of this route itself.
            var childAncestorPath = BuildFullPath(ancestorPath, route.Path);

            // Compile children once — shared by the canonical route and all aliases.
            var children = route.Children is { Count: > 0 } c
                ? Compile(c, childParams, nameIndex, childAncestorPath)
                : (IReadOnlyList<CompiledRoute>)[];

            // Register name if present — only on the canonical entry, not aliases.
            if (route.Name is not null && nameIndex is not null)
            {
                var fullPath = childAncestorPath ?? "";
                if (!nameIndex.TryAdd(route.Name, new NamedRouteEntry(fullPath, route)))
                {
                    var existing = nameIndex[route.Name];
                    throw new InvalidOperationException(
                        $"Route name \"{route.Name}\" is already in use by " +
                        $"\"{existing.FullPathTemplate}\". " +
                        $"Duplicate declared at \"{fullPath}\".");
                }
            }

            // Canonical entry.
            result.Add(new CompiledRoute
            {
                Definition = route,
                Segments = ownSegments,
                Children = children,
            });

            // Synthetic alias entries share the same Definition and Children by reference.
            if (route.Aliases is { Count: > 0 } aliases)
            {
                foreach (var alias in aliases)
                {
                    result.Add(new CompiledRoute
                    {
                        Definition = route,
                        Segments = ParseSegments(alias),
                        Children = children,
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Builds the full path for a child route by concatenating the ancestor path
    /// with this route's own path segments.
    /// </summary>
    private static string? BuildFullPath(string? ancestorPath, string ownPath)
    {
        if (string.IsNullOrEmpty(ownPath))
            return ancestorPath;

        if (ancestorPath is null)
            return ownPath;

        return ancestorPath + "/" + ownPath;
    }

    private static void ValidateRoute(
        RouteDefinition route,
        IReadOnlySet<string> ancestorParameters,
        HashSet<string> ownParams)
    {
        var hasRedirectTo = route.RedirectTo is not null;
        var hasRedirectFactory = route.RedirectToFactory is not null;
        var hasComponent = route.Component is not null;
        var hasNamedComponents = route.Components is { Count: > 0 };
        var hasChildren = route.Children is { Count: > 0 };
        var hasAliases = route.Aliases is { Count: > 0 };

        // RedirectTo and RedirectToFactory are mutually exclusive.
        if (hasRedirectTo && hasRedirectFactory)
            throw new InvalidOperationException(
                $"Route \"{route.Path}\" cannot have both RedirectTo and RedirectToFactory.");

        // A redirect and an alias together is nonsensical.
        if ((hasRedirectTo || hasRedirectFactory) && hasAliases)
            throw new InvalidOperationException(
                $"Route \"{route.Path}\" cannot have both a redirect and aliases.");

        // A redirect must not also specify a Component.
        if ((hasRedirectTo || hasRedirectFactory) && hasComponent)
            throw new InvalidOperationException(
                $"Route \"{route.Path}\" cannot have both a redirect and a Component.");

        // Every route must have a Component, a named component, a redirect, or children.
        if (!hasComponent && !hasNamedComponents && !hasRedirectTo && !hasRedirectFactory && !hasChildren)
            throw new InvalidOperationException(
                $"Route \"{route.Path}\" must have a Component, a named component, a redirect, or children.");

        // Validate that all :param references in the redirect template are bound.
        if (hasRedirectTo)
        {
            ValidateRedirectParams(route.Path, route.RedirectTo!, ancestorParameters, ownParams);
        }
    }

    private static void ValidateRedirectParams(
        string routePath,
        string template,
        IReadOnlySet<string> ancestorParams,
        HashSet<string> ownParams)
    {
        var knownParams = new HashSet<string>(ancestorParams, StringComparer.Ordinal);
        foreach (var p in ownParams)
            knownParams.Add(p);

        foreach (var paramName in ParseRedirectParamRefs(template))
        {
            if (!knownParams.Contains(paramName))
                throw new InvalidOperationException(
                    $"Redirect template \"{template}\" on route \"{routePath}\" references " +
                    $"parameter \":{paramName}\" which is not available in this route or its ancestors.");
        }
    }

    /// <summary>Extracts the set of parameter names from compiled segments.</summary>
    private static HashSet<string> GetParameterNames(IReadOnlyList<CompiledSegment> segments)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var seg in segments)
        {
            if (seg.IsParameter)
                names.Add(seg.Value);
        }
        return names;
    }

    /// <summary>
    /// Yields <c>:paramName</c> references found in a redirect template string.
    /// Only recognises identifiers that start with a letter or underscore and
    /// contain letters, digits, or underscores.
    /// </summary>
    private static IEnumerable<string> ParseRedirectParamRefs(string template)
    {
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] == ':' && i + 1 < template.Length && IsParamStartChar(template[i + 1]))
            {
                var j = i + 1;
                while (j < template.Length && IsParamChar(template[j]))
                    j++;
                yield return template[(i + 1)..j];
                i = j - 1;
            }
        }
    }

    private static bool IsParamStartChar(char c) => char.IsLetter(c) || c == '_';
    private static bool IsParamChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static HashSet<string> Union(IReadOnlySet<string>? a, HashSet<string> b)
    {
        if (a is null || a.Count == 0)
            return new HashSet<string>(b, StringComparer.Ordinal);

        var result = new HashSet<string>(a, StringComparer.Ordinal);
        foreach (var item in b)
            result.Add(item);
        return result;
    }

    internal static IReadOnlyList<CompiledSegment> ParseSegments(string path)
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
