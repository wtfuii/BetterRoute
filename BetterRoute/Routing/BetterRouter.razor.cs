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
            _compiled = CompiledRoute.Compile(Routes);
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
        RouterState? toState = null;

        if (RouteMatcher.TryMatch(path, _compiled, out var matched))
        {
            var merged = MergeParameters(matched);
            var query = QueryStringParser.Parse(queryString);
            toState = new RouterState(matched, CurrentDepth: 0, merged, query, absoluteUri, path, fragment);
        }

        // No match — bypass guards and render NotFound.
        if (toState is null)
        {
            _state = null;
            _initialNavigationComplete = true;
            return;
        }

        var fromState = _state;
        var leaveGuards = _guardRegistrar.GetLeaveGuards();

        // 2-4. Run the guard pipeline (pure function, testable in isolation).
        var pipelineResult = await GuardPipeline.RunAsync(
            fromState,
            toState,
            matched,
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
                        "/" + fromState.Path.TrimStart('/'),
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
        _navigateCts?.Cancel();
        _navigateCts?.Dispose();
    }
}
