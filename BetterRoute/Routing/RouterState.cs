using System.Globalization;
using System.Text;
using BetterRoute.Routing.Internal;

namespace BetterRoute.Routing;

/// <summary>
/// Cascaded state available to every component in the matched route chain.
/// Grab it via <c>[CascadingParameter] public RouterState State { get; set; } = default!;</c>.
/// </summary>
public sealed record RouterState(
    IReadOnlyList<MatchedRoute> Matched,
    int CurrentDepth,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Query,
    string Url,
    string Path,
    string? Fragment)
{
    /// <summary>
    /// The named-route index used by <see cref="ResolveUrl"/> and <see cref="NavigateTo(string,System.Collections.Generic.IReadOnlyDictionary{string,string}?,bool)"/>.
    /// Set by <see cref="BetterRouter"/> during route compilation.
    /// </summary>
    internal NamedRouteIndex NamedRoutes { get; init; } = NamedRouteIndex.Empty;

    /// <summary>The matched route at <see cref="CurrentDepth"/>.</summary>
    public MatchedRoute Current => Matched[CurrentDepth];

    /// <summary>Returns the value for <paramref name="key"/>, or null if not present.</summary>
    public string? GetParameter(string key) =>
        Parameters.TryGetValue(key, out var value) ? value : null;

    /// <summary>Returns the first value for <paramref name="key"/>, or null if not present.</summary>
    public string? GetQuery(string key) =>
        Query.TryGetValue(key, out var values) && values.Count > 0 ? values[0] : null;

    /// <summary>Returns all values for <paramref name="key"/>, or an empty list if not present.</summary>
    public IReadOnlyList<string> GetQueryValues(string key) =>
        Query.TryGetValue(key, out var values) ? values : [];

    /// <summary>Returns the same state rebased to a different depth in the matched chain. Used by <c>RouterOutlet</c>.</summary>
    public RouterState AtDepth(int depth) => this with { CurrentDepth = depth };

    /// <summary>
    /// Callback set by <see cref="BetterRouter"/> that performs the actual navigation.
    /// </summary>
    internal Action<string, bool>? NavigateCallback { get; init; }

    // ── Named-route resolution ────────────────────────────────────────

    /// <summary>
    /// Returns the URL for the named route, substituting <paramref name="parameters"/>
    /// into <c>:param</c> segments. Extra keys not appearing in the template
    /// are appended as a query string.
    /// </summary>
    /// <param name="routeName">The <see cref="RouteDefinition.Name"/> value.</param>
    /// <param name="parameters">Values for path parameters and optional extra query parameters.</param>
    /// <exception cref="InvalidOperationException">
    /// The route name is unknown, or a required parameter is missing.
    /// </exception>
    public string ResolveUrl(
        string routeName,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        var index = NamedRoutes ?? NamedRouteIndex.Empty;
        var entry = index.Get(routeName);
        return BuildUrlFromTemplate(entry.FullPathTemplate, entry.Definition.Name,
            parameters ?? EmptyParameters);
    }

    /// <summary>
    /// Resolves the named route and navigates to it.
    /// When <paramref name="parameters"/> is <c>null</c>, the current
    /// <see cref="Parameters"/> are used as defaults.
    /// </summary>
    /// <param name="routeName">The <see cref="RouteDefinition.Name"/> value.</param>
    /// <param name="parameters">
    /// Values for path parameters and optional extra query parameters.
    /// When <c>null</c>, the current <see cref="Parameters"/> are reused
    /// (convenient for sibling navigation).
    /// </param>
    /// <param name="replace">When <c>true</c>, replaces the current history entry.</param>
    public void NavigateTo(
        string routeName,
        IReadOnlyDictionary<string, string>? parameters = null,
        bool replace = false)
    {
        if (NavigateCallback is null)
            throw new InvalidOperationException(
                "RouterState.NavigateTo requires a navigation callback, " +
                "which is set when the state is created by BetterRouter.");

        var effective = parameters ?? Parameters;
        var url = ResolveUrl(routeName, effective);
        NavigateCallback(url, replace);
    }

    /// <summary>
    /// Resolves the named route with parameters from an anonymous object and navigates to it.
    /// Property values are converted via <c>Convert.ToString(value, CultureInfo.InvariantCulture)</c>.
    /// </summary>
    public void NavigateTo(string routeName, object? parameters, bool replace = false)
    {
        var dict = AnonymousObjectToDictionary(parameters);
        // When no parameter object is given, fall back to current Parameters.
        var effective = parameters is null ? Parameters : dict;
        NavigateTo(routeName, effective, replace);
    }

    // ── URL building ──────────────────────────────────────────────────

    private static string BuildUrlFromTemplate(
        string fullPathTemplate,
        string? routeName,
        IReadOnlyDictionary<string, string> parameters)
    {
        var sb = new StringBuilder();
        var consumed = new HashSet<string>(StringComparer.Ordinal);

        var segments = CompiledRoute.ParseSegments(fullPathTemplate);
        foreach (var segment in segments)
        {
            sb.Append('/');
            if (segment.IsParameter)
            {
                if (!parameters.TryGetValue(segment.Value, out var value))
                {
                    throw new InvalidOperationException(
                        $"Parameter \":{segment.Value}\" is required " +
                        (routeName is not null ? $"by route \"{routeName}\" " : "") +
                        $"(template: \"{fullPathTemplate}\") but was not provided.");
                }
                sb.Append(Uri.EscapeDataString(value));
                consumed.Add(segment.Value);
            }
            else
            {
                sb.Append(segment.Value);
            }
        }

        // Root route with no segments → "/".
        if (sb.Length == 0)
            sb.Append('/');

        // Extra keys → query string.
        var extras = new List<string>();
        foreach (var (key, value) in parameters)
        {
            if (!consumed.Contains(key))
                extras.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }
        if (extras.Count > 0)
        {
            sb.Append('?');
            sb.Append(string.Join("&", extras));
        }

        return sb.ToString();
    }

    // ── Anonymous-object conversion ───────────────────────────────────

    private static IReadOnlyDictionary<string, string> AnonymousObjectToDictionary(object? parameters)
    {
        if (parameters is null)
            return EmptyParameters;

        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in parameters.GetType().GetProperties())
        {
            var value = prop.GetValue(parameters);
            var str = value is null
                ? string.Empty
                : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            dict[prop.Name] = str;
        }
        return dict;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyParameters =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
