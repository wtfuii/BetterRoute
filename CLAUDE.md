# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build / Test / Run

```bash
# Build everything
dotnet build BetterRoute.sln

# Run all tests
dotnet test BetterRoute.Tests/BetterRoute.Tests.csproj

# Run a specific test class or filter
dotnet test BetterRoute.Tests/BetterRoute.Tests.csproj --filter "FullyQualifiedName~RouteMatcherTests"

# Run the sample Blazor WASM app (launches dev server)
dotnet run --project BetterRoute.Sample/BetterRoute.Sample.csproj
```

Requires .NET 10 SDK. The solution targets `net10.0` with package versions `10.0.*`.

## Architecture

BetterRoute is a **tree-based client-side routing library for Blazor WebAssembly** — an alternative to Blazor's built-in `Router`. Routes are defined declaratively as a nested tree of `RouteDefinition` records rather than a flat list of templates, enabling parent/child relationships, cascading state, named outlets, redirects, aliases, navigation guards, and programmatic navigation by route name.

### Core data flow

1. **Definition**: `RouteDefinition` records (`Path`, `Component`, optional `Children`, `Name`, `RedirectTo`, `RedirectToFactory`, `Aliases`, `Components`, `BeforeEnter`) form a tree in `App.razor`.
2. **Compilation**: `BetterRouter` compiles `RouteDefinition` trees into `CompiledRoute` trees (segment arrays + child references) and a `NamedRouteIndex` on parameter change. Aliases are expanded into synthetic `CompiledRoute` nodes sharing the same `Definition`. Compile-time validation enforces mutual exclusivity of redirects/components/aliases, param binding in redirect templates, and name uniqueness.
3. **Matching**: On navigation, `RouteMatcher.TryMatch(path, tree)` walks the compiled tree. Literal segments are case-insensitive; `:param` segments capture URL-decoded values. Literal siblings take precedence over parameter siblings. Returns `MatchResult.Success`, `StaticRedirect`, `DynamicRedirect`, or `NotFound`.
4. **Redirect resolution**: Static redirects (`RedirectTo`) substitute `:param` placeholders from captured parameters, resolve relative paths (`../`, `./`, bare segments), and preserve original query/fragment. Dynamic redirects (`RedirectToFactory`) build a provisional `RouterState` and call the factory delegate. Redirect hops are capped at 10 to prevent loops.
5. **Guard pipeline**: Before committing a match, the `GuardPipeline` runs three ordered phases:
   - **Phase 1 — Leave guards**: `IBeforeRouteLeave.CanLeaveAsync()` on currently-mounted components, deepest-first (skipped on initial navigation).
   - **Phase 2 — Global guard**: `BeforeEach` parameter on `BetterRouter` (if set).
   - **Phase 3 — Enter guards**: `BeforeEnter` on `RouteDefinition` nodes that are new to the matched chain (reused parents are skipped), root-out.
   - Results can be `Continue`, `Cancel` (restores previous URL), or `Redirect` (navigates to target). Exceptions from guards are caught and forwarded to `OnNavigationError`.
6. **State**: A successful match produces `IReadOnlyList<MatchedRoute>` — one entry per tree level. Parameters from all levels are merged into a single `RouterState.Parameters` dictionary (later levels override earlier). The `RouterState` also carries parsed query string values, the fragment, named-route index, and a navigate callback.
7. **Rendering**: `BetterRouter` cascades `RouterState` and `GuardRegistrar` and renders the matched leaf component. Layout nesting is achieved via `RouterOutlet` components placed in intermediate route components — it cascades state at the next depth so children render deeper in the tree. Named outlets (`RouterOutlet Name="sidebar"`) render named components from `RouteDefinition.Components`.

### Key classes

| Class | Role |
|---|---|
| `RouteDefinition` | Public sealed record — declarative route node (Path, Component, Children, Name, RedirectTo, RedirectToFactory, Aliases, Components, BeforeEnter) |
| `CompiledRoute` | Internal — preprocessed route tree for fast matching; handles validation and alias expansion |
| `NamedRouteIndex` | Internal — index of named routes for `ResolveUrl` / `NavigateTo`; built during compilation |
| `RouteMatcher.TryMatch` | Internal static — recursive tree matching with backtracking; returns `MatchResult` |
| `MatchResult` | Internal — discriminated union: `Success`, `StaticRedirect`, `DynamicRedirect`, `NotFound` |
| `RedirectTargetResolver` | Internal static — resolves redirect templates: param substitution, relative paths, query/fragment preservation |
| `QueryStringParser` | Public static — parses query strings into `IReadOnlyDictionary<string, IReadOnlyList<string>>` |
| `MatchedRoute` | Public sealed record — one level of a successful match; includes `AllComponents` (default + named) |
| `RouterState` | Public sealed record — cascaded to all components via `CascadingValue`; carries matched chain, params, query, fragment, named-route index, and `ResolveUrl`/`NavigateTo` methods |
| `BetterRouter` | Razor component — top-level router (receives `Routes`, matches URL, runs guard pipeline, cascades state) |
| `RouterOutlet` | Razor component — renders next child in matched chain; supports named outlets via `Name` parameter |
| `NavigationGuard` | Public delegate — `ValueTask<GuardResult> (NavigationContext, CancellationToken)` |
| `NavigationContext` | Public sealed record — `From` (previous state), `To` (target state), `IsPopState` (browser back/forward) |
| `GuardResult` | Public abstract record — `Continue`, `Cancel`, or `Redirect(string)`; with static `Ok`/`Stop`/`To()` factories |
| `GuardRegistrar` | Public sealed class — cascaded registrar for `IBeforeRouteLeave` components; manages registration/unregistration |
| `IBeforeRouteLeave` | Public interface — `CanLeaveAsync(NavigationContext, CancellationToken)` for component-level leave guards |
| `RouteLink` | Public Razor component — drop-in replacement for `<a>` that uses client-side navigation; supports path-based (`Href`) and named-route (`Name`+`Params`) modes |
| `GuardPipeline` | Internal static — executes leave→beforeEach→enter phases; handles cancellation, errors, and redirect propagation |

### Navigation guards

Navigation guards intercept route transitions. Three layers, executed in order:

1. **`IBeforeRouteLeave`** — Interface implemented by components. They register with `GuardRegistrar` (cascaded by `BetterRouter` and `RouterOutlet`) and are called deepest-first when the user navigates away.
2. **`BeforeEach`** — Global guard parameter on `BetterRouter`. Runs after all leave guards pass.
3. **`BeforeEnter`** — Per-route guard on `RouteDefinition` (init-only property). Runs on newly-entered route nodes, root-out. Parents reused from the previous navigation are skipped (detected via reference equality on `RouteDefinition`).

All guards return `GuardResult`: `Continue` (allow), `Cancel` (restore previous URL), or `Redirect(string)` (navigate elsewhere). Guard exceptions are caught and forwarded to `BetterRouter.OnNavigationError`.

### Named routes

`RouteDefinition.Name` (optional, dotted convention like `"user.post"`) enables programmatic navigation:

- `RouterState.ResolveUrl(name, parameters?)` — Resolves a named route to its full URL. Extra parameters become query string values.
- `RouterState.NavigateTo(name, parameters?, replace?)` — Navigates to a named route. When `parameters` is null, reuses current `Parameters`. Accepts both `IReadOnlyDictionary<string, string>` and anonymous-object overloads.

Names must be unique across the route tree (enforced at compile time).

### Named outlets

`RouteDefinition.Components` (optional `IReadOnlyDictionary<string, Type>`) allows a route to declare multiple named components. The `RouterOutlet` component accepts a `Name` parameter:

- `RouterOutlet` (no name) — renders the default `RouteDefinition.Component`.
- `RouterOutlet Name="sidebar"` — renders the named component from `Components["sidebar"]`.

Named outlets render as siblings (same `CurrentDepth`) rather than children. `MatchedRoute.AllComponents` merges the default component (keyed `""`) with named components.

### Redirects & aliases

- **`RedirectTo`** — Static redirect template (e.g. `"/users/:userId/profile"`). `:param` placeholders are substituted from captured parameters. Supports relative paths (`../`, `./`, bare segments). Redirects are mutual-exclusive with `Component` and `Aliases`.
- **`RedirectToFactory`** — Dynamic redirect factory `Func<RouterState, string?>`. Return null to signal not-found. Mutual-exclusive with `RedirectTo`, `Component`, and `Aliases`.
- **`Aliases`** — Alternative paths that render the same component without changing the URL. Expanded into synthetic `CompiledRoute` nodes during compilation.

Redirect hops are capped at 10 to prevent infinite loops.

### Query string & fragment

- `QueryStringParser.Parse(string?)` parses query strings into `IReadOnlyDictionary<string, IReadOnlyList<string>>` (multi-value, URL-decoded).
- `RouterState.Query` — parsed query string dictionary.
- `RouterState.GetQuery(key)` — first value for a key (or null).
- `RouterState.GetQueryValues(key)` — all values for a key.
- `RouterState.Fragment` — hash/fragment portion of the URL.
- Original query string and fragment are preserved during redirect resolution unless the target has its own.

### Design conventions

- **Immutability**: All data types are C# `record` types. `RouterState.AtDepth()` uses `this with { ... }` for modified copies.
- **Code-behind**: Razor components use `.razor` markup + `.razor.cs` code-behind partial classes.
- **`IsFixed="false"`**: Both `BetterRouter` and `RouterOutlet` cascade `RouterState` with `IsFixed="false"` — required for components to re-render on navigation.
- **Layout is routing**: There's no separate layout system. Nested layouts are intermediate routes whose component contains a `RouterOutlet`.
- **No DI registration**: The router is used directly as a Blazor component — no `AddBetterRoute()` extension method exists.
- **Empty-path `""`** in children acts as an index/default child route.
- **Namespace**: Public API is in `BetterRoute.Routing`; internal implementation is in `BetterRoute.Routing.Internal`. Tests use `InternalsVisibleTo` to access internal types.
- **`_Imports.razor`**: The library has a `_Imports.razor` with `@using BetterRoute.Routing` so Razor code-behind files can reference the namespace.
- **Compile-time validation**: `CompiledRoute.Compile()` validates route trees eagerly — duplicate names, unbound redirect params, mutual-exclusivity violations, and missing components all throw at compile time.
- **Redirect loop detection**: `BetterRouter` tracks a `_redirectCount` and refuses to follow more than 10 consecutive redirects.
- **URL restoration on cancel**: When a guard cancels navigation, the previous URL is restored via `Navigation.NavigateTo(previousUri)` with `_isRestoring` flag to prevent re-entry.

### Test project

Tests are in `BetterRoute.Tests/` using xunit. Coverage includes:

| File | Area |
|---|---|
| `RouteMatcherTests.cs` | Path matching, redirects, aliases, literal vs param priority, case-insensitivity, edge cases |
| `CompileValidationTests.cs` | Compile-time validation rules, alias expansion |
| `GuardPipelineTests.cs` | Three-phase ordering, leave/enter guard behavior, cancel/redirect/error handling |
| `GuardRegistrarTests.cs` | Registration, deepest-first sorting, snapshot isolation |
| `GuardResultTests.cs` | Type hierarchy and factory methods |
| `NamedRoutesTests.cs` | Name indexing, `ResolveUrl`, `NavigateTo`, parameter substitution, URL escaping |
| `NamedOutletsTests.cs` | Named components, `AllComponents`, outlet resolution |
| `ParseQueryTests.cs` | Query string parsing, URL decoding, multi-value support |
| `RedirectTargetResolverTests.cs` | Parameter substitution, relative path resolution, query/fragment preservation |

### Future features

`docs/future/` contains design sketches (not implemented): catch-all segments, lazy loading, transitions, scroll restoration. The design sketches for navigation guards, named routes, named outlets, redirects/aliases, and query parameters have been implemented — the actual implementation may differ from the sketches.
