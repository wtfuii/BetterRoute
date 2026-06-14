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

    [Fact]
    public void Matches_single_literal_segment()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users", typeof(A)),
        ]);

        Assert.True(RouteMatcher.TryMatch("/users", tree, out var matched));
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

        Assert.True(RouteMatcher.TryMatch("/users/42", tree, out var matched));
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

        Assert.True(RouteMatcher.TryMatch("/users/42/posts/7", tree, out var matched));
        Assert.Equal(3, matched.Count);
        Assert.Equal(typeof(A), matched[0].Definition.Component);
        Assert.Empty(matched[0].SegmentParameters);
        Assert.Equal(typeof(B), matched[1].Definition.Component);
        Assert.Equal("42", matched[1].SegmentParameters["userId"]);
        Assert.Equal(typeof(C), matched[2].Definition.Component);
        Assert.Equal("7", matched[2].SegmentParameters["postId"]);
    }

    [Fact]
    public void Returns_false_when_no_route_matches()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users", typeof(A)),
        ]);

        Assert.False(RouteMatcher.TryMatch("/teams", tree, out var matched));
        Assert.Empty(matched);
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

        Assert.True(RouteMatcher.TryMatch("/users", tree, out var matched));
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

        Assert.True(RouteMatcher.TryMatch("/USERS/42", tree, out var matched));
        Assert.Single(matched);
        Assert.Equal("42", matched[0].SegmentParameters["userId"]);
    }

    [Fact]
    public void Parameter_values_are_url_decoded()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("search/:term", typeof(A)),
        ]);

        Assert.True(RouteMatcher.TryMatch("/search/hello%20world", tree, out var matched));
        Assert.Equal("hello world", matched[0].SegmentParameters["term"]);
    }

    [Fact]
    public void Empty_root_path_matches_root_url()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("", typeof(A)),
        ]);

        Assert.True(RouteMatcher.TryMatch("/", tree, out var matched));
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

        Assert.True(RouteMatcher.TryMatch("/users/me", tree, out var matched));
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

        Assert.True(RouteMatcher.TryMatch("/users/42", tree, out var matched));
        Assert.Equal(typeof(C), matched[1].Definition.Component);
        Assert.Equal("42", matched[1].SegmentParameters["userId"]);
    }

    [Fact]
    public void Multisegment_paths_are_split_on_slash()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("a/b/c", typeof(A)),
        ]);

        Assert.True(RouteMatcher.TryMatch("/a/b/c", tree, out var matched));
        Assert.Single(matched);
    }

    [Fact]
    public void Does_not_match_if_extra_segments_unconsumed()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users", typeof(A)),
        ]);

        Assert.False(RouteMatcher.TryMatch("/users/42", tree, out _));
    }
}
