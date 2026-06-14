# BetterRoute — Deferred Features

These features are out of scope for v1. Each file in this folder is a design sketch for one of them: motivation, proposed API, behavior, edge cases, and open questions. Nothing here is committed implementation — it's a parking lot of ideas to pick up later.

| File | Feature |
| --- | --- |
| [query-parameters.md](./query-parameters.md) | Query string parsing and binding |
| [navigation-guards.md](./navigation-guards.md) | `beforeEach`, `beforeEnter`, async guards |
| [redirects-and-aliases.md](./redirects-and-aliases.md) | `redirect:` and `alias:` route entries |
| [named-outlets.md](./named-outlets.md) | Multiple named `<RouterOutlet>` per parent |
| [lazy-components.md](./lazy-components.md) | Async/lazy-loaded route components |
| [transitions.md](./transitions.md) | Animated transitions between route changes |
| [catch-all-segments.md](./catch-all-segments.md) | `*rest` segments that capture arbitrary tail |
| [named-routes.md](./named-routes.md) | `router.push({ name, params })` resolution |
| [scroll-restoration.md](./scroll-restoration.md) | Restoring scroll position on back/forward |

## How to read these

Every doc follows the same shape:

1. **Motivation** — why this is worth building, including the use case that prompts it.
2. **Proposed API** — what consumers would write. Code samples.
3. **Behavior** — what the router does at runtime.
4. **Edge cases** — the cases that look easy but aren't.
5. **Open questions** — decisions we'd need to make before implementing.

When promoting one of these to a real feature, copy the file into a working plan, fill in the open questions, and remove it from this folder once shipped.
