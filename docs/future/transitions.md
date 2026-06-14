# Route Transitions / Animations

## Motivation

Today a route change is an instant DOM swap. For UI polish — and for the user to perceive *where* they are in a hierarchical navigation — animated transitions matter: a fade between sibling routes, a slide when descending into a child, a different curve when going back. Vue Router and React Router both have ecosystem solutions; in Blazor it's all rolled by hand.

This is more about *enabling* CSS-driven transitions than implementing them in C#. The router needs to keep the outgoing component alive long enough for an "exit" animation to play, then swap.

## Proposed API

A wrapper component, not a router parameter:

```razor
<RouterOutlet>
    <Transition Name="fade" Duration="200" />
</RouterOutlet>
```

Or, simpler and more Vue-ish, a sibling component that subscribes to the same cascading state:

```razor
<RouterTransition Name="fade" Duration="200">
    <RouterOutlet />
</RouterTransition>
```

`Name="fade"` corresponds to a set of CSS classes (`fade-enter`, `fade-enter-active`, `fade-exit`, `fade-exit-active`) the consumer defines. The router adds/removes them at the right moments.

For programmatic control:

```csharp
public sealed class TransitionContext
{
    public string FromUrl { get; init; }
    public string ToUrl { get; init; }
    public TransitionDirection Direction { get; init; }  // Forward / Back / Sibling / Replace
}
```

Consumers can opt for direction-aware animation:

```razor
<RouterTransition ResolveName="@(ctx => ctx.Direction == TransitionDirection.Back ? "slide-right" : "slide-left")">
    <RouterOutlet />
</RouterTransition>
```

## Behavior

1. **Wrap the outlet** — `RouterTransition` reads the cascading `RouterState`, computes the component for the current depth, but instead of swapping immediately when the component changes:
   - Renders the outgoing component with the `exit` and `exit-active` classes.
   - Schedules removal after `Duration`.
   - Renders the incoming component beneath/alongside with `enter` then `enter-active`.
2. **Direction detection** — compare the new `Matched` chain to the previous one:
   - Same depth, different leaf → `Sibling`
   - New chain is a superset of old (descending) → `Forward`
   - Old chain is a superset of new (ascending) → `Back`
   - Otherwise → `Replace`
3. **Mid-transition navigation** — if the user navigates again while a transition is in flight, cancel the current animation: jump to the final state of the in-flight transition, then immediately start the new one. Don't queue.
4. **Keep `RouterState` consistent** — the outgoing component sees the *old* `RouterState`, the incoming sees the *new*. Two cascading scopes overlap for the duration.

## Edge cases

- **Reused parents** — `/users/42/profile` → `/users/42/posts/7`: `UsersLayout` and `UserLayout` should not animate; only the leaf outlet they own should. The transition wrapper only triggers when the component *at its level* changes.
- **Concurrent transitions across depths** — if both a parent and a child component change in one navigation (e.g. switching from `/users/42/profile` to `/admin`), each `RouterTransition` in the chain runs its own animation. They may overlap visually; document this. Usually fine.
- **Component pre-disposal** — Blazor disposes the outgoing component as soon as it's removed from the render tree. To keep it alive for an exit animation, we must keep it rendered until the timer fires. Use a paired render fragment.
- **No CSS-class support** — for pure programmatic animation, expose `OnEnter` / `OnExit` callbacks that return a `Task` (router waits for the task before considering the transition done). Lets consumers drive `Web Animations API` via JS interop without forcing CSS classes.
- **Reduce-motion media query** — respect `prefers-reduced-motion`. Either skip the animation entirely or shorten to instant. Build it in by default, opt out via `RespectReducedMotion="false"`.
- **Server-side rendering** — transitions only make sense after hydration. On SSR / prerender, render directly without the wrapper logic.
- **Memory** — exit-animated components are still mounted. If a parent's component renders a large tree, dispose timing matters. Cap concurrent transitions per outlet to one.

## Open questions

- **Built-in CSS classes** — ship a small set (`fade`, `slide`, `none`)? Or stay BYO-CSS? Vue ships them in `<transition>`. Probably ship the class-name convention but no CSS — consumer brings styles.
- **Duration as ms vs. CSS-driven** — `Duration` is brittle (must match CSS `transition-duration`). Better: listen for `transitionend` on the root element. More accurate but assumes a single CSS transition root. Tradeoff. Vue uses `transitionend` by default with an optional `:duration` override.
- **Per-route transition opt-out** — `RouteDefinition.NoTransition = true`? Or a CSS class on the consumer side? Probably the latter — keep route config focused on routing.
- **Animating depth changes vs. sibling swaps** — these often want different transitions (descend = slide, sibling = fade). The proposed `ResolveName` callback handles it but is verbose. Consider a built-in shorthand. Defer.
- **Compatibility with [named outlets](./named-outlets.md) and [lazy components](./lazy-components.md)** — each named outlet animates independently (good). Lazy components animate the loading component → loaded component swap too. Make sure both interactions are sane.
