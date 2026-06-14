# Named Routes & Programmatic Navigation

## Motivation

Hardcoding URL strings in markup is fragile:

- Renaming a path (`/users` → `/people`) requires hunting through `NavigateTo("users/...")` calls across the entire codebase.
- Building parameterized URLs by hand is repetitive and error-prone (`$"/users/{userId}/posts/{postId}"` repeated 30 times).
- IDE refactor tools can't follow strings.

`RouteDefinition` already has an unused `Name` field. This feature wires it up to a typed navigator service that resolves URLs from `(name, params)` tuples.

## Proposed API

A scoped service registered alongside the router:

```csharp
public interface IRouter
{
    string ResolveUrl(string routeName, IReadOnlyDictionary<string, string>? parameters = null);
    void NavigateTo(string routeName, IReadOnlyDictionary<string, string>? parameters = null, bool replace = false);
    void NavigateTo(string routeName, object? parameters, bool replace = false);   // anonymous-object overload
}
```

Registration:

```csharp
builder.Services.AddBetterRoute();   // registers IRouter, NavigationManager-backed
```

Routes declared with names:

```csharp
new RouteDefinition(":userId/posts/:postId", typeof(UserPost), Name: "user.post");
```

Consumer:

```razor
@inject IRouter Router

<a href="@Router.ResolveUrl("user.post", new { userId = "42", postId = "7" })">View post</a>
<button @onclick="@(() => Router.NavigateTo("user.post", new { userId, postId }))">Go</button>
```

`RouterState` also gets a convenience method so children can navigate to siblings without string params:

```csharp
state.NavigateTo("user.profile");   // reuses current Parameters["userId"]
```

## Behavior

### Name resolution

1. At route-tree compile time, walk every route in the tree. For each route with a `Name`:
   - Compute its **full path template** by concatenating all ancestor path templates with `/`.
   - Index it in a `Dictionary<string, NamedRoute>` on the compiled tree.
2. Names must be unique across the entire tree. Duplicate name → throw at compile time.

### URL building

1. Look up the named route's full path template.
2. Walk its segments. Literal segments are emitted as-is. `:param` segments require a value in `parameters` — missing → throw.
3. Catch-all `*rest` segments: the value is emitted verbatim (already a joined path).
4. Extra keys in `parameters` that don't appear in the template → appended as query string (`?foo=bar`). This matches Vue Router's behavior.

### Programmatic navigation

`NavigateTo(name, params)` resolves the URL, then delegates to `NavigationManager.NavigateTo(url, replace)`. The existing `LocationChanged` flow takes over from there.

The `state.NavigateTo("user.profile")` convenience grabs the current `RouterState.Parameters` and merges with any explicit overrides — same dictionary semantics, just with defaults pre-filled.

## Edge cases

- **Name collision** — duplicate names anywhere in the tree → throw at compile time with both locations identified.
- **Missing parameter on resolve** — throw with the template and missing key in the message.
- **Extra parameters on resolve** — appended as query string. If [query parameters](./query-parameters.md) ships first, this composes naturally.
- **Named route that has been promoted to a redirect** — resolve gets the *target* URL, not the redirect URL. (Or: resolve to the redirect URL and let normal navigation hit the redirect chain. Pick one. Suggest the latter — fewer special cases.)
- **Resolving a named route while in plan mode / SSR** — works the same; resolution is pure.
- **`state.NavigateTo("self.sibling")` semantics** — overrides win over current `Parameters`. If the named route is shallower than the current chain, drop deeper parameters when building.
- **Anonymous object overload** — `new { userId = 42 }`. Integers need `.ToString()` somewhere; use `Convert.ToString(value, CultureInfo.InvariantCulture)` to avoid locale surprises. Document.
- **Nested route names with dots** — `"user.post"` vs `"user/post"` vs `"userPost"`. Pick a convention (suggest dots — readable, common). The library doesn't care; just don't add accidental hierarchy semantics to dots.

## Open questions

- **Generated typed accessors?** A source generator could emit `Routes.User.Post(userId, postId) → string`. Eliminates name strings entirely. Worth its own design pass; out of scope here.
- **`Router.CurrentRoute` accessor** — `IRouter` could expose the live `RouterState`. Duplicates the cascading parameter but useful in non-component code (services). Probably yes.
- **Relative named routes** — `state.NavigateTo("./profile")` to mean "sibling of current"? Adds parser complexity. Vue Router supports it. Defer.
- **History API integration** — `replace: true` vs. push, `state` object passthrough for browser history. Match Blazor's `NavigationManager.NavigateTo` overloads; don't reinvent.
- **DI registration shape** — a single `AddBetterRoute()` extension is the right entry point. Should it also auto-discover routes from attributes (e.g. `[Route(...)]` decorations on components)? Probably no — that conflicts with the config-tree model we picked. Stay coherent.
