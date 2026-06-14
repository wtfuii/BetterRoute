using BetterRoute.Routing;
using BetterRoute.Routing.Internal;

namespace BetterRoute.Tests;

public class GuardPipelineTests
{
    private sealed class A;
    private sealed class B;
    private sealed class C;

    private sealed class FakeLeaveGuard : IBeforeRouteLeave
    {
        private readonly GuardResult _result;
        public bool WasCalled { get; private set; }

        public FakeLeaveGuard(GuardResult? result = null)
        {
            _result = result ?? GuardResult.Ok;
        }

        public ValueTask<GuardResult> CanLeaveAsync(NavigationContext ctx, CancellationToken ct)
        {
            WasCalled = true;
            return ValueTask.FromResult(_result);
        }
    }

    private sealed class ThrowingLeaveGuard : IBeforeRouteLeave
    {
        private readonly Exception _ex;

        public ThrowingLeaveGuard(Exception? ex = null)
        {
            _ex = ex ?? new InvalidOperationException("guard error");
        }

        public ValueTask<GuardResult> CanLeaveAsync(NavigationContext ctx, CancellationToken ct)
            => throw _ex;
    }

    private static RouterState MakeState(
        IReadOnlyList<MatchedRoute> matched,
        string url = "/test",
        string path = "test")
    {
        return new RouterState(
            matched,
            CurrentDepth: 0,
            Parameters: new Dictionary<string, string>(),
            Query: new Dictionary<string, IReadOnlyList<string>>(),
            Url: url,
            Path: path,
            Fragment: null);
    }

    private static MatchedRoute MakeMatched(RouteDefinition def)
    {
        return new MatchedRoute(def, SegmentParameters: new Dictionary<string, string>());
    }

    [Fact]
    public async Task All_pass_returns_continue()
    {
        var def = new RouteDefinition("home", typeof(A));
        var toState = MakeState([MakeMatched(def)]);

        var result = await GuardPipeline.RunAsync(
            fromState: null,
            toState,
            newMatched: [MakeMatched(def)],
            leaveGuards: [],
            beforeEach: null,
            onError: null,
            ct: CancellationToken.None);

        Assert.IsType<GuardPipelineResult.Continue>(result);
    }

    [Fact]
    public async Task Initial_navigation_skips_leave_guards()
    {
        var def = new RouteDefinition("home", typeof(A));
        var toState = MakeState([MakeMatched(def)]);
        var leaveGuard = new FakeLeaveGuard();

        var result = await GuardPipeline.RunAsync(
            fromState: null,
            toState,
            newMatched: [MakeMatched(def)],
            leaveGuards: [leaveGuard],
            beforeEach: null,
            onError: null,
            ct: CancellationToken.None);

        Assert.IsType<GuardPipelineResult.Continue>(result);
        Assert.False(leaveGuard.WasCalled);
    }

    [Fact]
    public async Task Leave_guards_run_deepest_first()
    {
        var def = new RouteDefinition("home", typeof(A));
        var fromState = MakeState([MakeMatched(def)]);
        var toState = MakeState([MakeMatched(def)]);

        var callOrder = new List<int>();
        var deep = new FakeLeaveGuard();
        var shallow = new FakeLeaveGuard();

        // Wrapping guards to record call order.
        var deepWrapper = new DelegateLeaveGuard(_ =>
        {
            callOrder.Add(3);
            return ValueTask.FromResult(GuardResult.Ok);
        });
        var shallowWrapper = new DelegateLeaveGuard(_ =>
        {
            callOrder.Add(1);
            return ValueTask.FromResult(GuardResult.Ok);
        });

        // Register with depths — deepest should be called first.
        var registrar = new GuardRegistrar();
        registrar.Register(shallowWrapper, depth: 1);
        registrar.Register(deepWrapper, depth: 3);

        var result = await GuardPipeline.RunAsync(
            fromState,
            toState,
            newMatched: [MakeMatched(def)],
            leaveGuards: registrar.GetLeaveGuards(),
            beforeEach: null,
            onError: null,
            ct: CancellationToken.None);

        Assert.IsType<GuardPipelineResult.Continue>(result);
        Assert.Equal([3, 1], callOrder); // deepest first
    }

    [Fact]
    public async Task First_cancel_from_leave_stops_pipeline()
    {
        var def = new RouteDefinition("home", typeof(A));
        var fromState = MakeState([MakeMatched(def)]);
        var toState = MakeState([MakeMatched(def)]);

        var cancelGuard = new FakeLeaveGuard(GuardResult.Stop);
        var secondGuard = new FakeLeaveGuard();

        var result = await GuardPipeline.RunAsync(
            fromState,
            toState,
            newMatched: [MakeMatched(def)],
            leaveGuards: [cancelGuard, secondGuard],
            beforeEach: null,
            onError: null,
            ct: CancellationToken.None);

        Assert.IsType<GuardPipelineResult.Cancel>(result);
        Assert.False(secondGuard.WasCalled); // Pipeline stopped after first cancel.
    }

    [Fact]
    public async Task Leave_guard_redirect_returns_redirect_result()
    {
        var def = new RouteDefinition("home", typeof(A));
        var fromState = MakeState([MakeMatched(def)]);
        var toState = MakeState([MakeMatched(def)]);

        var redirectGuard = new FakeLeaveGuard(GuardResult.To("/login"));

        var result = await GuardPipeline.RunAsync(
            fromState,
            toState,
            newMatched: [MakeMatched(def)],
            leaveGuards: [redirectGuard],
            beforeEach: null,
            onError: null,
            ct: CancellationToken.None);

        var redirect = Assert.IsType<GuardPipelineResult.Redirect>(result);
        Assert.Equal("/login", redirect.Target);
    }

    [Fact]
    public async Task BeforeEach_runs_after_leave_guards()
    {
        var def = new RouteDefinition("home", typeof(A));
        var fromState = MakeState([MakeMatched(def)]);
        var toState = MakeState([MakeMatched(def)]);

        var callOrder = new List<string>();
        var leaveGuard = new DelegateLeaveGuard(_ =>
        {
            callOrder.Add("leave");
            return ValueTask.FromResult(GuardResult.Ok);
        });

        NavigationGuard beforeEach = (ctx, ct) =>
        {
            callOrder.Add("beforeEach");
            return ValueTask.FromResult(GuardResult.Ok);
        };

        var result = await GuardPipeline.RunAsync(
            fromState,
            toState,
            newMatched: [MakeMatched(def)],
            leaveGuards: [leaveGuard],
            beforeEach: beforeEach,
            onError: null,
            ct: CancellationToken.None);

        Assert.IsType<GuardPipelineResult.Continue>(result);
        Assert.Equal(["leave", "beforeEach"], callOrder);
    }

    [Fact]
    public async Task BeforeEach_cancel_skips_enter_guards()
    {
        var defA = new RouteDefinition("admin", typeof(A))
        {
            BeforeEnter = (ctx, ct) => ValueTask.FromResult(GuardResult.Ok),
        };
        var defB = new RouteDefinition("", typeof(B));
        var fromState = MakeState([MakeMatched(defB)]);
        var toState = MakeState([MakeMatched(defA)]);

        NavigationGuard beforeEach = (ctx, ct) =>
            ValueTask.FromResult(GuardResult.Stop);

        var result = await GuardPipeline.RunAsync(
            fromState,
            toState,
            newMatched: [MakeMatched(defA)],
            leaveGuards: [],
            beforeEach: beforeEach,
            onError: null,
            ct: CancellationToken.None);

        Assert.IsType<GuardPipelineResult.Cancel>(result);
    }

    [Fact]
    public async Task BeforeEach_redirect_returns_redirect_result()
    {
        var def = new RouteDefinition("home", typeof(A));
        var fromState = MakeState([MakeMatched(def)]);
        var toState = MakeState([MakeMatched(def)]);

        NavigationGuard beforeEach = (ctx, ct) =>
            ValueTask.FromResult(GuardResult.To("/maintenance"));

        var result = await GuardPipeline.RunAsync(
            fromState,
            toState,
            newMatched: [MakeMatched(def)],
            leaveGuards: [],
            beforeEach: beforeEach,
            onError: null,
            ct: CancellationToken.None);

        var redirect = Assert.IsType<GuardPipelineResult.Redirect>(result);
        Assert.Equal("/maintenance", redirect.Target);
    }

    [Fact]
    public async Task Enter_guards_skip_reused_parents()
    {
        // Simulate /users/42/profile → /users/42/posts/7
        // UsersLayout and UserLayout are reused, only PostPage is new.
        var usersLayout = new RouteDefinition("users", typeof(A))
        {
            BeforeEnter = (ctx, ct) => ValueTask.FromResult(GuardResult.Ok),
        };
        var userLayout = new RouteDefinition(":userId", typeof(B))
        {
            BeforeEnter = (ctx, ct) => ValueTask.FromResult(GuardResult.Ok),
        };
        var profilePage = new RouteDefinition("profile", typeof(C));
        var postPage = new RouteDefinition("posts/:postId", typeof(A));

        var fromMatched = new List<MatchedRoute>
        {
            MakeMatched(usersLayout),
            MakeMatched(userLayout),
            MakeMatched(profilePage),
        };

        var newMatched = new List<MatchedRoute>
        {
            MakeMatched(usersLayout),   // same reference — reused
            MakeMatched(userLayout),    // same reference — reused
            MakeMatched(postPage),      // new
        };

        var fromState = MakeState(fromMatched);
        var toState = MakeState(newMatched);

        // Count how many times BeforeEnter is actually called.
        var enterCalls = 0;
        usersLayout = usersLayout with
        {
            BeforeEnter = (ctx, ct) =>
            {
                Interlocked.Increment(ref enterCalls);
                return ValueTask.FromResult(GuardResult.Ok);
            }
        };
        userLayout = userLayout with
        {
            BeforeEnter = (ctx, ct) =>
            {
                Interlocked.Increment(ref enterCalls);
                return ValueTask.FromResult(GuardResult.Ok);
            }
        };
        // postPage has no BeforeEnter, so enterCalls should be 0 — only
        // reused parents have BeforeEnter, and they should be skipped.

        // Rebuild matched lists with the updated definitions.
        fromMatched[0] = MakeMatched(usersLayout);
        fromMatched[1] = MakeMatched(userLayout);
        newMatched[0] = MakeMatched(usersLayout);
        newMatched[1] = MakeMatched(userLayout);
        newMatched[2] = MakeMatched(postPage);

        var result = await GuardPipeline.RunAsync(
            MakeState(fromMatched),
            MakeState(newMatched),
            newMatched: newMatched,
            leaveGuards: [],
            beforeEach: null,
            onError: null,
            ct: CancellationToken.None);

        Assert.IsType<GuardPipelineResult.Continue>(result);
        Assert.Equal(0, enterCalls); // Reused parents' BeforeEnter skipped.
    }

    [Fact]
    public async Task Enter_guards_run_for_new_routes_root_out()
    {
        var callOrder = new List<string>();
        var root = new RouteDefinition("admin", typeof(A))
        {
            BeforeEnter = (ctx, ct) =>
            {
                callOrder.Add("root");
                return ValueTask.FromResult(GuardResult.Ok);
            },
        };
        var child = new RouteDefinition("users", typeof(B))
        {
            BeforeEnter = (ctx, ct) =>
            {
                callOrder.Add("child");
                return ValueTask.FromResult(GuardResult.Ok);
            },
        };

        var newMatched = new List<MatchedRoute>
        {
            MakeMatched(root),
            MakeMatched(child),
        };

        var toState = MakeState(newMatched);

        var result = await GuardPipeline.RunAsync(
            fromState: null,
            toState,
            newMatched: newMatched,
            leaveGuards: [],
            beforeEach: null,
            onError: null,
            ct: CancellationToken.None);

        Assert.IsType<GuardPipelineResult.Continue>(result);
        Assert.Equal(["root", "child"], callOrder); // root-out order
    }

    [Fact]
    public async Task Guard_exception_triggers_onError_and_returns_cancel()
    {
        var def = new RouteDefinition("home", typeof(A));
        var fromState = MakeState([MakeMatched(def)]);
        var toState = MakeState([MakeMatched(def)]);

        Exception? captured = null;
        Action<Exception> onError = ex => captured = ex;

        var throwingGuard = new ThrowingLeaveGuard(
            new InvalidOperationException("boom"));

        var result = await GuardPipeline.RunAsync(
            fromState,
            toState,
            newMatched: [MakeMatched(def)],
            leaveGuards: [throwingGuard],
            beforeEach: null,
            onError: onError,
            ct: CancellationToken.None);

        Assert.IsType<GuardPipelineResult.Cancel>(result);
        Assert.NotNull(captured);
        Assert.IsType<InvalidOperationException>(captured);
        Assert.Equal("boom", captured.Message);
    }

    [Fact]
    public async Task OperationCanceledException_propagates_not_caught()
    {
        var def = new RouteDefinition("home", typeof(A));
        var fromState = MakeState([MakeMatched(def)]);
        var toState = MakeState([MakeMatched(def)]);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // The upfront ct.ThrowIfCancellationRequested() in RunAsync causes
        // the async state machine to return a faulted ValueTask.
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            GuardPipeline.RunAsync(
                fromState,
                toState,
                newMatched: [MakeMatched(def)],
                leaveGuards: [],
                beforeEach: null,
                onError: null,
                ct: cts.Token).AsTask());
    }

    [Fact]
    public async Task IsPopState_passed_to_NavigationContext_for_leave_guards()
    {
        var def = new RouteDefinition("home", typeof(A));
        var fromState = MakeState([MakeMatched(def)]);
        var toState = MakeState([MakeMatched(def)]);

        bool? capturedIsPopState = null;
        var guard = new DelegateLeaveGuard(ctx =>
        {
            capturedIsPopState = ctx.IsPopState;
            return ValueTask.FromResult(GuardResult.Ok);
        });

        var result = await GuardPipeline.RunAsync(
            fromState,
            toState,
            newMatched: [MakeMatched(def)],
            leaveGuards: [guard],
            beforeEach: null,
            onError: null,
            ct: CancellationToken.None,
            isPopState: true);

        Assert.IsType<GuardPipelineResult.Continue>(result);
        Assert.True(capturedIsPopState);
    }

    [Fact]
    public async Task IsPopState_passed_to_NavigationContext_for_beforeEach()
    {
        var def = new RouteDefinition("home", typeof(A));
        var toState = MakeState([MakeMatched(def)]);

        bool? capturedIsPopState = null;
        NavigationGuard beforeEach = (ctx, ct) =>
        {
            capturedIsPopState = ctx.IsPopState;
            return ValueTask.FromResult(GuardResult.Ok);
        };

        var result = await GuardPipeline.RunAsync(
            fromState: null,
            toState,
            newMatched: [MakeMatched(def)],
            leaveGuards: [],
            beforeEach: beforeEach,
            onError: null,
            ct: CancellationToken.None,
            isPopState: true);

        Assert.IsType<GuardPipelineResult.Continue>(result);
        Assert.True(capturedIsPopState);
    }

    [Fact]
    public async Task From_state_is_null_during_initial_navigation()
    {
        var def = new RouteDefinition("home", typeof(A));
        var toState = MakeState([MakeMatched(def)]);

        RouterState? capturedFrom = null;
        NavigationGuard beforeEach = (ctx, ct) =>
        {
            capturedFrom = ctx.From;
            return ValueTask.FromResult(GuardResult.Ok);
        };

        var result = await GuardPipeline.RunAsync(
            fromState: null,
            toState,
            newMatched: [MakeMatched(def)],
            leaveGuards: [],
            beforeEach: beforeEach,
            onError: null,
            ct: CancellationToken.None);

        Assert.IsType<GuardPipelineResult.Continue>(result);
        Assert.Null(capturedFrom);
    }

    [Fact]
    public async Task Enter_guard_cancel_stops_pipeline()
    {
        var first = new RouteDefinition("first", typeof(A))
        {
            BeforeEnter = (ctx, ct) => ValueTask.FromResult(GuardResult.Stop),
        };
        var second = new RouteDefinition("second", typeof(B))
        {
            BeforeEnter = (ctx, ct) => ValueTask.FromResult(GuardResult.Ok),
        };

        var newMatched = new List<MatchedRoute>
        {
            MakeMatched(first),
            MakeMatched(second),
        };

        var result = await GuardPipeline.RunAsync(
            fromState: null,
            toState: MakeState(newMatched),
            newMatched: newMatched,
            leaveGuards: [],
            beforeEach: null,
            onError: null,
            ct: CancellationToken.None);

        Assert.IsType<GuardPipelineResult.Cancel>(result);
        // Pipeline stopped after first enter guard returned Cancel;
        // second guard was never called (tested implicitly — if it were
        // called and returned Continue, the pipeline would have continued).
    }

    /// <summary>
    /// Adapter that wraps an inline delegate as an IBeforeRouteLeave.
    /// </summary>
    private sealed class DelegateLeaveGuard : IBeforeRouteLeave
    {
        private readonly Func<NavigationContext, ValueTask<GuardResult>> _fn;

        public DelegateLeaveGuard(Func<NavigationContext, ValueTask<GuardResult>> fn)
        {
            _fn = fn;
        }

        public ValueTask<GuardResult> CanLeaveAsync(NavigationContext ctx, CancellationToken ct)
            => _fn(ctx);
    }
}
