using BetterRoute.Routing;
using BetterRoute.Routing.Internal;

namespace BetterRoute.Tests;

public class NamedRoutesTests
{
    private sealed class A;
    private sealed class B;
    private sealed class C;
    private sealed class D;

    // ── Name indexing at compile time ──────────────────────────────────

    [Fact]
    public void Single_root_route_with_name_is_indexed()
    {
        CompiledRoute.Compile([
            new RouteDefinition("about", typeof(A), Name: "about"),
        ], out var index);

        Assert.Equal(1, index.Count);
        var entry = index.Get("about");
        Assert.Equal("about", entry.FullPathTemplate);
        Assert.Equal(typeof(A), entry.Definition.Component);
    }

    [Fact]
    public void Deeply_nested_route_name_has_full_ancestor_path()
    {
        CompiledRoute.Compile([
            new RouteDefinition("users", typeof(A), Children:
            [
                new RouteDefinition(":userId", typeof(B), Children:
                [
                    new RouteDefinition("posts/:postId", typeof(C), Name: "user.post"),
                ]),
            ]),
        ], out var index);

        Assert.Equal(1, index.Count);
        var entry = index.Get("user.post");
        Assert.Equal("users/:userId/posts/:postId", entry.FullPathTemplate);
    }

    [Fact]
    public void Name_on_parent_containing_path_is_indexed()
    {
        CompiledRoute.Compile([
            new RouteDefinition("users", typeof(A), Children:
            [
                new RouteDefinition(":userId", typeof(B), Name: "user"),
            ]),
        ], out var index);

        var entry = index.Get("user");
        Assert.Equal("users/:userId", entry.FullPathTemplate);
    }

    [Fact]
    public void Name_on_root_empty_path_route_indexed_with_empty_string()
    {
        CompiledRoute.Compile([
            new RouteDefinition("", typeof(A), Name: "home"),
        ], out var index);

        var entry = index.Get("home");
        Assert.Equal("", entry.FullPathTemplate);
    }

    [Fact]
    public void Duplicate_name_throws_with_both_locations()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CompiledRoute.Compile([
                new RouteDefinition("users", typeof(A), Name: "list"),
                new RouteDefinition("teams", typeof(B), Name: "list"),
            ], out _));

        Assert.Contains("\"list\"", ex.Message);
        Assert.Contains("users", ex.Message);
        Assert.Contains("teams", ex.Message);
    }

    [Fact]
    public void Routes_without_names_are_not_indexed()
    {
        CompiledRoute.Compile([
            new RouteDefinition("users", typeof(A), Children:
            [
                new RouteDefinition(":userId", typeof(B)),
            ]),
        ], out var index);

        Assert.Equal(0, index.Count);
    }

    [Fact]
    public void Alias_route_name_registered_on_canonical_path_only()
    {
        CompiledRoute.Compile([
            new RouteDefinition("", typeof(A), Name: "home", Aliases: ["index"]),
        ], out var index);

        Assert.Equal(1, index.Count);
        var entry = index.Get("home");
        Assert.Equal("", entry.FullPathTemplate);
    }

    // ── URL building (ResolveUrl) ──────────────────────────────────────

    [Fact]
    public void Resolves_literal_route_to_path()
    {
        CompiledRoute.Compile([
            new RouteDefinition("about", typeof(A), Name: "about"),
        ], out var index);

        var state = CreateState(index);
        var url = state.ResolveUrl("about");
        Assert.Equal("/about", url);
    }

    [Fact]
    public void Substitutes_single_parameter_into_path_segment()
    {
        CompiledRoute.Compile([
            new RouteDefinition(":userId", typeof(A), Name: "user"),
        ], out var index);

        var state = CreateState(index);
        var url = state.ResolveUrl("user", new Dictionary<string, string>
        {
            ["userId"] = "42",
        });
        Assert.Equal("/42", url);
    }

    [Fact]
    public void Substitutes_multiple_parameters()
    {
        CompiledRoute.Compile([
            new RouteDefinition("users", typeof(A), Children:
            [
                new RouteDefinition(":userId", typeof(B), Children:
                [
                    new RouteDefinition("posts/:postId", typeof(C), Name: "user.post"),
                ]),
            ]),
        ], out var index);

        var state = CreateState(index);
        var url = state.ResolveUrl("user.post", new Dictionary<string, string>
        {
            ["userId"] = "42",
            ["postId"] = "7",
        });
        Assert.Equal("/users/42/posts/7", url);
    }

    [Fact]
    public void Missing_required_parameter_throws_with_message()
    {
        CompiledRoute.Compile([
            new RouteDefinition(":userId", typeof(A), Name: "user"),
        ], out var index);

        var state = CreateState(index);
        var ex = Assert.Throws<InvalidOperationException>(
            () => state.ResolveUrl("user", new Dictionary<string, string>()));

        Assert.Contains(":userId", ex.Message);
        Assert.Contains("user", ex.Message);
    }

    [Fact]
    public void Extra_parameters_appended_as_query_string()
    {
        CompiledRoute.Compile([
            new RouteDefinition("search", typeof(A), Name: "search"),
        ], out var index);

        var state = CreateState(index);
        var url = state.ResolveUrl("search", new Dictionary<string, string>
        {
            ["q"] = "hello world",
            ["sort"] = "asc",
        });
        Assert.Equal("/search?q=hello%20world&sort=asc", url);
    }

    [Fact]
    public void Parameter_values_are_url_escaped()
    {
        CompiledRoute.Compile([
            new RouteDefinition("users/:userName", typeof(A), Name: "user"),
        ], out var index);

        var state = CreateState(index);
        var url = state.ResolveUrl("user", new Dictionary<string, string>
        {
            ["userName"] = "hello world",
        });
        Assert.Equal("/users/hello%20world", url);
    }

    [Fact]
    public void Empty_parameters_dictionary_resolves_literals_fine()
    {
        CompiledRoute.Compile([
            new RouteDefinition("about", typeof(A), Name: "about"),
        ], out var index);

        var state = CreateState(index);
        var url = state.ResolveUrl("about", null);
        Assert.Equal("/about", url);
    }

    [Fact]
    public void Empty_root_path_named_route_resolves_to_slash()
    {
        CompiledRoute.Compile([
            new RouteDefinition("", typeof(A), Name: "root"),
        ], out var index);

        var state = CreateState(index);
        var url = state.ResolveUrl("root");
        Assert.Equal("/", url);
    }

    [Fact]
    public void Unknown_route_name_throws()
    {
        CompiledRoute.Compile([
            new RouteDefinition("about", typeof(A), Name: "about"),
        ], out var index);

        var state = CreateState(index);
        var ex = Assert.Throws<InvalidOperationException>(
            () => state.ResolveUrl("nonexistent"));

        Assert.Contains("nonexistent", ex.Message);
    }

    [Fact]
    public void ResolveUrl_works_without_named_routes_index()
    {
        var state = new RouterState(
            Matched: [],
            CurrentDepth: 0,
            Parameters: new Dictionary<string, string>(),
            Query: new Dictionary<string, IReadOnlyList<string>>(),
            Url: "",
            Path: "/",
            Fragment: null);

        var ex = Assert.Throws<InvalidOperationException>(
            () => state.ResolveUrl("anything"));
        Assert.Contains("anything", ex.Message);
    }

    // ── Anonymous object overload ─────────────────────────────────────

    [Fact]
    public void Anonymous_object_properties_become_string_parameters()
    {
        CompiledRoute.Compile([
            new RouteDefinition("users/:userId/posts/:postId", typeof(A), Name: "user.post"),
        ], out var index);

        var state = CreateState(index);
        var url = state.ResolveUrl("user.post",
            ToDict(new { userId = "42", postId = "7" }));
        Assert.Equal("/users/42/posts/7", url);
    }

    [Fact]
    public void Integer_properties_converted_via_Convert_ToString()
    {
        CompiledRoute.Compile([
            new RouteDefinition(":userId", typeof(A), Name: "user"),
        ], out var index);

        var state = CreateState(index);
        var url = state.ResolveUrl("user", ToDict(new { userId = 42 }));
        Assert.Equal("/42", url);
    }

    [Fact]
    public void Null_anonymous_object_treated_as_empty_params()
    {
        CompiledRoute.Compile([
            new RouteDefinition("about", typeof(A), Name: "about"),
        ], out var index);

        var state = CreateState(index);
        var url = state.ResolveUrl("about", ToDict(null));
        Assert.Equal("/about", url);
    }

    [Fact]
    public void Null_property_value_in_anonymous_object_becomes_empty_string()
    {
        CompiledRoute.Compile([
            new RouteDefinition("items", typeof(A), Name: "items"),
        ], out var index);

        var state = CreateState(index);
        var url = state.ResolveUrl("items", ToDict(new { tag = (string?)null }));
        Assert.Equal("/items?tag=", url);
    }

    // ── RouterState.NavigateTo ────────────────────────────────────────

    [Fact]
    public void NavigateTo_resolves_and_calls_callback()
    {
        CompiledRoute.Compile([
            new RouteDefinition("about", typeof(A), Name: "about"),
        ], out var index);

        string? navigatedUrl = null;
        bool? navigatedReplace = null;
        var state = new RouterState(
            Matched: [],
            CurrentDepth: 0,
            Parameters: new Dictionary<string, string>(),
            Query: new Dictionary<string, IReadOnlyList<string>>(),
            Url: "",
            Path: "/",
            Fragment: null)
        {
            NamedRoutes = index,
            NavigateCallback = (url, replace) =>
            {
                navigatedUrl = url;
                navigatedReplace = replace;
            },
        };

        state.NavigateTo("about");
        Assert.Equal("/about", navigatedUrl);
        Assert.False(navigatedReplace);
    }

    [Fact]
    public void NavigateTo_with_replace_passes_through()
    {
        CompiledRoute.Compile([
            new RouteDefinition("about", typeof(A), Name: "about"),
        ], out var index);

        bool? navigatedReplace = null;
        var state = new RouterState(
            Matched: [],
            CurrentDepth: 0,
            Parameters: new Dictionary<string, string>(),
            Query: new Dictionary<string, IReadOnlyList<string>>(),
            Url: "",
            Path: "/",
            Fragment: null)
        {
            NamedRoutes = index,
            NavigateCallback = (url, replace) => navigatedReplace = replace,
        };

        state.NavigateTo("about", replace: true);
        Assert.True(navigatedReplace);
    }

    [Fact]
    public void NavigateTo_without_callback_throws()
    {
        CompiledRoute.Compile([
            new RouteDefinition("about", typeof(A), Name: "about"),
        ], out var index);

        var state = new RouterState(
            Matched: [],
            CurrentDepth: 0,
            Parameters: new Dictionary<string, string>(),
            Query: new Dictionary<string, IReadOnlyList<string>>(),
            Url: "",
            Path: "/",
            Fragment: null)
        {
            NamedRoutes = index,
            // NavigateCallback not set
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => state.NavigateTo("about"));
        Assert.Contains("navigation callback", ex.Message);
    }

    [Fact]
    public void State_NavigateTo_reuses_current_parameters()
    {
        CompiledRoute.Compile([
            new RouteDefinition("users/:userId/profile", typeof(A), Name: "user.profile"),
        ], out var index);

        string? navigatedUrl = null;
        var state = new RouterState(
            Matched: [],
            CurrentDepth: 0,
            Parameters: new Dictionary<string, string> { ["userId"] = "42" },
            Query: new Dictionary<string, IReadOnlyList<string>>(),
            Url: "",
            Path: "/users/42",
            Fragment: null)
        {
            NamedRoutes = index,
            NavigateCallback = (url, _) => navigatedUrl = url,
        };

        // Call with no explicit parameters — should use current Parameters.
        state.NavigateTo("user.profile");
        Assert.Equal("/users/42/profile", navigatedUrl);
    }

    [Fact]
    public void Overrides_win_over_state_parameters()
    {
        CompiledRoute.Compile([
            new RouteDefinition("users/:userId/profile", typeof(A), Name: "user.profile"),
        ], out var index);

        string? navigatedUrl = null;
        var state = new RouterState(
            Matched: [],
            CurrentDepth: 0,
            Parameters: new Dictionary<string, string> { ["userId"] = "42" },
            Query: new Dictionary<string, IReadOnlyList<string>>(),
            Url: "",
            Path: "/users/42",
            Fragment: null)
        {
            NamedRoutes = index,
            NavigateCallback = (url, _) => navigatedUrl = url,
        };

        state.NavigateTo("user.profile", new Dictionary<string, string>
        {
            ["userId"] = "99",
        });
        Assert.Equal("/users/99/profile", navigatedUrl);
    }

    [Fact]
    public void NavigateTo_with_anonymous_object_uses_it()
    {
        CompiledRoute.Compile([
            new RouteDefinition("users/:userId/posts/:postId", typeof(A), Name: "user.post"),
        ], out var index);

        string? navigatedUrl = null;
        var state = new RouterState(
            Matched: [],
            CurrentDepth: 0,
            Parameters: new Dictionary<string, string>(),
            Query: new Dictionary<string, IReadOnlyList<string>>(),
            Url: "",
            Path: "/",
            Fragment: null)
        {
            NamedRoutes = index,
            NavigateCallback = (url, _) => navigatedUrl = url,
        };

        state.NavigateTo("user.post", new { userId = "42", postId = "7" });
        Assert.Equal("/users/42/posts/7", navigatedUrl);
    }

    // ── Named route with redirect ─────────────────────────────────────

    [Fact]
    public void Named_redirect_route_resolves_to_its_own_path_not_target()
    {
        CompiledRoute.Compile([
            new RouteDefinition("old-profile", RedirectTo: "/users/42/profile", Name: "legacy"),
        ], out var index);

        var state = CreateState(index);
        var url = state.ResolveUrl("legacy");
        // Resolves to the redirect route's own path, not the target.
        Assert.Equal("/old-profile", url);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a RouterState with the given index and a no-op callback,
    /// suitable for testing ResolveUrl.
    /// </summary>
    private static RouterState CreateState(NamedRouteIndex index)
    {
        return new RouterState(
            Matched: [],
            CurrentDepth: 0,
            Parameters: new Dictionary<string, string>(),
            Query: new Dictionary<string, IReadOnlyList<string>>(),
            Url: "",
            Path: "/",
            Fragment: null)
        {
            NamedRoutes = index,
        };
    }

    /// <summary>
    /// Converts an anonymous object to a dictionary so we can test
    /// ResolveUrl directly (which takes IReadOnlyDictionary).
    /// Uses the same Convert.ToString logic as the NavigateTo overload.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ToDict(object? parameters)
    {
        if (parameters is null)
            return new Dictionary<string, string>();

        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in parameters.GetType().GetProperties())
        {
            var value = prop.GetValue(parameters);
            dict[prop.Name] = value is null
                ? string.Empty
                : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }
        return dict;
    }
}
