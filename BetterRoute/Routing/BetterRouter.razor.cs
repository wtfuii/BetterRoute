using BetterRoute.Routing.Internal;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Rendering;

namespace BetterRoute.Routing;

public partial class BetterRouter : ComponentBase, IDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter, EditorRequired]
    public IReadOnlyList<RouteDefinition> Routes { get; set; } = [];

    [Parameter] public Type? NotFound { get; set; }

    private IReadOnlyList<CompiledRoute> _compiled = [];
    private IReadOnlyList<RouteDefinition>? _lastRoutesRef;
    private RouterState? _state;

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
    }

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_lastRoutesRef, Routes))
        {
            _compiled = CompiledRoute.Compile(Routes);
            _lastRoutesRef = Routes;
        }
        _state = Match(Navigation.Uri);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        _state = Match(e.Location);
        StateHasChanged();
    }

    private RouterState? Match(string absoluteUri)
    {
        var path = ToRelativePath(absoluteUri, out var queryString, out var fragment);
        if (!RouteMatcher.TryMatch(path, _compiled, out var matched))
            return null;

        var merged = MergeParameters(matched);
        var query = QueryStringParser.Parse(queryString);
        return new RouterState(matched, CurrentDepth: 0, merged, query, absoluteUri, path, fragment);
    }

    private string ToRelativePath(string absoluteUri, out string? queryString, out string? fragment)
    {
        var relative = Navigation.ToBaseRelativePath(absoluteUri);

        // Strip leading slash for consistent split-then-reassemble processing.
        relative = relative.TrimStart('/');

        fragment = null;
        var fragmentIndex = relative.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            fragment = relative[(fragmentIndex + 1)..];
            relative = relative[..fragmentIndex];
        }

        var queryIndex = relative.IndexOf('?');
        if (queryIndex >= 0)
        {
            queryString = relative[(queryIndex + 1)..];
            relative = relative[..queryIndex];
        }
        else
        {
            queryString = null;
        }

        return "/" + relative;
    }

    private static IReadOnlyDictionary<string, string> MergeParameters(IReadOnlyList<MatchedRoute> matched)
    {
        Dictionary<string, string>? merged = null;
        foreach (var level in matched)
        {
            if (level.SegmentParameters.Count == 0)
                continue;
            merged ??= new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (k, v) in level.SegmentParameters)
                merged[k] = v;
        }
        return merged ?? EmptyParameters;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyParameters =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private RenderFragment RenderRoot(RouterState state) => builder =>
    {
        builder.OpenComponent(0, state.Current.Definition.Component);
        builder.CloseComponent();
    };

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
