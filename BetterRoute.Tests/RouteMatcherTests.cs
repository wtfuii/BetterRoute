using BetterRoute.Routing;
using BetterRoute.Routing.Internal;

namespace BetterRoute.Tests;

public class RouteMatcherTests
{
    private sealed class A;
    private sealed class B;
    private sealed class C;
    private sealed class D;
    private sealed class E;

    // ── Existing tests (adapted to MatchResult) ──────────────────────

    [Fact]
    public void Matches_single_literal_segment()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users", typeof(A)),
        ]);

        var result = RouteMatcher.TryMatch("/users", tree);
        var success = Assert.IsType<MatchResult.Success>(result);
        var matched = success.Matched;
        Assert.Single(matched);
        Assert.Equal(typeof(A), matched[0].Definition.Component);
        Assert.Empty(matched[0].SegmentParameters);
    }

    [Fact]
    public void Captures_path_parameter()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users/:userId", typeof(A)),
        ]);

        var result = RouteMatcher.TryMatch("/users/42", tree);
        var success = Assert.IsType<MatchResult.Success>(result);
        var matched = success.Matched;
        Assert.Single(matched);
        Assert.Equal("42", matched[0].SegmentParameters["userId"]);
    }

    [Fact]
    public void Descends_into_children_and_collects_parameters_per_level()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users", typeof(A), Children: [
                new RouteDefinition(":userId", typeof(B), Children: [
                    new RouteDefinition("posts/:postId", typeof(C)),
                ]),
            ]),
        ]);

        var result = RouteMatcher.TryMatch("/users/42/posts/7", tree);
        var success = Assert.IsType<MatchResult.Success>(result);
        var matched = success.Matched;
        Assert.Equal(3, matched.Count);
        Assert.Equal(typeof(A), matched[0].Definition.Component);
        Assert.Empty(matched[0].SegmentParameters);
        Assert.Equal(typeof(B), matched[1].Definition.Component);
        Assert.Equal("42", matched[1].SegmentParameters["userId"]);
        Assert.Equal(typeof(C), matched[2].Definition.Component);
        Assert.Equal("7", matched[2].SegmentParameters["postId"]);
    }

    [Fact]
    public void Returns_not_found_when_no_route_matches()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users", typeof(A)),
        ]);

        var result = RouteMatcher.TryMatch("/teams", tree);
        Assert.IsType<MatchResult.NotFound>(result);
    }

    [Fact]
    public void Matches_index_child_when_url_ends_at_parent()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users", typeof(A), Children: [
                new RouteDefinition("", typeof(B)),
                new RouteDefinition(":userId", typeof(C)),
            ]),
        ]);

        var result = RouteMatcher.TryMatch("/users", tree);
        var success = Assert.IsType<MatchResult.Success>(result);
        var matched = success.Matched;
        Assert.Equal(2, matched.Count);
        Assert.Equal(typeof(A), matched[0].Definition.Component);
        Assert.Equal(typeof(B), matched[1].Definition.Component);
    }

    [Fact]
    public void Literal_segments_are_case_insensitive()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("Users/:userId", typeof(A)),
        ]);

        var result = RouteMatcher.TryMatch("/USERS/42", tree);
        var success = Assert.IsType<MatchResult.Success>(result);
        var matched = success.Matched;
        Assert.Single(matched);
        Assert.Equal("42", matched[0].SegmentParameters["userId"]);
    }

    [Fact]
    public void Parameter_values_are_url_decoded()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("search/:term", typeof(A)),
        ]);

        var result = RouteMatcher.TryMatch("/search/hello%20world", tree);
        var success = Assert.IsType<MatchResult.Success>(result);
        var matched = success.Matched;
        Assert.Equal("hello world", matched[0].SegmentParameters["term"]);
    }

    [Fact]
    public void Empty_root_path_matches_root_url()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("", typeof(A)),
        ]);

        var result = RouteMatcher.TryMatch("/", tree);
        var success = Assert.IsType<MatchResult.Success>(result);
        var matched = success.Matched;
        Assert.Single(matched);
        Assert.Equal(typeof(A), matched[0].Definition.Component);
    }

    [Fact]
    public void Prefers_literal_sibling_over_parameter_sibling()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users", typeof(A), Children: [
                new RouteDefinition("me", typeof(B)),
                new RouteDefinition(":userId", typeof(C)),
            ]),
        ]);

        var result = RouteMatcher.TryMatch("/users/me", tree);
        var success = Assert.IsType<MatchResult.Success>(result);
        var matched = success.Matched;
        Assert.Equal(typeof(B), matched[1].Definition.Component);
    }

    [Fact]
    public void Falls_back_to_parameter_sibling_when_literal_does_not_match()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users", typeof(A), Children: [
                new RouteDefinition("me", typeof(B)),
                new RouteDefinition(":userId", typeof(C)),
            ]),
        ]);

        var result = RouteMatcher.TryMatch("/users/42", tree);
        var success = Assert.IsType<MatchResult.Success>(result);
        var matched = success.Matched;
        Assert.Equal(typeof(C), matched[1].Definition.Component);
        Assert.Equal("42", matched[1].SegmentParameters["userId"]);
    }

    [Fact]
    public void Multisegment_paths_are_split_on_slash()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("a/b/c", typeof(A)),
        ]);

        var result = RouteMatcher.TryMatch("/a/b/c", tree);
        var success = Assert.IsType<MatchResult.Success>(result);
        var matched = success.Matched;
        Assert.Single(matched);
    }

    [Fact]
    public void Does_not_match_if_extra_segments_unconsumed()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users", typeof(A)),
        ]);

        var result = RouteMatcher.TryMatch("/users/42", tree);
        Assert.IsType<MatchResult.NotFound>(result);
    }

    // ── New tests: redirects ─────────────────────────────────────────

    [Fact]
    public void Static_redirect_route_returns_StaticRedirect()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("profile", RedirectTo: "/user/me"),
        ]);

        var result = RouteMatcher.TryMatch("/profile", tree);
        var redirect = Assert.IsType<MatchResult.StaticRedirect>(result);
        Assert.Equal("/user/me", redirect.RedirectTemplate);
        Assert.Single(redirect.Matched);
        Assert.Equal("profile", redirect.Matched[0].Definition.Path);
    }

    [Fact]
    public void Static_redirect_passes_captured_parameters()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users", typeof(A), Children: [
                new RouteDefinition(":userId/legacy", RedirectTo: "/users/:userId"),
            ]),
        ]);

        var result = RouteMatcher.TryMatch("/users/42/legacy", tree);
        var redirect = Assert.IsType<MatchResult.StaticRedirect>(result);
        Assert.Equal("/users/:userId", redirect.RedirectTemplate);
        Assert.Equal(2, redirect.Matched.Count);
        Assert.Equal("42", redirect.Matched[1].SegmentParameters["userId"]);
    }

    [Fact]
    public void Dynamic_redirect_route_returns_DynamicRedirect()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("dashboard", RedirectToFactory: _ => "/user/me/dashboard"),
        ]);

        var result = RouteMatcher.TryMatch("/dashboard", tree);
        var redirect = Assert.IsType<MatchResult.DynamicRedirect>(result);
        Assert.NotNull(redirect.Factory);
        Assert.Single(redirect.Matched);
    }

    [Fact]
    public void Redirect_on_index_child_returns_StaticRedirect()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("", RedirectTo: "/dashboard"),
        ]);

        var result = RouteMatcher.TryMatch("/", tree);
        var redirect = Assert.IsType<MatchResult.StaticRedirect>(result);
        Assert.Equal("/dashboard", redirect.RedirectTemplate);
    }

    [Fact]
    public void Deep_redirect_propagates_up_through_layout()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("admin", typeof(A), Children: [
                new RouteDefinition("old-settings", RedirectTo: "/admin/settings"),
            ]),
        ]);

        var result = RouteMatcher.TryMatch("/admin/old-settings", tree);
        var redirect = Assert.IsType<MatchResult.StaticRedirect>(result);
        Assert.Equal("/admin/settings", redirect.RedirectTemplate);
        Assert.Equal(2, redirect.Matched.Count);
    }

    [Fact]
    public void Redirect_abandons_child_matching()
    {
        // A redirect route with children — the redirect fires when the URL
        // exactly matches the redirect path. Children handle deeper URLs.
        var tree = CompiledRoute.Compile([
            new RouteDefinition("old", RedirectTo: "/new", Children: [
                new RouteDefinition(":id", typeof(A)),
            ]),
        ]);

        // Exact match on the redirect route → redirect.
        var result = RouteMatcher.TryMatch("/old", tree);
        Assert.IsType<MatchResult.StaticRedirect>(result);

        // Deeper URL → child matches (no redirect).
        var childResult = RouteMatcher.TryMatch("/old/42", tree);
        var success = Assert.IsType<MatchResult.Success>(childResult);
        Assert.Equal(typeof(A), success.Matched[^1].Definition.Component);
    }

    // ── New tests: aliases ───────────────────────────────────────────

    [Fact]
    public void Alias_matches_same_definition()
    {
        var canonical = new RouteDefinition("docs", typeof(A), Aliases: ["help"]);
        var tree = CompiledRoute.Compile([canonical]);

        // Canonical path.
        var canonResult = RouteMatcher.TryMatch("/docs", tree);
        var canonSuccess = Assert.IsType<MatchResult.Success>(canonResult);
        Assert.Same(canonical, canonSuccess.Matched[0].Definition);

        // Alias path — same Definition reference.
        var aliasResult = RouteMatcher.TryMatch("/help", tree);
        var aliasSuccess = Assert.IsType<MatchResult.Success>(aliasResult);
        Assert.Same(canonical, aliasSuccess.Matched[0].Definition);
    }

    [Fact]
    public void Alias_with_children_accessible()
    {
        var canonical = new RouteDefinition("docs", typeof(A), Children: [
            new RouteDefinition("api", typeof(B)),
        ], Aliases: ["help"]);

        var tree = CompiledRoute.Compile([canonical]);

        // Child reachable via canonical path.
        var canonResult = RouteMatcher.TryMatch("/docs/api", tree);
        var canonSuccess = Assert.IsType<MatchResult.Success>(canonResult);
        Assert.Equal(2, canonSuccess.Matched.Count);
        Assert.Equal(typeof(B), canonSuccess.Matched[^1].Definition.Component);

        // Child reachable via alias path.
        var aliasResult = RouteMatcher.TryMatch("/help/api", tree);
        var aliasSuccess = Assert.IsType<MatchResult.Success>(aliasResult);
        Assert.Equal(2, aliasSuccess.Matched.Count);
        Assert.Equal(typeof(B), aliasSuccess.Matched[^1].Definition.Component);
    }

    [Fact]
    public void Multiple_aliases_on_same_route()
    {
        var canonical = new RouteDefinition("docs", typeof(A), Aliases: ["help", "support"]);
        var tree = CompiledRoute.Compile([canonical]);

        Assert.IsType<MatchResult.Success>(RouteMatcher.TryMatch("/docs", tree));
        Assert.IsType<MatchResult.Success>(RouteMatcher.TryMatch("/help", tree));
        Assert.IsType<MatchResult.Success>(RouteMatcher.TryMatch("/support", tree));
    }

    [Fact]
    public void Alias_preserves_parameter_capture()
    {
        var canonical = new RouteDefinition("users/:userId", typeof(A), Aliases: ["u/:userId"]);
        var tree = CompiledRoute.Compile([canonical]);

        var result = RouteMatcher.TryMatch("/u/42", tree);
        var success = Assert.IsType<MatchResult.Success>(result);
        Assert.Equal("42", success.Matched[0].SegmentParameters["userId"]);
    }

    // ── Edge-case tests ──────────────────────────────────────────────

    [Fact]
    public void Component_required_in_non_redirect_non_parent_routes()
    {
        // Route with no Component, no redirect, and no children should throw.
        Assert.Throws<InvalidOperationException>(() =>
            CompiledRoute.Compile([new RouteDefinition("orphan")]));
    }

    [Fact]
    public void Parent_without_component_is_valid()
    {
        // A route with only children (no Component, no redirect) is valid —
        // it acts as a layout container.
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users", Children: [
                new RouteDefinition(":id", typeof(A)),
            ]),
        ]);

        var result = RouteMatcher.TryMatch("/users/42", tree);
        var success = Assert.IsType<MatchResult.Success>(result);
        Assert.Equal(2, success.Matched.Count);
    }

    [Fact]
    public void NotFound_when_url_has_fewer_segments_than_multisegment_route()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("a/b/c", typeof(A)),
        ]);

        var result = RouteMatcher.TryMatch("/a/b", tree);
        Assert.IsType<MatchResult.NotFound>(result);
    }
}
