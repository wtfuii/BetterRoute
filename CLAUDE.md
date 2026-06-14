# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build / Test / Run

```bash
# Build everything
dotnet build BetterRoute.sln

# Run tests
dotnet test BetterRoute.Tests/BetterRoute.Tests.csproj

# Run the sample Blazor WASM app (launches dev server)
dotnet run --project BetterRoute.Sample/BetterRoute.Sample.csproj
```

Requires .NET 10 SDK. The solution targets `net10.0` with package versions `10.0.*`.

## Architecture

BetterRoute is a **tree-based client-side routing library for Blazor WebAssembly** — an alternative to Blazor's built-in `Router`. Routes are defined declaratively as a nested tree of `RouteDefinition` records rather than a flat list of templates, enabling parent/child relationships and cascading state.

### Core data flow

1. **Definition**: `RouteDefinition` records (`Path`, `Component`, optional `Children`) form a tree in `App.razor`.
2. **Compilation**: `BetterRouter` compiles `RouteDefinition` trees into `CompiledRoute` trees (segment arrays + child references) on parameter change.
3. **Matching**: On navigation, `RouteMatcher.TryMatch(path, tree)` walks the compiled tree. Literal segments are case-insensitive; `:param` segments capture URL-decoded values. Literal siblings take precedence over parameter siblings.
4. **State**: A successful match produces `IReadOnlyList<MatchedRoute>` — one entry per tree level. Parameters from all levels are merged into a single `RouterState.Parameters` dictionary (later levels override earlier).
5. **Rendering**: `BetterRouter` cascades `RouterState` and renders the matched leaf component. Layout nesting is achieved via `RouterOutlet` components placed in intermediate route components — it cascades state at the next depth so children render deeper in the tree.

### Key classes

| Class | Role |
|---|---|
| `RouteDefinition` | Public sealed record — declarative route node |
| `CompiledRoute` | Internal — preprocessed route for fast matching |
| `RouteMatcher.TryMatch` | Internal — recursive tree matching with backtracking |
| `MatchedRoute` | Public sealed record — one level of a successful match |
| `RouterState` | Public sealed record — cascaded to all components via `CascadingValue` |
| `BetterRouter` | Razor component — top-level router (receives `Routes`, matches URL, cascades state) |
| `RouterOutlet` | Razor component — renders next child in matched chain |
| `QueryStringParser` | Public static utility for parsing query strings |

### Design conventions

- **Immutability**: All data types are C# `record` types. `RouterState.AtDepth()` uses `this with { ... }` for modified copies.
- **Code-behind**: Razor components use `.razor` markup + `.razor.cs` code-behind partial classes.
- **`IsFixed="false"`**: Both `BetterRouter` and `RouterOutlet` cascade `RouterState` with `IsFixed="false"` — required for components to re-render on navigation.
- **Layout is routing**: There's no separate layout system. Nested layouts are intermediate routes whose component contains a `RouterOutlet`.
- **No DI registration**: The router is used directly as a Blazor component — no `AddBetterRoute()` extension method exists.
- **Empty-path `""`** in children acts as an index/default child route.

### Future features

`docs/future/` contains design sketches (not implemented): navigation guards, named routes, lazy loading, transitions, redirects, scroll restoration, catch-all segments, named outlets. The `RouteDefinition.Name` field is reserved for named-route resolution.
