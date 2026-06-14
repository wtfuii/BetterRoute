using BetterRoute.Routing;

namespace BetterRoute.Tests;

public class GuardRegistrarTests
{
    private sealed class TestGuard : IBeforeRouteLeave
    {
        public ValueTask<GuardResult> CanLeaveAsync(NavigationContext ctx, CancellationToken ct) =>
            ValueTask.FromResult(GuardResult.Ok);
    }

    [Fact]
    public void Register_then_get_returns_ordered_deepest_first()
    {
        var registrar = new GuardRegistrar();
        var a = new TestGuard();
        var b = new TestGuard();
        var c = new TestGuard();

        registrar.Register(b, depth: 1);
        registrar.Register(c, depth: 3);
        registrar.Register(a, depth: 2);

        var guards = registrar.GetLeaveGuards();
        Assert.Equal(3, guards.Count);
        // Deepest first: depth 3, then 2, then 1.
        Assert.Same(c, guards[0]);
        Assert.Same(a, guards[1]);
        Assert.Same(b, guards[2]);
    }

    [Fact]
    public void Unregister_removes_only_specified_component()
    {
        var registrar = new GuardRegistrar();
        var a = new TestGuard();
        var b = new TestGuard();

        registrar.Register(a, depth: 2);
        registrar.Register(b, depth: 1);
        registrar.Unregister(a);

        var guards = registrar.GetLeaveGuards();
        Assert.Single(guards);
        Assert.Same(b, guards[0]);
    }

    [Fact]
    public void GetLeaveGuards_on_empty_returns_empty()
    {
        var registrar = new GuardRegistrar();
        var guards = registrar.GetLeaveGuards();
        Assert.Empty(guards);
    }

    [Fact]
    public void Unregister_nonexistent_does_not_throw()
    {
        var registrar = new GuardRegistrar();
        var guard = new TestGuard();

        // Should not throw.
        registrar.Unregister(guard);

        var guards = registrar.GetLeaveGuards();
        Assert.Empty(guards);
    }

    [Fact]
    public void GetLeaveGuards_returns_snapshot_not_live_view()
    {
        var registrar = new GuardRegistrar();
        var a = new TestGuard();

        registrar.Register(a, depth: 1);
        var snapshot = registrar.GetLeaveGuards();

        // Mutate the returned list — should not affect the registrar.
        // (AsReadOnly prevents mutation at compile time; this test
        //  confirms the snapshot is independent.)
        registrar.Register(new TestGuard(), depth: 2);

        Assert.Single(snapshot); // Snapshot still has only the original entry.
    }

    [Fact]
    public void Multiple_registrations_at_same_depth_both_appear()
    {
        var registrar = new GuardRegistrar();
        var a = new TestGuard();
        var b = new TestGuard();

        registrar.Register(a, depth: 1);
        registrar.Register(b, depth: 1);

        var guards = registrar.GetLeaveGuards();
        Assert.Equal(2, guards.Count);
        Assert.Contains(a, guards);
        Assert.Contains(b, guards);
    }

    [Fact]
    public void Unregister_uses_reference_equality()
    {
        var registrar = new GuardRegistrar();
        var a1 = new TestGuard();
        var a2 = new TestGuard(); // Different instance, same type.

        registrar.Register(a1, depth: 1);
        registrar.Unregister(a2); // Should not remove a1 — different reference.

        var guards = registrar.GetLeaveGuards();
        Assert.Single(guards);
        Assert.Same(a1, guards[0]);
    }
}
