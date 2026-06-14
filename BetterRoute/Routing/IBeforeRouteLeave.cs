namespace BetterRoute.Routing;

/// <summary>
/// Implemented by components that need to block or redirect navigation away from themselves.
/// Register via the cascading <see cref="GuardRegistrar"/> during <c>OnInitialized</c>
/// and unregister during <c>Dispose</c>.
/// </summary>
public interface IBeforeRouteLeave
{
    /// <summary>
    /// Called before the router leaves this component.
    /// Return <see cref="GuardResult.Ok"/> to allow, <see cref="GuardResult.Stop"/> to cancel,
    /// or <see cref="GuardResult.To"/> to redirect.
    /// </summary>
    ValueTask<GuardResult> CanLeaveAsync(NavigationContext ctx, CancellationToken ct);
}
