# Query Parameters

## Motivation

Path parameters (`:userId`) identify the *resource*. Query parameters (`?tab=settings&page=3`) describe how to *view* it — tab selection, pagination, filters, sort order. Components frequently need both, and forcing query state into the URL via path segments is awkward (`/users/42/settings/page/3` doesn't express that those are orthogonal toggles).

The v1 router strips the query string before matching. This document describes adding first-class read access (and optional binding) to query parameters at every level of the matched chain.

## Proposed API

Extend `RouterState` with a parsed query dictionary, exposed alongside path parameters but kept separate so consumers don't confuse the two:

```csharp
public sealed record RouterState(
    IReadOnlyList<MatchedRoute> Matched,
    int CurrentDepth,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Query,
    string Url,
    string Path)
{
    public string? GetQuery(string key) =>
        Query.TryGetValue(key, out var values) && values.Count > 0 ? values[0] : null;

    public IReadOnlyList<string> GetQueryValues(string key) =>
        Query.TryGetValue(key, out var values) ? values : [];
}
```

`Query` is a multi-value dictionary because `?tag=a&tag=b` is a legitimate pattern (e.g. multi-select filters). `GetQuery` is the convenience accessor for the common single-value case.

Consumers read:

```razor
@code {
    [CascadingParameter] public RouterState State { get; set; } = default!;
}
<p>Tab: @(State.GetQuery("tab") ?? "overview")</p>
<p>Tags: @string.Join(", ", State.GetQueryValues("tag"))</p>
```

Writing: out of scope for this doc. Consumers use `NavigationManager.NavigateTo($"users/42?tab=settings")` directly. A typed builder could come later.

## Behavior

1. `BetterRouter.OnParametersSet` / `OnLocationChanged` splits the URL once into path and query (`indexof '?'`, then to fragment `#`).
2. Query is parsed by `System.Web.HttpUtility.ParseQueryString` (or a lightweight in-house parser to avoid dragging in `System.Web` on Blazor WASM — `Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery` is the right pick).
3. The same `Query` dictionary is included in every level's cascaded `RouterState`. Query parameters are not "captured" per-segment the way path params are; they're global to the URL.
4. A change to query parameters re-fires `LocationChanged`, the matcher re-runs, but `Matched` is likely identical — only `Query` and `Url` change. Components subscribing via cascading value still re-render because the `RouterState` record reference changes.

## Edge cases

- **No query** → `Query` is the shared empty dictionary, not null. Saves null checks everywhere.
- **Duplicate keys** → preserve insertion order. `?a=1&a=2` becomes `Query["a"] == ["1", "2"]`.
- **Bare keys** (`?debug`) → present with an empty-string value. Matches browser `URLSearchParams` behavior.
- **URL-encoded values** → unescape on parse. `%20` → space.
- **Fragment** (`#section`) — stripped before parsing query. A separate `Fragment` field on `RouterState` is an obvious add-on; trivial.
- **Case sensitivity** — keys are case-sensitive. `?Tab=` and `?tab=` are different. This matches RFC 3986 + common JS framework behavior and avoids ambiguity with multi-value collapsing.
- **Re-matching cost** — query changes trigger full path re-match. Cheap (microseconds for typical trees) so not worth optimizing yet.

## Open questions

- **Typed binding**: Vue Router doesn't bind query to component params, but Blazor's `[SupplyParameterFromQuery]` does. Should we offer an analog (`[BetterQuery("tab")] string? Tab { get; set; }`)? Adds a source generator or reflection layer. Probably defer to a v3.
- **Writing**: should `RouterState` expose `WithQuery(string key, string? value)` returning a new URL string, so consumers don't hand-build query strings? Small ergonomics win. Defer until requested.
- **Array syntax**: PHP and some Java frameworks use `?tag[]=a&tag[]=b`. The proposal above does *not* support this — consumers use repeated keys. Decide before shipping that there's no requirement for `[]` syntax.
- **Encoding**: `+` → space, or `+` → literal `+`? `QueryHelpers.ParseQuery` decodes `+` as space, which matches `application/x-www-form-urlencoded` but not strict RFC 3986. Document the choice.
