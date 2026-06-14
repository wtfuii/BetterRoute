namespace BetterRoute.Routing;

/// <summary>
/// Result returned by a <see cref="NavigationGuard"/>.
/// Use <see cref="Ok"/> to approve, <see cref="Stop"/> to cancel,
/// or <see cref="To"/> to redirect.
/// </summary>
public abstract record GuardResult
{
    /// <summary>Approve the navigation. Alias for <c>new Continue()</c>.</summary>
    public static GuardResult Ok { get; } = new Continue();

    /// <summary>Cancel the navigation. Alias for <c>new Cancel()</c>.</summary>
    public static GuardResult Stop { get; } = new Cancel();

    /// <summary>Redirect to <paramref name="target"/> instead. Alias for <c>new Redirect(target)</c>.</summary>
    public static GuardResult To(string target) => new Redirect(target);

    /// <summary>The navigation is approved.</summary>
    public sealed record Continue : GuardResult;

    /// <summary>The navigation is cancelled.</summary>
    public sealed record Cancel : GuardResult;

    /// <summary>Redirect to <paramref name="Target"/> instead of completing this navigation.</summary>
    public sealed record Redirect(string Target) : GuardResult;

    private GuardResult() { }
}
