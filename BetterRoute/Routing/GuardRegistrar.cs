namespace BetterRoute.Routing;

/// <summary>
/// Cascaded through the component tree by <c>BetterRouter</c>.
/// Components that implement <see cref="IBeforeRouteLeave"/> call
/// <see cref="Register"/> during <c>OnInitialized</c> and
/// <see cref="Unregister"/> during <c>Dispose</c>.
/// </summary>
public sealed class GuardRegistrar
{
    private readonly List<(int Depth, IBeforeRouteLeave Component)> _entries = [];

    /// <summary>Register a leave guard at the given tree depth.</summary>
    public void Register(IBeforeRouteLeave component, int depth) =>
        _entries.Add((depth, component));

    /// <summary>Remove a previously registered leave guard.</summary>
    public void Unregister(IBeforeRouteLeave component) =>
        _entries.RemoveAll(e => ReferenceEquals(e.Component, component));

    /// <summary>
    /// Returns a snapshot of all registered leave guards, sorted deepest-first.
    /// </summary>
    public IReadOnlyList<IBeforeRouteLeave> GetLeaveGuards()
    {
        if (_entries.Count == 0)
            return [];

        // Defensive snapshot: copy, sort deepest-first, project out the component.
        var snapshot = _entries.ToList();
        snapshot.Sort((a, b) => b.Depth.CompareTo(a.Depth));
        return snapshot.ConvertAll(e => e.Component).AsReadOnly();
    }
}
