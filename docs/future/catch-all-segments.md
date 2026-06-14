# Catch-All Segments

## Motivation

Some URLs have a variable-length tail the router shouldn't have to enumerate up front:

- A documentation site renders `/docs/intro`, `/docs/guides/routing`, `/docs/api/components/router-outlet/parameters` from one Markdown directory tree. Encoding every depth as a separate `RouteDefinition` doesn't make sense.
- A custom "not found within a section" handler: `/admin/anything-that-doesnt-match` → render the admin shell with a "section not found" body, not the global 404.
- A file-system-style explorer where the URL path mirrors a directory hierarchy.

Without catch-all support, consumers either enumerate every depth or fall back to the global `NotFound`, which loses the surrounding layout.

## Proposed API

A `*name` segment prefix marks a catch-all. It must be the *last* segment in its path and captures the entire remainder of the URL as a single string:

```csharp
new RouteDefinition("docs/*slug", typeof(DocsViewer));
// /docs/guides/routing  →  Parameters["slug"] == "guides/routing"
// /docs                 →  Parameters["slug"] == ""           (matches the empty tail)

new RouteDefinition("admin", typeof(AdminLayout), Children:
[
    new RouteDefinition("dashboard", typeof(AdminDashboard)),
    new RouteDefinition("*rest", typeof(AdminNotFound)),       // section-level 404
]);
```

The captured value is the raw remaining path, URL-decoded segment-by-segment then rejoined with `/`. It is *not* further split — components see one string.

## Behavior

1. **Compile** — segment starting with `*` becomes a `CompiledSegment` with `IsCatchAll = true`. Compile-time check: must be the only segment in its position and must be last in the route's path.
2. **Match** — when the matcher reaches a catch-all segment, it consumes all remaining URL segments. Sets `Parameters[name] = join(remaining, "/")`. Marks the match as successful regardless of how many segments remain (including zero).
3. **Routing preference**: literal > parameter > catch-all. A catch-all is the lowest-priority sibling. Order siblings by specificity at compile time.
4. **Children** — catch-all routes cannot have children. Compile-time error if they do — there's no "remaining URL" left to match against.

## Edge cases

- **Empty tail** — `/docs` against `/docs/*slug` matches with `slug = ""`. This is the right default for "section root with optional sub-path". Document so consumers don't `Parameters["slug"].Split('/')` blindly.
- **Catch-all at root** — `new RouteDefinition("*path", typeof(Fallback))` is allowed and effectively replaces `NotFound`. Useful for SPAs that want to render an in-app 404 with their own layout. Recommended for documentation but not required.
- **Catch-all alongside literal/parameter siblings** — the literal `/dashboard` wins over `*rest`; the parameter `:id` wins over `*rest`. Test this explicitly.
- **Trailing slash in captured value** — strip leading and trailing slashes from the captured value, internal slashes preserved. So `/docs/guides/` captures `slug = "guides"`, not `"guides/"`.
- **URL decoding of the captured value** — decode each segment individually then join with literal `/`. So `/docs/a%2Fb/c` captures `slug = "a/b/c"` — three segments, where the middle one's literal slash is preserved? Tricky. Probably better: decode segment-wise, join with `/`, and accept the ambiguity. Document it.
- **Mid-path `*`** — `users/*rest/profile` is disallowed. Catch-all must be terminal. Compile-time error.
- **Multiple catch-alls per parent** — disallowed. Compile-time error. There's no sensible ordering.
- **Catch-all with no other siblings under a parent** — totally fine and common (the section-404 case).

## Open questions

- **Syntax** — Vue Router uses `:pathMatch(.*)*` regex syntax. Angular uses `**`. Blazor's built-in router uses `{*rest}`. We chose `*rest` for brevity and Vue-ish feel. Consider compat with Blazor's `{*}` syntax. Probably not — they're config-syntactically different anyway.
- **Captured-value shape**: string vs. `IReadOnlyList<string>`? A list is more useful programmatically; a string matches typical user expectations. Possibly both: `Parameters["slug"]` is the string, `Parameters["slug.segments"]` is the list — but that's ugly. Stick with string and let consumers split.
- **Repeated catch-alls across levels** — if both `/admin/*rest` and `/admin/users/*rest` exist, the more-specific one wins by sibling priority rules. Worth a test.
- **Decoding policy** — see "URL decoding" above. Pick a rule and document.
- **Interaction with [redirects](./redirects-and-aliases.md)** — can a redirect target reference `:rest`? Yes, by the same substitution rules as other params. Captured value goes in as-is. Worth a test.
