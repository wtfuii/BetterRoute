using BetterRoute.Routing.Internal;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Rendering;

namespace BetterRoute.Routing;

public partial class BetterRouter : ComponentBase, IDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>
    /// The root route definitions. Build these as a tree of
    /// <see cref="RouteDefinition"/> records with nested <see cref="RouteDefinition.Children"/>.
    /// Compiled into an internal match tree on first render and whenever the reference changes.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<RouteDefinition> Routes { get; set; } = [];

    /// <summary>
    /// Optional component type to render when no route matches the current URL.
    /// When <c>null</c>, unmatched URLs produce no output.
    /// </summary>
    [Parameter] public Type? NotFound { get; set; }

    /// <summary>
    /// Optional global guard that runs on every navigation after leave guards
    /// and before per-route enter guards.
    /// </summary>
    [Parameter] public NavigationGuard? BeforeEach { get; set; }

    /// <summary>
    /// Optional callback invoked when a guard throws an exception.
    /// The exception is caught and the navigation is treated as cancelled.
    /// </summary>
    [Parameter] public Action<Exception>? OnNavigationError { get; set; }

    private IReadOnlyList<CompiledRoute> _compiled = [];
    private IReadOnlyList<RouteDefinition>? _lastRoutesRef;
    private NamedRouteIndex _namedRouteIndex = NamedRouteIndex.Empty;
    private RouterState? _state;
    private bool _initialNavigationComplete;
    private bool _isRestoring;
    private bool _isPopState;
    private int _redirectCount;
    private CancellationTokenSource? _navigateCts;
    private readonly GuardRegistrar _guardRegistrar = new();

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
    }

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_lastRoutesRef, Routes))
        {
            _compiled = CompiledRoute.Compile(Routes, out _namedRouteIndex);
            _lastRoutesRef = Routes;
        }

        if (!_initialNavigationComplete)
        {
            _ = NavigateToAsync(Navigation.Uri);
        }
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        // When we restore the URL after a Cancel, skip re-processing
        // to avoid an infinite loop.
        if (_isRestoring)
        {
            _isRestoring = false;
            return;
        }

        _isPopState = e.IsNavigationIntercepted;
        await NavigateToAsync(e.Location);
    }

    private async Task NavigateToAsync(string absoluteUri)
    {
        // Cancel any in-flight navigation and create a fresh token.
        _navigateCts?.Cancel();
        _navigateCts = new CancellationTokenSource();
        var ct = _navigateCts.Token;

        try
        {
            await NavigateInternalAsync(absoluteUri, ct);
        }
        catch (OperationCanceledException)
        {
            // Swallowed — a newer navigation superseded this one.
        }
        finally
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task NavigateInternalAsync(string absoluteUri, CancellationToken ct)
    {
        if (_isRestoring)
            return;

        ct.ThrowIfCancellationRequested();

        // 1. Resolve match for the target URL.
        var path = ToRelativePath(absoluteUri, out var queryString, out var fragment);
        var matchResult = RouteMatcher.TryMatch(path, _compiled);

        RouterState? toState = null;

        switch (matchResult)
        {
            case MatchResult.Success success:
                toState = BuildRouterState(success.Matched, absoluteUri, path, queryString, fragment);
                break;

            case MatchResult.StaticRedirect redirect:
                await HandleRedirectAsync(
                    redirect.RedirectTemplate,
                    redirect.Matched,
                    path, queryString, fragment,
                    ct);
                return;

            case MatchResult.DynamicRedirect redirect:
                await HandleRedirectAsync(
                    null, // template
                    redirect.Matched,
                    path, queryString, fragment,
                    ct,
                    isDynamic: true,
                    factory: redirect.Factory);
                return;

            case MatchResult.NotFound:
                _state = null;
                _initialNavigationComplete = true;
                return;
        }

        // Should never be null here — Success sets it, all other MatchResult
        // variants return early. Defensive check for null-safety.
        if (toState is null)
            return;

        var fromState = _state;
        var leaveGuards = _guardRegistrar.GetLeaveGuards();

        // 2-4. Run the guard pipeline (pure function, testable in isolation).
        var pipelineResult = await GuardPipeline.RunAsync(
            fromState,
            toState,
            toState.Matched,
            leaveGuards,
            BeforeEach,
            OnNavigationError,
            ct,
            _isPopState);

        // 5-7. Handle the pipeline result.
        switch (pipelineResult)
        {
            case GuardPipelineResult.Continue:
                _state = toState;
                _initialNavigationComplete = true;
                _redirectCount = 0;
                break;

            case GuardPipelineResult.Cancel:
                _initialNavigationComplete = true;
                if (fromState is not null)
                {
                    // The URL has already changed in the address bar (for back/forward).
                    // Restore it with replace:true so we don't create a new history entry.
                    _isRestoring = true;
                    Navigation.NavigateTo(
                        BuildUrl(fromState),
                        replace: true,
                        forceLoad: false);
                }
                // _state stays as fromState, so the old page remains visible.
                break;

            case GuardPipelineResult.Redirect redirect:
                _redirectCount++;
                if (_redirectCount > 10)
                {
                    var ex = new InvalidOperationException(
                        "Navigation guard redirect loop detected: exceeded 10 redirect hops.");
                    OnNavigationError?.Invoke(ex);
                    _initialNavigationComplete = true;
                    return;
                }
                // Trigger a new navigation to the redirect target.
                // This fires LocationChanged, which restarts the pipeline from step 1.
                Navigation.NavigateTo(redirect.Target, forceLoad: false);
                break;
        }
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

    /// <summary>Rebuilds a relative URL from a <see cref="RouterState"/>'s path, query, and fragment.</summary>
    private static string BuildUrl(RouterState state)
    {
        var url = state.Path;
        if (state.Query.Count > 0)
        {
            var pairs = state.Query.SelectMany(kv =>
                kv.Value.Select(v => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(v)}"));
            url += "?" + string.Join("&", pairs);
        }
        if (state.Fragment is not null)
            url += "#" + state.Fragment;
        return url;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyParameters =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Builds a <see cref="RouterState"/> from a matched chain and URL parts.</summary>
    private RouterState BuildRouterState(
        IReadOnlyList<MatchedRoute> matched,
        string absoluteUri,
        string path,
        string? queryString,
        string? fragment)
    {
        var merged = MergeParameters(matched);
        var query = QueryStringParser.Parse(queryString);
        return new RouterState(matched, CurrentDepth: 0, merged, query, absoluteUri, path, fragment)
        {
            NamedRoutes = _namedRouteIndex,
            NavigateCallback = (url, replace) =>
                Navigation.NavigateTo(url, forceLoad: false, replace: replace),
        };
    }

    /// <summary>
    /// Resolves a redirect target from a matched route and initiates navigation.
    /// Shares the <see cref="_redirectCount"/> counter with the guard-pipeline redirect path.
    /// </summary>
    private async Task HandleRedirectAsync(
        string? template,
        IReadOnlyList<MatchedRoute> matched,
        string currentPath,
        string? queryString,
        string? fragment,
        CancellationToken ct,
        bool isDynamic = false,
        Func<RouterState, string?>? factory = null)
    {
        string target;

        if (isDynamic)
        {
            // Build a provisional RouterState for the factory to inspect.
            var provisional = BuildRouterState(matched, currentPath, currentPath, queryString, fragment);
            var factoryResult = factory!(provisional);
            if (factoryResult is null)
            {
                // Factory returned null: no redirect and no component — treat as not-found.
                _state = null;
                _initialNavigationComplete = true;
                return;
            }
            target = RedirectTargetResolver.Resolve(
                factoryResult, MergeParameters(matched), currentPath, queryString, fragment);
        }
        else
        {
            target = RedirectTargetResolver.Resolve(
                template!, MergeParameters(matched), currentPath, queryString, fragment);
        }

        _redirectCount++;
        if (_redirectCount > 10)
        {
            var ex = new InvalidOperationException(
                "Redirect loop detected: exceeded 10 redirect hops.");
            OnNavigationError?.Invoke(ex);
            _initialNavigationComplete = true;
            return;
        }

        // replace: true keeps the back-button working naturally —
        // the user lands on the canonical URL without an extra history entry.
        // Trim the leading '/' so NavigateTo resolves the path relative to the
        // app's base URI rather than the origin root. This keeps redirects
        // working when the app is deployed under a non-root base path,
        // e.g. GitHub Pages at /BetterRoute/.
        Navigation.NavigateTo(target.TrimStart('/'), replace: true, forceLoad: false);
    }

    private RenderFragment RenderRoot(RouterState state) => builder =>
    {
        builder.OpenComponent(0, state.Current.Definition.Component!);
        builder.CloseComponent();
    };

    /// <summary>
    /// Unsubscribes from navigation events and cancels any in-flight navigation.
    /// </summary>
    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        _navigateCts?.Cancel();
        _navigateCts?.Dispose();
    }
}
