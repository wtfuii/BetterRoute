# BetterRoute

<p align="center">
  <b>A tree-based routing library for Blazor — works with WASM, Server, and Web App (Auto / InteractiveServer / InteractiveWebAssembly)</b>
</p>

<p align="center">
  <a href="https://github.com/wtfuii/BetterRoute/actions/workflows/ci.yml">
    <img src="https://github.com/wtfuii/BetterRoute/actions/workflows/ci.yml/badge.svg" alt="Build Status">
  </a>
  <a href="https://www.nuget.org/packages/BetterRoute">
    <img src="https://img.shields.io/nuget/v/BetterRoute.svg?label=NuGet" alt="NuGet">
  </a>
  <img src="https://img.shields.io/badge/net-10.0-blueviolet" alt=".NET 10.0">
  <img src="https://img.shields.io/badge/Blazor-WASM_|_Server_|_Web_App-512bd4" alt="Blazor WASM | Server | Web App">
  <a href="LICENSE">
    <img src="https://img.shields.io/badge/license-MIT-green" alt="License">
  </a>
</p>

BetterRoute is an alternative to Blazor's built-in `Router` that replaces flat route tables with a **declarative tree of `RouteDefinition` records**. Routes are defined as nested nodes — mirroring the component hierarchy they render — which unlocks parent/child relationships, cascading state, named outlets, redirects, aliases, navigation guards, and programmatic navigation by route name.

---

## Table of Contents

- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
  - [Tree-Based Route Definitions](#tree-based-route-definitions)
  - [Layout as Routing](#layout-as-routing)
- [Features](#features)
  - [Path Parameters](#path-parameters)
  - [Redirects](#redirects)
  - [Aliases](#aliases)
  - [Navigation Guards](#navigation-guards)
  - [Named Routes](#named-routes)
  - [Named Outlets](#named-outlets)
  - [Query Strings & Fragments](#query-strings--fragments)
  - [Compile-Time Validation](#compile-time-validation)
  - [Not Found Handling](#not-found-handling)
- [API Reference](#api-reference)
- [Build & Test](#build--test)
- [Architecture](#architecture)
- [Future Features](#future-features)
- [License](#license)

---

## Quick Start

### 1. Install the package

```bash
dotnet add package BetterRoute
```

### 2. Add the router to `App.razor`

Replace the default `<Router>` with `<BetterRouter>` and define your route tree:

```razor
@using BetterRoute.Routing

<BetterRouter Routes="@Routes" NotFound="typeof(NotFoundPage)" />

@code {
    private static readonly IReadOnlyList<RouteDefinition> Routes =
    [
        new RouteDefinition("", typeof(Home)),
        new RouteDefinition("users", typeof(UsersLayout), Children:
        [
            new RouteDefinition("", typeof(UsersIndex)),
            new RouteDefinition(":userId", typeof(UserLayout), Children:
            [
                new RouteDefinition("", typeof(UserOverview)),
                new RouteDefinition("profile", typeof(UserProfile)),
                new RouteDefinition("posts/:postId", typeof(UserPost)),
            ]),
        ]),
    ];
}
```

No DI registration is needed — the router is used directly as a Blazor component.

---

## Core Concepts

### Tree-Based Route Definitions

Routes are a nested structure of `RouteDefinition` records, not a flat list of templates:

```csharp
new RouteDefinition("users", typeof(UsersLayout), Children:
[
    new RouteDefinition("", typeof(UsersIndex)),         // /users
    new RouteDefinition(":userId", typeof(UserLayout), Children:  // /users/:userId
    [
        new RouteDefinition("profile", typeof(UserProfile)),    // /users/:userId/profile
        new RouteDefinition("posts/:postId", typeof(UserPost)), // /users/:userId/posts/:postId
    ]),
])
```

This tree mirrors your component hierarchy. A parent route renders a layout component; children render inside `<RouterOutlet>`.

### Layout as Routing

There is no separate layout system. **Nested layouts are intermediate routes** whose component contains a `<RouterOutlet>`:

```razor
@* UsersLayout.razor *@
<div class="users-shell">
    <h1>Users</h1>
    <RouterOutlet />   @* renders UsersIndex, UserLayout, etc. *@
</div>
```

```razor
@* UserLayout.razor *@
@code {
    [CascadingParameter] public RouterState State { get; set; } = default!;
}
<div class="user-shell">
    <h2>User @State.GetParameter("userId")</h2>
    <RouterOutlet />   @* renders UserOverview, UserProfile, UserPost *@
</div>
```

State cascades down via `CascadingValue` with `IsFixed="false"`, so components re-render on every navigation.

---

## Features

### Path Parameters

Prefix a segment with `:` to capture it as a parameter:

```csharp
new RouteDefinition("users/:userId/posts/:postId", typeof(UserPost))
// /users/42/posts/7 → Parameters["userId"] = "42", Parameters["postId"] = "7"
```

Parameters from every level of the matched chain are merged into `RouterState.Parameters`. Deeper levels override shallower ones with the same name. Literal segments take precedence over parameter segments when paths overlap.

### Redirects

#### Static Redirects

```csharp
new RouteDefinition("profile", RedirectTo: "/users/:userId/profile")
// /profile?userId=42 → /users/42/profile
```

`:param` placeholders are substituted from captured parameters. Relative paths (`../sibling`, `./child`) are resolved against the current URL. Query strings and fragments from the original URL are preserved unless the target defines its own.

#### Dynamic Redirects

```csharp
new RouteDefinition("dashboard", RedirectToFactory: state =>
{
    return state.GetParameter("role") switch
    {
        "admin" => "/admin/dashboard",
        _       => "/user/dashboard",
    };
})
```

The factory receives a provisional `RouterState` and returns the redirect target (or `null` to signal not-found).

Redirects are mutual-exclusive with `Component` and `Aliases`. Redirect hops are capped at **10** to prevent infinite loops.

### Aliases

Alternative paths that render the same component without changing the URL:

```csharp
new RouteDefinition("", typeof(Home), Aliases: ["home", "index"])
// /home and /index both render Home without redirecting
```

Aliases are expanded at compile time into synthetic route nodes that share the same `Component` and `Children` by reference.

### Navigation Guards

Three layers of guards run in order on every navigation:

#### 1. Per-Component Leave Guards (`IBeforeRouteLeave`)

```csharp
public class EditForm : ComponentBase, IBeforeRouteLeave
{
    [CascadingParameter] public GuardRegistrar GuardRegistrar { get; set; } = default!;

    protected override void OnInitialized()
        => GuardRegistrar.Register(this, depth: 0);

    public async ValueTask<GuardResult> CanLeaveAsync(NavigationContext ctx, CancellationToken ct)
    {
        if (HasUnsavedChanges)
        {
            var confirmed = await JS.InvokeAsync<bool>("confirm", "Discard changes?");
            return confirmed ? GuardResult.Ok : GuardResult.Stop;
        }
        return GuardResult.Ok;
    }

    public void Dispose() => GuardRegistrar.Unregister(this);
}
```

Leave guards are called **deepest-first**.

#### 2. Global Guard (`BeforeEach`)

```razor
<BetterRouter Routes="@Routes" BeforeEach="@CheckAuth" />

@code {
    private async ValueTask<GuardResult> CheckAuth(NavigationContext ctx, CancellationToken ct)
    {
        if (ctx.To.Path.StartsWith("/admin") && !IsLoggedIn)
            return GuardResult.To("/login");
        return GuardResult.Ok;
    }
}
```

Runs after all leave guards pass.

#### 3. Per-Route Enter Guards (`BeforeEnter`)

```csharp
new RouteDefinition("admin", typeof(AdminLayout), Children: [...])
{
    BeforeEnter = async (ctx, ct) =>
    {
        if (!HasAdminRole)
            return GuardResult.Stop;
        return GuardResult.Ok;
    }
}
```

Runs only for route nodes that are **new** to the matched chain — parents reused from the previous navigation are skipped (detected via reference equality).

#### Guard Results

All guards return a `GuardResult`:

| Factory | Returns | Effect |
|---|---|---|
| `GuardResult.Ok` | `Continue` | Approve the navigation |
| `GuardResult.Stop` | `Cancel` | Cancel and restore the previous URL |
| `GuardResult.To(url)` | `Redirect` | Navigate to a different URL |

Guard exceptions are caught and forwarded to `BetterRouter.OnNavigationError`.

### Named Routes

Assign a `Name` to any route for programmatic navigation:

```csharp
new RouteDefinition("users/:userId/posts/:postId", typeof(UserPost), Name: "user.post")
```

```razor
@* Navigate from anywhere with access to RouterState *@
@code {
    [CascadingParameter] public RouterState State { get; set; } = default!;

    void GoToPost(int userId, int postId)
    {
        // Dictionary overload
        State.NavigateTo("user.post", new Dictionary<string, string>
        {
            ["userId"] = "42",
            ["postId"] = "7"
        });

        // Anonymous object overload (uses Convert.ToString with InvariantCulture)
        State.NavigateTo("user.post", new { userId = 42, postId = 7 });

        // When parameters is null, reuses current Parameters
        State.NavigateTo("user.post"); // keeps current userId, navigates to sibling
    }

    string GetPostUrl(int userId, int postId)
    {
        return State.ResolveUrl("user.post", new { userId, postId });
    }
}
```

Names use a dotted convention (`"user.post"`) and must be unique across the entire route tree. Extra keys not appearing in the template are appended as query string parameters.

### Named Outlets

A route can declare multiple named components via the `Components` dictionary:

```csharp
new RouteDefinition("users/:userId/search", typeof(UserSearch),
    Components: new Dictionary<string, Type>
    {
        ["sidebar"] = typeof(UserSidebar),
    })
```

Render them with named `<RouterOutlet>` elements:

```razor
@* In UserLayout.razor *@
<div style="display: flex; gap: 16px;">
    <main style="flex: 1;">
        <RouterOutlet />             @* renders UserSearch *@
    </main>
    <aside style="width: 220px;">
        <RouterOutlet Name="sidebar" />  @* renders UserSidebar *@
    </aside>
</div>
```

Named outlets render as **siblings** (same depth) rather than children. `MatchedRoute.AllComponents` merges the default component (keyed `""`) with all named components.

### Query Strings & Fragments

Query strings are automatically parsed and available on `RouterState`:

```csharp
// URL: /users/42/search?q=blazor&sort=asc&tag=oss&tag=web
State.Query                          // { "q": ["blazor"], "sort": ["asc"], "tag": ["oss", "web"] }
State.GetQuery("q")                  // "blazor"
State.GetQueryValues("tag")          // ["oss", "web"]
State.Fragment                       // "section-2" (from #section-2)
```

`QueryStringParser.Parse(string?)` is public and can be used standalone. Bare keys (no `=`) map to an empty-string value, matching `URLSearchParams` behavior.

### Compile-Time Validation

Routes are validated eagerly when `BetterRouter` first renders. The following are caught with clear error messages:

- **Duplicate route names** — `Name` must be unique across the tree
- **Unbound redirect params** — `:param` references in `RedirectTo` must be available from the current route or its ancestors
- **Mutual exclusivity** — cannot combine `RedirectTo`/`RedirectToFactory` with `Component` or `Aliases`
- **Missing target** — every route must have a `Component`, a named component, a redirect, or children

### Not Found Handling

Set the `NotFound` parameter on `BetterRouter` to render a component when no route matches:

```razor
<BetterRouter Routes="@Routes" NotFound="typeof(NotFoundPage)" />
```

---

## API Reference

### `RouteDefinition`

| Property | Type | Description |
|---|---|---|
| `Path` | `string` | Path template relative to parent. Segments prefixed with `:` capture parameters. Empty string `""` for index/default child. |
| `Component` | `Type?` | Component type rendered when this route matches. |
| `Children` | `IReadOnlyList<RouteDefinition>?` | Nested routes matched against remaining URL segments. |
| `Name` | `string?` | Unique name for programmatic navigation (e.g. `"user.post"`). |
| `Components` | `IReadOnlyDictionary<string, Type>?` | Named components for outlets. Default component is keyed `""`. |
| `RedirectTo` | `string?` | Static redirect template with `:param` substitution. |
| `RedirectToFactory` | `Func<RouterState, string?>?` | Dynamic redirect factory. Return `null` to signal not-found. |
| `Aliases` | `IReadOnlyList<string>?` | Alternative paths that render the same component without redirecting. |
| `BeforeEnter` | `NavigationGuard?` | Per-route enter guard (init-only property). |

### `BetterRouter`

| Parameter | Type | Description |
|---|---|---|
| `Routes` | `IReadOnlyList<RouteDefinition>` | Root route definitions. Compiled on first render and when the reference changes. |
| `NotFound` | `Type?` | Component to render when no route matches. |
| `BeforeEach` | `NavigationGuard?` | Global guard that runs on every navigation between leave and enter guards. |
| `OnNavigationError` | `Action<Exception>?` | Callback invoked when a guard throws or a redirect loop is detected. |

### `RouterState`

| Member | Type | Description |
|---|---|---|
| `Matched` | `IReadOnlyList<MatchedRoute>` | Full matched route chain from root to leaf. |
| `Current` | `MatchedRoute` | The matched route at the current rendering depth. |
| `CurrentDepth` | `int` | Zero-based index into `Matched`. |
| `Parameters` | `IReadOnlyDictionary<string, string>` | All path parameters merged across the chain. |
| `Query` | `IReadOnlyDictionary<string, IReadOnlyList<string>>` | Parsed query string values. |
| `Url` | `string` | Full absolute URL including origin. |
| `Path` | `string` | Relative path portion of the URL. |
| `Fragment` | `string?` | Fragment after `#`, or null. |
| `GetParameter(key)` | `string?` | Convenience accessor for `Parameters`. |
| `GetQuery(key)` | `string?` | First query value for a key, or null. |
| `GetQueryValues(key)` | `IReadOnlyList<string>` | All query values for a key. |
| `ResolveUrl(name, params?)` | `string` | Resolve a named route to its full URL. |
| `NavigateTo(name, params?, replace?)` | `void` | Navigate to a named route. |

### `RouterOutlet`

| Parameter | Type | Description |
|---|---|---|
| `Name` | `string?` | When `null`, renders the default component at the next depth. When set, renders a named component at the same depth. |

---

## Build & Test

```bash
# Build the solution
dotnet build BetterRoute.sln

# Run all tests
dotnet test BetterRoute.Tests/BetterRoute.Tests.csproj

# Run a specific test class
dotnet test BetterRoute.Tests/BetterRoute.Tests.csproj --filter "FullyQualifiedName~RouteMatcherTests"

# Run the sample Blazor WASM app
dotnet run --project BetterRoute.Sample/BetterRoute.Sample.csproj
```

**Requirements:** .NET 10 SDK. The library targets `net10.0` with the `browser` platform and depends on `Microsoft.AspNetCore.Components.Web` 10.0.x.

The test suite covers route matching, compile-time validation, navigation guards, named routes, named outlets, query string parsing, and redirect resolution. The library uses `InternalsVisibleTo` so tests can reach internal types.

---

## Architecture

BetterRoute processes a navigation in five stages:

1. **Definition** — Consumer declares a tree of `RouteDefinition` records in `App.razor`.
2. **Compilation** — `BetterRouter` compiles the tree into `CompiledRoute` nodes (pre-segmented paths + child references) and a `NamedRouteIndex` on parameter change. Aliases are expanded into synthetic entries.
3. **Matching** — `RouteMatcher.TryMatch` walks the compiled tree. Literal segments are case-insensitive; `:param` segments capture URL-decoded values. Literal siblings take precedence over parameter siblings.
4. **Guard Pipeline** — `GuardPipeline.RunAsync` executes three phases: leave guards (deepest-first), global `BeforeEach`, and per-route `BeforeEnter` (root-out, new nodes only). Results can be `Continue`, `Cancel`, or `Redirect`.
5. **Rendering** — A successful match produces a `RouterState` cascaded to all components. `RouterOutlet` advances depth so nested components see their own slice of the matched chain.

### Internal Pipeline

```
Navigation → Match URL → Redirect? → Build RouterState → Leave Guards → BeforeEach → Enter Guards → Commit → Render
                ↓              ↓           ↓                  ↓              ↓           ↓            ↓
           TryMatch()   Resolve/        params +           deepest-      global     per-route    CascadingValue
                        navigate        query +            first                                  + RouterOutlet
                                        fragment
```

---

## Future Features

The following features are designed but not yet implemented. See `docs/future/` for detailed design documents.

### Catch-All Segments ([docs/future/catch-all-segments.md](docs/future/catch-all-segments.md))

**Status:** Planned

A `*name` segment prefix to capture the entire remaining URL tail as a single string:

```csharp
new RouteDefinition("docs/*slug", typeof(DocsViewer));
// /docs/guides/routing → Parameters["slug"] = "guides/routing"
```

Catch-all segments must be the **last** segment in the path, cannot have children, and have the lowest priority among siblings (literal > parameter > catch-all). Useful for documentation sites, section-level 404 handlers, and file-system-style explorers.

### Route Transitions ([docs/future/transitions.md](docs/future/transitions.md))

**Status:** Planned

A `<RouterTransition>` component that wraps `<RouterOutlet>` to enable CSS-driven animated transitions between routes:

```razor
<RouterTransition Name="fade" Duration="200">
    <RouterOutlet />
</RouterTransition>
```

Supports direction-aware animation (`Forward` / `Back` / `Sibling` / `Replace`), respects `prefers-reduced-motion`, and provides `OnEnter`/`OnExit` callbacks for programmatic animation via the Web Animations API.

### Scroll Restoration ([docs/future/scroll-restoration.md](docs/future/scroll-restoration.md))

**Status:** Planned

A `ScrollBehavior` hook on `BetterRouter` for controlling scroll position after navigation:

```csharp
<BetterRouter Routes="@Routes" ScrollBehavior="@MyScroll" />

@code {
    private ScrollTarget? MyScroll(NavigationContext ctx, ScrollPosition? saved)
    {
        if (ctx.IsPopState && saved is not null)
            return new ScrollTarget.To(saved.X, saved.Y);  // restore on back

        if (ctx.To.Fragment is not null)
            return new ScrollTarget.Element(ctx.To.Fragment);  // scroll to anchor

        return new ScrollTarget.To(0, 0);  // top on forward nav
    }
}
```

Saved positions live in `sessionStorage` for back/forward restoration. Includes a JS interop helper that respects `prefers-reduced-motion` for smooth scrolling.

---

## License

MIT

---

<p align="center">
  <sub>Built with ❤️ for the Blazor community</sub>
</p>
