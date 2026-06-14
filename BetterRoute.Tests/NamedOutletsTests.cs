using BetterRoute.Routing;
using BetterRoute.Routing.Internal;

namespace BetterRoute.Tests;

public class NamedOutletsTests
{
    private sealed class DefaultPage;
    private sealed class SidebarWidget;
    private sealed class ModalContent;
    private sealed class LayoutComponent;

    [Fact]
    public void RouteDefinition_stores_Components()
    {
        var components = new Dictionary<string, Type>
        {
            ["sidebar"] = typeof(SidebarWidget),
            ["modal"] = typeof(ModalContent),
        };

        var route = new RouteDefinition("users", typeof(DefaultPage),
            Components: components);

        Assert.NotNull(route.Components);
        Assert.Equal(2, route.Components.Count);
        Assert.Equal(typeof(SidebarWidget), route.Components["sidebar"]);
        Assert.Equal(typeof(ModalContent), route.Components["modal"]);
    }

    [Fact]
    public void RouteDefinition_Components_defaults_to_null()
    {
        var route = new RouteDefinition("users", typeof(DefaultPage));
        Assert.Null(route.Components);
    }

    [Fact]
    public void AllComponents_includes_default_component()
    {
        var route = new RouteDefinition("users", typeof(DefaultPage));
        var matched = new MatchedRoute(route, new Dictionary<string, string>());

        var all = matched.AllComponents;

        Assert.Single(all);
        Assert.True(all.ContainsKey(""));
        Assert.Equal(typeof(DefaultPage), all[""]);
    }

    [Fact]
    public void AllComponents_includes_named_components()
    {
        var components = new Dictionary<string, Type>
        {
            ["sidebar"] = typeof(SidebarWidget),
            ["modal"] = typeof(ModalContent),
        };
        var route = new RouteDefinition("users", typeof(DefaultPage),
            Components: components);
        var matched = new MatchedRoute(route, new Dictionary<string, string>());

        var all = matched.AllComponents;

        Assert.Equal(3, all.Count);
        Assert.Equal(typeof(DefaultPage), all[""]);
        Assert.Equal(typeof(SidebarWidget), all["sidebar"]);
        Assert.Equal(typeof(ModalContent), all["modal"]);
    }

    [Fact]
    public void AllComponents_with_only_named_components()
    {
        var components = new Dictionary<string, Type>
        {
            ["sidebar"] = typeof(SidebarWidget),
        };
        var route = new RouteDefinition("users", Component: null,
            Components: components);
        var matched = new MatchedRoute(route, new Dictionary<string, string>());

        var all = matched.AllComponents;

        Assert.Single(all);
        Assert.False(all.ContainsKey(""));
        Assert.Equal(typeof(SidebarWidget), all["sidebar"]);
    }

    [Fact]
    public void AllComponents_empty_when_no_components()
    {
        var route = new RouteDefinition("users", Component: null);
        var matched = new MatchedRoute(route, new Dictionary<string, string>());

        var all = matched.AllComponents;

        Assert.Empty(all);
    }

    [Fact]
    public void AllComponents_is_read_only()
    {
        var route = new RouteDefinition("users", typeof(DefaultPage));
        var matched = new MatchedRoute(route, new Dictionary<string, string>());

        var all = matched.AllComponents;

        Assert.IsAssignableFrom<IReadOnlyDictionary<string, Type>>(all);
        // ReadOnlyDictionary implements IDictionary<,> explicitly but throws on mutation.
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, Type>)all).Add("new", typeof(DefaultPage)));
    }

    [Fact]
    public void AllComponents_keys_are_case_sensitive()
    {
        var components = new Dictionary<string, Type>
        {
            ["sidebar"] = typeof(SidebarWidget),
            ["Sidebar"] = typeof(ModalContent),
        };
        var route = new RouteDefinition("users", typeof(DefaultPage),
            Components: components);
        var matched = new MatchedRoute(route, new Dictionary<string, string>());

        var all = matched.AllComponents;

        Assert.Equal(3, all.Count);
        Assert.Equal(typeof(SidebarWidget), all["sidebar"]);
        Assert.Equal(typeof(ModalContent), all["Sidebar"]);
        Assert.NotEqual(all["sidebar"], all["Sidebar"]);
    }

    [Fact]
    public void AllComponents_uses_ordinal_comparer()
    {
        var components = new Dictionary<string, Type>
        {
            ["sidebar"] = typeof(SidebarWidget),
        };
        var route = new RouteDefinition("users", typeof(DefaultPage),
            Components: components);
        var matched = new MatchedRoute(route, new Dictionary<string, string>());

        var all = matched.AllComponents;

        // Ordinal comparison means different casing is a different key
        Assert.True(all.ContainsKey("sidebar"));
        Assert.False(all.ContainsKey("SIDEBAR"));
        Assert.False(all.ContainsKey("Sidebar"));
    }

    [Fact]
    public void Route_matching_unchanged_with_named_components()
    {
        // Named components should not affect path matching
        var components = new Dictionary<string, Type>
        {
            ["sidebar"] = typeof(SidebarWidget),
        };
        var tree = CompiledRoute.Compile([
            new RouteDefinition("users/:userId", typeof(DefaultPage),
                Components: components, Children: [
                    new RouteDefinition("profile", typeof(ModalContent)),
                ]),
        ]);

        // Match the parent route
        var result = RouteMatcher.TryMatch("/users/42", tree);
        var success = Assert.IsType<MatchResult.Success>(result);
        Assert.Single(success.Matched);
        Assert.Equal(typeof(DefaultPage), success.Matched[0].Definition.Component);
        Assert.Equal("42", success.Matched[0].SegmentParameters["userId"]);
        // Named components are accessible through the matched route's definition
        Assert.NotNull(success.Matched[0].Definition.Components);
        Assert.Equal(typeof(SidebarWidget),
            success.Matched[0].Definition.Components!["sidebar"]);

        // Match a child route
        result = RouteMatcher.TryMatch("/users/42/profile", tree);
        success = Assert.IsType<MatchResult.Success>(result);
        Assert.Equal(2, success.Matched.Count);
        Assert.Equal(typeof(ModalContent), success.Matched[1].Definition.Component);
    }

    [Fact]
    public void Layout_only_route_with_named_components()
    {
        // A route with no default Component but with named components and children
        var components = new Dictionary<string, Type>
        {
            ["sidebar"] = typeof(SidebarWidget),
        };
        var tree = CompiledRoute.Compile([
            new RouteDefinition("dashboard", typeof(LayoutComponent), Children: [
                new RouteDefinition("widgets", Component: null,
                    Components: components),
            ]),
        ]);

        var result = RouteMatcher.TryMatch("/dashboard/widgets", tree);
        var success = Assert.IsType<MatchResult.Success>(result);
        Assert.Equal(2, success.Matched.Count);
        // The leaf route has no default component but has named components
        Assert.Null(success.Matched[1].Definition.Component);
        Assert.NotNull(success.Matched[1].Definition.Components);
        Assert.Equal(typeof(SidebarWidget),
            success.Matched[1].Definition.Components!["sidebar"]);
    }

    [Fact]
    public void AllComponents_caches_result()
    {
        var route = new RouteDefinition("users", typeof(DefaultPage));
        var matched = new MatchedRoute(route, new Dictionary<string, string>());

        var first = matched.AllComponents;
        var second = matched.AllComponents;

        Assert.Same(first, second);
    }
}
