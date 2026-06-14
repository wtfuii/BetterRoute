# Named Outlets

## Motivation

A single `<RouterOutlet>` per parent works for the common case but breaks down for layouts that have multiple, independently-routable regions. Examples:

- A master/detail screen where the master list and the detail panel are both driven by the URL (`/users/42/posts` shows the user list *and* the user's posts panel simultaneously).
- An admin shell with a main content area and an always-present sidebar widget that switches based on context (`/dashboard` shows `Sidebar=NotificationsWidget`, `/users` shows `Sidebar=UserActionsWidget`).
- A modal-overlay outlet that renders alongside the main content (`/users/42(modal:settings)` — Angular-style auxiliary route).

Without named outlets, consumers fall back to manually toggling components based on `RouterState.Path` — which is exactly the flat-routing pain BetterRoute was designed to remove.

## Proposed API

Each `RouteDefinition` accepts a dictionary of components keyed by outlet name, in addition to the default component:

```csharp
public sealed record RouteDefinition(
    string Path,
    Type Component,                                                // default outlet
    IReadOnlyList<RouteDefinition>? Children = null,
    string? Name = null,
    IReadOnlyDictionary<string, Type>? Components = null);          // named outlets
```

```csharp
new RouteDefinition("users/:userId", typeof(UserLayout), Children:
[
    new RouteDefinition("posts",
        Component: typeof(PostsList),                              // → default outlet
        Components: new Dictionary<string, Type>
        {
            ["sidebar"] = typeof(PostFilters),                     // → <RouterOutlet Name="sidebar"/>
            ["modal"]   = typeof(NewPostModal),
        }),
]);
```

Consumer template:

```razor
<!-- UserLayout.razor -->
<div class="layout">
    <main><RouterOutlet /></main>
    <aside><RouterOutlet Name="sidebar" /></aside>
    <RouterOutlet Name="modal" />
</div>
```

`<RouterOutlet>` gains an optional `Name` parameter (default: `null`, meaning "default outlet").

## Behavior

1. **Matching is unchanged** — still produces a `MatchedRoute[]` chain. Each `MatchedRoute.Definition` already carries both the default and named components.
2. `MatchedRoute` extends to expose `IReadOnlyDictionary<string, Type> AllComponents` (combining `Component` under key `""` with `Components`).
3. `<RouterOutlet Name="...">` reads the cascading `RouterState`, looks up the component for its name at `Matched[CurrentDepth + 1]`, and renders it (or nothing if no entry).
4. **A named outlet renders without descending depth.** That is: the depth axis still belongs to the default outlet. Named outlets are siblings at the same depth, not nested. If you want a deeper named-outlet route, the route's `Children` are still navigated through the default outlet.

   This matches Vue's behavior and avoids a combinatorial mess where each named outlet has its own depth counter.

## Edge cases

- **No component for a named outlet on a given match** — render nothing. Don't error. Routes are allowed to opt into only some named outlets.
- **Default outlet missing** (`Component: null`, only `Components` set) — allowed as long as no consumer hits a path that expects a default render. At compile, just leave the default slot empty and let `<RouterOutlet />` render nothing. Useful when a route only contributes a sidebar.
- **Multiple `<RouterOutlet Name="x">` in the same level** — second one rendering the same component is surprising. Either disallow at runtime (warn + render the first) or allow. Vue allows. Probably allow with no warning — easier and the consumer asked for it.
- **Named outlets across route swaps** — when navigating from `/users/42/posts` to `/users/42/profile`, both the default content *and* the named outlets swap. The diff for which outlets to re-render is per-name, but cascading values handle it: every `RouterState` update re-renders all outlets reading it.
- **Cascading through named outlets** — components rendered into named outlets see the same `RouterState` as the default-outlet component at the same depth. They can read params and call `RouterOutlet` themselves to descend further. This is rare but should not be blocked.
- **URL syntax for ad-hoc named-outlet content** — Angular uses `(outlet:path)` syntax. We do *not* propose that. Named outlets are driven by route config only; the URL stays a clean path. This is a deliberate scope cut.

## Open questions

- **API shape — single dict vs. typed builders.** A `Dictionary<string, Type>` is stringly-typed. Compile-time outlet name checking would need source generators or attributes. Probably not worth it for v1 of named outlets — accept the stringly-typed cost.
- **Per-named-outlet `BeforeEnter` guards?** Vue allows per-named-component guards. Probably defer — the global/route guards (see [navigation-guards.md](./navigation-guards.md)) cover most needs.
- **Outlet keys: case sensitivity?** Suggest case-sensitive (`"sidebar"` ≠ `"Sidebar"`) to avoid silent collisions. Document.
- **Empty-string as default outlet name** — internal implementation detail; externally always `null`/absent means default. Make sure consumers can't accidentally collide with the empty string.
- **Animation/transition interaction** — when a named outlet's component swaps, does it animate independently of the default outlet? If [transitions](./transitions.md) ships, this is a real question. Probably yes — each outlet is its own animation root.
