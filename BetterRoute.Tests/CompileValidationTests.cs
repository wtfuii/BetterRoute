using BetterRoute.Routing;
using BetterRoute.Routing.Internal;

namespace BetterRoute.Tests;

public class CompileValidationTests
{
    private sealed class A;

    [Fact]
    public void RedirectTo_and_RedirectToFactory_both_set_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CompiledRoute.Compile([
                new RouteDefinition("x", RedirectTo: "/y", RedirectToFactory: _ => "/z"),
            ]));
        Assert.Contains("RedirectTo", ex.Message);
        Assert.Contains("RedirectToFactory", ex.Message);
    }

    [Fact]
    public void RedirectTo_and_Component_both_set_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CompiledRoute.Compile([
                new RouteDefinition("x", typeof(A), RedirectTo: "/y"),
            ]));
        Assert.Contains("redirect", ex.Message);
        Assert.Contains("Component", ex.Message);
    }

    [Fact]
    public void RedirectToFactory_and_Component_both_set_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CompiledRoute.Compile([
                new RouteDefinition("x", typeof(A), RedirectToFactory: _ => "/y"),
            ]));
        Assert.Contains("redirect", ex.Message);
        Assert.Contains("Component", ex.Message);
    }

    [Fact]
    public void RedirectTo_and_Aliases_both_set_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CompiledRoute.Compile([
                new RouteDefinition("x", RedirectTo: "/y", Aliases: ["z"]),
            ]));
        Assert.Contains("redirect", ex.Message);
        Assert.Contains("alias", ex.Message.ToLower());
    }

    [Fact]
    public void RedirectToFactory_and_Aliases_both_set_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CompiledRoute.Compile([
                new RouteDefinition("x", RedirectToFactory: _ => "/y", Aliases: ["z"]),
            ]));
        Assert.Contains("redirect", ex.Message);
        Assert.Contains("alias", ex.Message.ToLower());
    }

    [Fact]
    public void Leaf_route_with_no_content_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CompiledRoute.Compile([
                new RouteDefinition("orphan"),
            ]));
        Assert.Contains("Component", ex.Message);
    }

    [Fact]
    public void Unbound_parameter_in_RedirectTo_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CompiledRoute.Compile([
                new RouteDefinition("legacy", RedirectTo: "/users/:userId"),
            ]));
        Assert.Contains(":userId", ex.Message);
    }

    [Fact]
    public void Bound_parameter_in_RedirectTo_compiles()
    {
        // :userId is declared in the route's own path, so the redirect is valid.
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users/:userId/legacy", RedirectTo: "/users/:userId"),
        ]);
        Assert.NotEmpty(tree);
    }

    [Fact]
    public void Ancestor_parameter_in_RedirectTo_compiles()
    {
        // :userId is captured by the parent; child redirect can reference it.
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users/:userId", typeof(A), Children: [
                new RouteDefinition("legacy", RedirectTo: "/profile/:userId"),
            ]),
        ]);
        Assert.NotEmpty(tree);
    }

    [Fact]
    public void Valid_alias_route_compiles()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("docs", typeof(A), Aliases: ["help", "support"]),
        ]);
        // Should produce 3 entries: canonical + 2 aliases.
        Assert.Equal(3, tree.Count);
        Assert.Same(tree[0].Definition, tree[1].Definition);
        Assert.Same(tree[0].Definition, tree[2].Definition);
    }

    [Fact]
    public void Valid_redirect_route_compiles()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("old", RedirectTo: "/new"),
        ]);
        Assert.Single(tree);
    }

    [Fact]
    public void Parent_without_Component_but_with_children_compiles()
    {
        // A layout-only parent is valid.
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users", Children: [
                new RouteDefinition(":id", typeof(A)),
            ]),
        ]);
        Assert.Single(tree);
        Assert.Single(tree[0].Children);
    }

    [Fact]
    public void Multiple_sibling_redirects_all_compile()
    {
        var tree = CompiledRoute.Compile([
            new RouteDefinition("old-a", RedirectTo: "/new-a"),
            new RouteDefinition("old-b", RedirectTo: "/new-b"),
        ]);
        Assert.Equal(2, tree.Count);
    }
}
