# Scroll Restoration

## Motivation

Default browser behavior for SPAs: scroll position is whatever it was after the last DOM mutation. That's usually wrong:

- User scrolls halfway down a long list, clicks an item, lands on the detail page scrolled to the *middle* of it (because the parent layout's scroll position carried over). Confusing.
- User clicks Back from the detail page — expects to land where they left the list. Instead lands at the top, losing their place.
- Anchor links (`#section-2`) work on first load but not on navigation.

Vue Router solves this with a `scrollBehavior` function the consumer implements. Blazor has no built-in story; you write JS interop.

## Proposed API

A single hook on `BetterRouter`:

```csharp
public delegate ScrollTarget? ScrollBehavior(NavigationContext ctx, ScrollPosition? saved);

public sealed record ScrollPosition(double X, double Y);
public abstract record ScrollTarget
{
    public sealed record To(double X, double Y) : ScrollTarget;
    public sealed record Element(string Selector, bool Smooth = false) : ScrollTarget;
    public sealed record Restore : ScrollTarget;     // sentinel: use `saved`
    public sealed record Keep : ScrollTarget;        // do not change scroll
}
```

```razor
<BetterRouter Routes="@Routes" ScrollBehavior="@MyScroll" />

@code {
    private ScrollTarget? MyScroll(NavigationContext ctx, ScrollPosition? saved)
    {
        // Back/forward: restore where the user was.
        if (ctx.IsPopState && saved is not null)
            return new ScrollTarget.To(saved.X, saved.Y);

        // Hash anchor: scroll to it.
        if (ctx.To.Url.Contains('#'))
            return new ScrollTarget.Element(ctx.To.Url[ctx.To.Url.IndexOf('#')..]);

        // New forward navigation: scroll to top.
        return new ScrollTarget.To(0, 0);
    }
}
```

If `ScrollBehavior` is not set, the router does **nothing** — preserves current Blazor behavior. Opt-in.

## Behavior

1. **Before navigation commits** — capture current scroll position via JS interop (`window.scrollX`, `window.scrollY`). Store keyed by a per-navigation id (browser history `state`). Survives back/forward.
2. **After the new render is committed** (use `OnAfterRenderAsync`) — invoke `ScrollBehavior` with the navigation context and any saved position for this history entry.
3. Apply the returned `ScrollTarget`:
   - `To(x, y)` → `window.scrollTo(x, y)`
   - `Element(sel)` → `document.querySelector(sel)?.scrollIntoView({ behavior: smooth ? 'smooth' : 'auto' })`
   - `Restore` → `window.scrollTo(saved.X, saved.Y)` (no-op if `saved` is null)
   - `Keep` → do nothing
   - `null` → do nothing
4. **Storage** — saved positions live in `sessionStorage` keyed by history entry id, so they survive page reloads and tab restores but not full session ends. Roughly the same lifecycle as the back-forward cache.

JS interop helper: ships with the package, lazy-loaded the first time `ScrollBehavior` is invoked.

## Edge cases

- **First load** — `ctx.From` is null. No saved position. `ScrollBehavior` decides; default behavior (when consumer scrolls to top) is fine.
- **Hash-only navigation** (`/page` → `/page#section`) — Blazor's NavigationManager fires `LocationChanged` for hash changes. Treat as a navigation; default policy is "scroll to the element".
- **Image-loaded layout shift** — between commit and image-loaded reflow, the scroll target moves. Common Vue solution: wait one animation frame, then scroll. Optionally wait until images on screen finish loading. Probably defer the fancy variant — consumers can opt in via `Smooth` or DIY.
- **Scroll containers other than `window`** — long lists inside a fixed-height div, etc. Out of scope. Consumers handle in their components.
- **iOS Safari hash-scroll bug** — Safari sometimes ignores `scrollIntoView` until next tick. Workaround: `setTimeout(..., 0)` wrapper. Bake into the JS helper.
- **Smooth-scroll + reduced-motion preference** — respect `prefers-reduced-motion`; downgrade `smooth` to `auto` automatically. Match the [transitions](./transitions.md) policy.
- **Server-side rendering** — no `window`; skip the whole thing. The hook never fires during prerender.
- **Concurrent navigations** — if the user clicks again before the first scroll settles, drop the in-flight scroll and apply the new behavior. Use a navigation generation counter.

## Open questions

- **Default behavior when no hook is set** — do nothing (current proposal), or always scroll to top on forward navigation? Vue's default is `null` (no-op). Stick with no-op so users aren't surprised; document that opting in is a one-liner.
- **Per-route override** — a `RouteDefinition.ScrollBehavior`? Probably overkill — global is enough and the hook already sees the full context.
- **Saved-position storage backend** — `sessionStorage` is the standard answer but means strings. Could use the in-memory `Dictionary<historyId, ScrollPosition>` for speed and rely on history popstate for traversal. Less robust across reloads. Pick `sessionStorage` and accept the JSON-marshal cost.
- **Browser scroll-restoration API** — `window.history.scrollRestoration = 'manual'` disables native restoration so our hook always wins. Set this in the JS helper on load.
- **Anchor smoothness default** — `false` (instant) is safer. Smooth scrolling on long pages can take seconds and feels broken to many users. Consumers opt in per call.
