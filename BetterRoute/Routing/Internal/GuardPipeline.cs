namespace BetterRoute.Routing.Internal;

/// <summary>
/// Pure-function guard pipeline: executes <c>BeforeRouteLeave</c>,
/// global <c>BeforeEach</c>, and per-route <c>BeforeEnter</c> in order.
/// Free of Blazor dependencies so it can be unit-tested directly.
/// </summary>
internal static class GuardPipeline
{
    public static async ValueTask<GuardPipelineResult> RunAsync(
        RouterState? fromState,
        RouterState toState,
        IReadOnlyList<MatchedRoute> newMatched,
        IReadOnlyList<IBeforeRouteLeave> leaveGuards,
        NavigationGuard? beforeEach,
        Action<Exception>? onError,
        CancellationToken ct,
        bool isPopState = false)
    {
        ct.ThrowIfCancellationRequested();

        var ctx = new NavigationContext(fromState, toState, isPopState);

        // Phase 1: BeforeRouteLeave on currently-mounted components (deepest-first).
        if (fromState is not null)
        {
            foreach (var guard in leaveGuards)
            {
                ct.ThrowIfCancellationRequested();
                var result = await ExecuteGuard(
                    () => guard.CanLeaveAsync(ctx, ct), onError);
                if (result is GuardPipelineResult.Cancel or GuardPipelineResult.Redirect)
                    return result;
            }
        }

        // Phase 2: Global BeforeEach.
        if (beforeEach is not null)
        {
            ct.ThrowIfCancellationRequested();
            var result = await ExecuteGuard(
                () => beforeEach(ctx, ct), onError);
            if (result is GuardPipelineResult.Cancel or GuardPipelineResult.Redirect)
                return result;
        }

        // Phase 3: Per-route BeforeEnter for routes new to the chain (root-out).
        var oldDefinitions = fromState?.Matched
            .Select(m => m.Definition)
            .ToHashSet(ReferenceEqualityComparer.Instance) ?? [];

        foreach (var level in newMatched)
        {
            // Reused parent: same RouteDefinition reference instance — skip.
            if (oldDefinitions.Contains(level.Definition))
                continue;

            if (level.Definition.BeforeEnter is { } enterGuard)
            {
                ct.ThrowIfCancellationRequested();
                var result = await ExecuteGuard(
                    () => enterGuard(ctx, ct), onError);
                if (result is GuardPipelineResult.Cancel or GuardPipelineResult.Redirect)
                    return result;
            }
        }

        return new GuardPipelineResult.Continue();
    }

    private static async ValueTask<GuardPipelineResult> ExecuteGuard(
        Func<ValueTask<GuardResult>> invoke,
        Action<Exception>? onError)
    {
        try
        {
            var result = await invoke();
            return result switch
            {
                GuardResult.Cancel => new GuardPipelineResult.Cancel(),
                GuardResult.Redirect r => new GuardPipelineResult.Redirect(r.Target),
                _ => new GuardPipelineResult.Continue(),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            onError?.Invoke(ex);
            return new GuardPipelineResult.Cancel();
        }
    }
}
