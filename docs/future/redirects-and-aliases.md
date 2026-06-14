# Redirects and Aliases

## Motivation

Two related but distinct needs:

- **Redirects**: the URL the user typed isn't the URL we want them on. Either the route moved (`/profile` → `/user/me`), or a path doesn't have its own content but should send the user somewhere (`/` → `/dashboard`). The browser's URL bar should update to the canonical target.
- **Aliases**: the same content should be reachable from multiple URLs (`/help` and `/docs` both render the docs landing page). The URL bar should *not* change — both are first-class.

Without these, consumers fake them with guards (for redirects) or duplicate route entries (for aliases), and SEO/bookmark behavior is fragile.

## Proposed API

Two new `RouteDefinition` constructors-or-variants. The simplest shape is to keep `RouteDefinition` as is and add a redirect-only variant:

```csharp
public sealed record RouteDefinition(
    string Path,
    Type? Component = null,
    IReadOnlyList<RouteDefinition>? Children = null,
    string? Name = null,
    string? RedirectTo = null,                    // absolute or relative URL template
    Func<RouterState, string>? RedirectToFactory = null,
    IReadOnlyList<string>? Aliases = null);
```

Static redirect:

```csharp
new RouteDefinition("profile", RedirectTo: "/user/me");
new RouteDefinition(":userId/legacy", RedirectTo: "/users/:userId");  // params reused
```

Dynamic redirect (resolves at match time):

```csharp
new RouteDefinition(":userId/dashboard",
    RedirectToFactory: state => state.Parameters["userId"] == "me"
        ? "/users/" + session.CurrentUserId + "/dashboard"
        : null);  // null → fall through, no redirect
```

Alias:

```csharp
new RouteDefinition("docs", typeof(Docs), Aliases: ["help", "support"]);
// /help and /support both render Docs; URL bar stays as user typed.
```

## Behavior

### Redirects

1. During matching, when a route with a `RedirectTo` or `RedirectToFactory` produces a successful match, **abandon the rest of matching**. Resolve the redirect target.
2. **Parameter substitution**: `RedirectTo: "/users/:userId"` rewrites `:userId` using parameters already captured by the matched chain up to and including the redirect node. Missing parameter → throw with a clear error at compile time if possible, otherwise at match time.
3. **Relative paths**: `RedirectTo: "../sibling"` is resolved against the current matched location. Absolute paths (`/foo`) are taken as-is.
4. `BetterRouter` calls `NavigationManager.NavigateTo(target, replace: true)`. `replace: true` keeps the browser back-button working naturally — the user lands on the canonical URL without an extra history entry.
5. Redirect chain limit: 10 hops, then throw. Same rule as guard redirects.

### Aliases

1. During tree compilation, each alias is duplicated into a synthetic `CompiledRoute` pointing at the same `Component`, sharing `Children` by reference.
2. On match, the matched chain looks identical to the canonical match — same `Component` types, same `MatchedRoute.Definition` (the original, not the alias). `RouterState.Path` reflects the *actual* URL the user is on.
3. No URL change; the alias is a real route in its own right.

## Edge cases

- **Redirect on the root URL** — common case (`/` → `/dashboard`). Make sure the bootstrap navigation handles it on first load, not just on user click.
- **Redirect during initial load** — must not flash the would-be-redirected component. Resolve redirects *before* the first render. The matcher should already do this if redirects are processed during `TryMatch`.
- **Redirect to a non-matching URL** — the redirected URL doesn't match anything. Fall through to `NotFound`. Do not redirect again; that's how loops happen.
- **Redirect with unbound parameters** — `RedirectTo: "/users/:userId"` from a route that never captured `:userId`. Compile-time check is the right place to catch this: when compiling each redirect template, verify all `:name` placeholders appear in the ancestor path.
- **Alias with children** — semantically the children also reach via the alias. `/help` aliases `/docs`, and `/docs/api` exists → `/help/api` should also work. Implementation: aliases share the children tree by reference, so this falls out for free.
- **Alias on a route that has a `RedirectTo`** — disallow at compile time. It's nonsensical.
- **Case sensitivity in redirect targets** — when substituting captured params, preserve their case. Don't lowercase.
- **Query/fragment preservation on redirect** — when redirecting, do we keep the original query string? Default: yes, append the original `?query#fragment` to the redirect target unless the target already has one. Document this — it's the right default but surprising if you don't expect it.

## Open questions

- **Component-less route definitions**: with redirects, `Component` becomes optional. The type signature is `Type? Component`. Need to enforce at construction-time that *exactly one* of `Component`, `RedirectTo`, `RedirectToFactory` is set. Could be a sealed hierarchy of `RouteDefinition` instead of a single record with optional fields — cleaner but more types.
- **Per-route vs. global redirects**: should there be a global redirect table on `BetterRouter` (a `(from → to)` dictionary) for trivial cases? Probably no — every redirect can also be expressed as a route entry, and the global table tempts people to put complex logic there.
- **Alias on a parent affects children URLs** — when a user lands on `/help/api`, is the matched chain `[Docs (via alias), Api]` or `[Docs, Api]`? Probably the latter — aliases shouldn't be visible in the matched chain; treat them as URL synonyms only.
- **Trailing slash policy**: `/docs` and `/docs/` — equivalent or different? Pick one (suggest: equivalent, normalize at match time) and document. Affects redirects ("redirect bare path to canonical with/without trailing slash") more than aliases.
