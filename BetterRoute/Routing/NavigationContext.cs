namespace BetterRoute.Routing;

/// <summary>
/// Context passed to every <see cref="NavigationGuard"/> before a navigation completes.
/// </summary>
/// <param name="From">
/// The current <see cref="RouterState"/>, or <c>null</c> on the initial page load.
/// </param>
/// <param name="To">The resolved target state for the new URL.</param>
/// <param name="IsPopState">
/// <c>true</c> when the navigation was triggered by a browser back/forward action.
/// </param>
public sealed record NavigationContext(
    RouterState? From,
    RouterState To,
    bool IsPopState);
