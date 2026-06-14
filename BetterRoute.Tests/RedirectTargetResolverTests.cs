using BetterRoute.Routing.Internal;

namespace BetterRoute.Tests;

public class RedirectTargetResolverTests
{
    private static IReadOnlyDictionary<string, string> Params(params (string, string)[] pairs)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in pairs)
            dict[k] = v;
        return dict;
    }

    private static string Resolve(
        string template,
        IReadOnlyDictionary<string, string>? parameters = null,
        string currentPath = "/",
        string? query = null,
        string? fragment = null)
    {
        return RedirectTargetResolver.Resolve(
            template,
            parameters ?? Params(),
            currentPath,
            query,
            fragment);
    }

    // ── Parameter substitution ───────────────────────────────────────

    [Fact]
    public void Substitutes_single_parameter()
    {
        var result = Resolve("/users/:userId", Params(("userId", "42")));
        Assert.Equal("/users/42", result);
    }

    [Fact]
    public void Substitutes_multiple_parameters()
    {
        var result = Resolve("/:org/:repo", Params(("org", "acme"), ("repo", "web")));
        Assert.Equal("/acme/web", result);
    }

    [Fact]
    public void Preserves_parameter_case()
    {
        var result = Resolve("/users/:userId", Params(("userId", "MyUser")));
        Assert.Equal("/users/MyUser", result);
    }

    [Fact]
    public void Unknown_parameter_left_verbatim()
    {
        // Defensive: if validation somehow missed an unbound param, leave it as-is.
        var result = Resolve("/users/:userId", Params());
        Assert.Equal("/users/:userId", result);
    }

    [Fact]
    public void Template_without_parameters_returns_unchanged()
    {
        var result = Resolve("/dashboard", Params(("userId", "42")));
        Assert.Equal("/dashboard", result);
    }

    [Fact]
    public void Empty_parameters_dictionary_does_not_alter_template()
    {
        var result = Resolve("/users/:userId/profile/:section",
            Params(("userId", "42")));
        Assert.Equal("/users/42/profile/:section", result);
    }

    // ── Relative path resolution ─────────────────────────────────────

    [Fact]
    public void Absolute_path_passes_through_unchanged()
    {
        var result = Resolve("/dashboard", currentPath: "/users/42/legacy");
        Assert.Equal("/dashboard", result);
    }

    [Fact]
    public void Relative_dot_dot_resolves_up_one_level()
    {
        var result = Resolve("../profile", currentPath: "/users/42/legacy");
        Assert.Equal("/users/42/profile", result);
    }

    [Fact]
    public void Relative_dot_dot_resolves_up_multiple_levels()
    {
        var result = Resolve("../../top", currentPath: "/a/b/c/d");
        Assert.Equal("/a/b/top", result);
    }

    [Fact]
    public void Relative_dot_slash_resolves_to_same_level()
    {
        var result = Resolve("./sibling", currentPath: "/users/42");
        Assert.Equal("/users/42/sibling", result);
    }

    [Fact]
    public void Bare_segment_appends_to_current_path()
    {
        // Bare segments (without leading ./ or ../) append to the current path.
        // Use "../profile" if you want to replace the last segment.
        var result = Resolve("profile", currentPath: "/users/42/legacy");
        Assert.Equal("/users/42/legacy/profile", result);
    }

    [Fact]
    public void Too_many_dot_dot_does_not_go_above_root()
    {
        var result = Resolve("../../../../top", currentPath: "/a/b");
        Assert.Equal("/top", result);
    }

    // ── Query / fragment preservation ─────────────────────────────────

    [Fact]
    public void Preserves_query_when_target_has_none()
    {
        var result = Resolve("/dashboard", query: "debug=1");
        Assert.Equal("/dashboard?debug=1", result);
    }

    [Fact]
    public void Preserves_fragment_when_target_has_none()
    {
        var result = Resolve("/dashboard", fragment: "section");
        Assert.Equal("/dashboard#section", result);
    }

    [Fact]
    public void Skips_query_when_target_has_one()
    {
        var result = Resolve("/dashboard?from=redirect", query: "debug=1");
        Assert.Equal("/dashboard?from=redirect", result);
    }

    [Fact]
    public void Skips_fragment_when_target_has_one()
    {
        var result = Resolve("/dashboard#top", fragment: "section");
        Assert.Equal("/dashboard#top", result);
    }

    [Fact]
    public void Preserves_both_query_and_fragment()
    {
        var result = Resolve("/dashboard", query: "debug=1", fragment: "top");
        Assert.Equal("/dashboard?debug=1#top", result);
    }

    [Fact]
    public void Preserves_fragment_but_skips_query_when_target_has_query()
    {
        var result = Resolve("/dashboard?from=redirect", query: "debug=1", fragment: "top");
        Assert.Equal("/dashboard?from=redirect#top", result);
    }
}
