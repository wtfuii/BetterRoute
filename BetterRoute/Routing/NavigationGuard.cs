namespace BetterRoute.Routing;

/// <summary>
/// A guard that runs before a navigation completes.
/// Return <see cref="GuardResult.Ok"/> to approve,
/// <see cref="GuardResult.Stop"/> to cancel,
/// or <see cref="GuardResult.To"/> to redirect.
/// </summary>
public delegate ValueTask<GuardResult> NavigationGuard(NavigationContext ctx, CancellationToken ct);
