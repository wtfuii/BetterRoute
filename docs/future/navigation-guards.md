# Navigation Guards

## Motivation

Auth, dirty-form protection, role checks, lazy data preload — all need a hook *between* "user clicked a link" and "the new route renders". In Vue Router this is `beforeEach` / `beforeEnter` / `beforeRouteLeave`. Without it, every page component repeats the same "redirect if not logged in" boilerplate, and "are you sure you want to leave?" can't really be expressed at all.

## Proposed API

Two kinds of guards: **global**, registered on the router, and **per-route**, attached to a `RouteDefinition`. Both are async and can either:

- approve navigation (return `GuardResult.Continue`),
- cancel it (return `GuardResult.Cancel`),
- redirect (return `GuardResult.Redirect("/login")`).

```csharp
public abstract record GuardResult
{
    public sealed record Continue : GuardResult;
    public sealed record Cancel : GuardResult;
    public sealed record Redirect(string Target) : GuardResult;

    public static GuardResult Ok { get; } = new Continue();
    public static GuardResult Stop { get; } = new Cancel();
    public static GuardResult To(string target) => new Redirect(target);
}

public delegate ValueTask<GuardResult> NavigationGuard(NavigationContext ctx, CancellationToken ct);

public sealed record NavigationContext(
    RouterState From,        // null on initial navigation
    RouterState To,
    bool IsPopState);        // browser back/forward
```

Attached:

```csharp
new RouteDefinition("admin", typeof(AdminLayout), Children: [...])
{
    BeforeEnter = ctx => session.IsAdmin ? GuardResult.Ok : GuardResult.To("/login"),
};
```

```razor
<BetterRouter Routes="@Routes" BeforeEach="@CheckAuth" NotFound="typeof(NotFound)" />

@code {
    private async ValueTask<GuardResult> CheckAuth(NavigationContext ctx, CancellationToken ct)
    {
        if (ctx.To.Path.StartsWith("/admin") && !await Auth.IsAdminAsync(ct))
            return GuardResult.To("/login?return=" + Uri.EscapeDataString(ctx.To.Path));
        return GuardResult.Ok;
    }
}
```

Component-side `BeforeRouteLeave` is more invasive — the component instance owns the decision, not the route config. Expose it via an interface:

```csharp
public interface IBeforeRouteLeave
{
    ValueTask<GuardResult> CanLeaveAsync(NavigationContext ctx, CancellationToken ct);
}
```

`BetterRouter` discovers it by inspecting the *current* matched components before swapping.

## Behavior

On every navigation:

1. **Resolve match** for the target URL. If no match, fall through to `NotFound` without running guards.
2. **Run `BeforeRouteLeave`** on each currently-mounted component (deepest-first) that implements `IBeforeRouteLeave`. First non-`Continue` result wins.
3. **Run global `BeforeEach`** (if set).
4. **Run per-route `BeforeEnter`** for each route in the *new* chain that wasn't in the old chain (root-out). Reused parents don't re-run their `BeforeEnter`.
5. On `Continue` from all guards, swap `RouterState` and render.
6. On `Cancel`, **restore the URL** to `From.Url` (this matters for back/forward — see edge cases) and do nothing else.
7. On `Redirect`, treat the redirect target as a new navigation. Restart from step 1 with the new URL. Cap recursion at a sane limit (e.g. 10) to prevent loops.

Guards are awaited sequentially within a phase. Concurrency between phases is intentionally not exposed — the contract is "if my `BeforeEach` returned `Continue`, no later guard can have seen my mutation already". Keep it simple.

## Edge cases

- **Initial navigation** — `From` is `null`. `BeforeRouteLeave` is skipped. Global `BeforeEach` runs with `ctx.From == null`. Document this clearly; otherwise consumers add `if (ctx.From == null) return Ok;` defensively.
- **Browser back/forward + Cancel** — the URL has already changed in the address bar by the time `LocationChanged` fires. We must call `NavigationManager.NavigateTo(From.Url, replace: true)` to rewind. This produces a synthetic forward navigation we must recognize and not re-guard (infinite loop). Use a re-entry flag.
- **Redirect chain limit** — 10 hops, then throw. Better than silent infinite loop.
- **Async guard cancelled by faster navigation** — if the user clicks a different link while a guard is still awaiting, cancel the in-flight `CancellationToken` and start the new navigation. Avoid the "old guard finishes, overwrites new state" race.
- **`BeforeRouteLeave` on a component that's been disposed** — only run guards on components currently in `Matched`. A component dropped earlier in the same navigation chain has no say.
- **Guard throws** — treat as `Cancel` and surface via an `OnNavigationError` callback on `BetterRouter`. Don't crash the app.
- **Reused parents** — `/users/42/profile` → `/users/42/posts/7`: `UsersLayout` and `UserLayout` stay mounted. They should *not* see `BeforeRouteLeave`, and `BeforeEnter` should not re-run for those route nodes. Diff old and new `Matched` chains by reference equality of `RouteDefinition`.

## Open questions

- **Where do guards live in the API surface?** Adding `BeforeEnter` to `RouteDefinition` breaks the "record is just config" simplicity. Alternative: a separate `RouteOptions` dictionary. Tradeoff is discoverability vs. surface area.
- **Sync vs. async-only API?** `ValueTask<GuardResult>` covers both but adds a tiny overhead for sync guards. Probably worth it for the unified API.
- **Per-route `BeforeLeave`?** Not in the proposal — leaving guards live on the component instance. Vue has both. Decide whether the duplication is worth it.
- **Should `Redirect` be allowed from `BeforeRouteLeave`?** Semantically odd — "I'm leaving but route me elsewhere". Probably yes for symmetry. Vue allows it.
- **Logging hook** for observability? An `INavigationListener` could subscribe to `start / cancel / redirect / commit` without participating in the decision. Useful for telemetry. Separate concern.
