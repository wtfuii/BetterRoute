# Lazy / Async-Loaded Components

## Motivation

Bundle size matters in Blazor WebAssembly: the initial download blocks the first render. A large admin section, a charting library, or a route used by 1% of users shouldn't all ship in the first payload. Vue Router solves this with `() => import('./AdminPanel.vue')` — the component is fetched only when the route is first hit.

Blazor's existing `LazyAssemblyLoader` enables on-demand DLL loading, but there's no routing integration — you have to write the orchestration yourself, and during the load the user sees nothing (no spinner) or sees a flash of the wrong page.

This feature wires lazy loading into the router so a route declaration can point at an *assembly* to load on demand, and the router orchestrates: show a placeholder, await the load, then render.

## Proposed API

Add a loader variant of `RouteDefinition` alongside the eager one:

```csharp
public sealed record RouteDefinition(
    string Path,
    Type? Component = null,
    IReadOnlyList<RouteDefinition>? Children = null,
    string? Name = null,
    Func<Task<Type>>? ComponentLoader = null,
    Type? LoadingComponent = null);
```

Exactly one of `Component` and `ComponentLoader` must be set. `LoadingComponent` is rendered while the loader is awaiting.

```csharp
new RouteDefinition("admin", typeof(AdminLayout), Children:
[
    new RouteDefinition("reports",
        ComponentLoader: async () =>
        {
            await LazyAssemblyLoader.LoadAssembliesAsync(["MyApp.Admin.Reports.dll"]);
            return typeof(MyApp.Admin.Reports.ReportsHome);
        },
        LoadingComponent: typeof(SpinnerComponent)),
]);
```

A small helper to remove the boilerplate:

```csharp
LazyRoute.From<MyApp.Admin.Reports.ReportsHome>("MyApp.Admin.Reports");
// returns a Func<Task<Type>> that calls LazyAssemblyLoader and returns the type
```

This needs the assembly name as a string at declaration time because the type isn't loaded yet — `typeof(ReportsHome)` would force the assembly to load eagerly. The compromise: declare with a string, use a code analyzer or build-time check to verify the type/assembly exists.

## Behavior

1. **Compile** — at tree compile time, mark routes with a loader as "lazy". No component is resolved yet.
2. **On match** — if the matched chain includes a lazy route whose component hasn't been loaded yet, `BetterRouter` enters a *loading state*:
   - Cascades a `RouterState` whose `Matched` ends at the closest already-loaded ancestor.
   - Renders the `LoadingComponent` (or nothing if not set) into that ancestor's outlet.
   - Awaits the loader.
3. **On loader completion** — store the resolved `Type` on the `CompiledRoute` (now eager forever). Re-match the same URL, cascade the now-complete `RouterState`, render normally.
4. **On loader failure** — surface via `OnNavigationError`. The route is *not* marked as loaded; a retry can succeed.
5. **Concurrent navigations** — if the user clicks two lazy routes quickly, the second navigation may complete first (smaller assembly). Track per-navigation cancellation tokens. Don't render a stale loaded component on top of the current navigation.
6. **Caching** — once loaded, the resolved type stays in memory. Don't unload. (Blazor WASM doesn't really support assembly unload anyway.)

## Edge cases

- **Lazy route with eager children** — fine; children only compile their loaders when reached.
- **Lazy parent + eager grandchild** — when navigating directly to `/admin/reports/123`, we have to await the parent's loader (`AdminLayout`) before we can render anything for the admin shell. The eager grandchild has nothing to render into until then.
- **Loading component sees `RouterState`** — yes, but `RouterState.Matched` will be truncated to the loaded prefix. Document this so consumers don't expect `Parameters["reportId"]` to be populated while the route's component is still loading. Provide an `IsLoading` flag on `RouterState`.
- **Prefetching on hover** — useful but a separate concern. Expose a public `BetterRouter.PrefetchAsync(string url)` that walks the would-be chain and triggers loaders, returns a Task. Not required for v1 of this feature; can ship later without breaking the API.
- **Pre-rendered server-side render** — if the lazy route is hit during SSR / prerender, the loader is forced to await before the SSR can complete. `LazyAssemblyLoader` is WASM-only. For server-rendered Blazor, lazy-by-assembly doesn't apply — the type just exists in `AppDomain`. Document that this feature is WASM-specific.
- **Multiple lazy levels in one match** — `/admin/reports/123` where both `admin` and `reports` are lazy. Load them in parallel? Or sequentially because the child loader depends on the parent's assembly? Probably parallel — assembly load order is independent. Use `Task.WhenAll`.
- **Loader returns `null` or wrong type** — guard at runtime, surface via error.

## Open questions

- **API ergonomics** — `Func<Task<Type>>` is awkward. Source generator that emits the loader given a type name + assembly? Maybe. Defer.
- **Per-route vs. per-app loading UI** — proposed API allows per-route `LoadingComponent`. Also worth a global default on `BetterRouter`? Probably yes — most apps want one spinner.
- **Distinguishing "loading" from "errored"** — `RouterState.LoadStatus { NotLoading, Loading, Errored }`? Or expose via separate cascading value? Decide before shipping.
- **Code-splitting other-than-assemblies** — Blazor only really splits at the assembly level. If a future version supports finer-grained splits, this API should still work.
- **Suspense boundaries** — Vue 3 / React patterns. Probably too much for this library. Stay focused.
