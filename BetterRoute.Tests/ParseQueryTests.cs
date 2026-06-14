using BetterRoute.Routing;
using BetterRoute.Routing.Internal;

namespace BetterRoute.Tests;

public class ParseQueryTests
{
    [Fact]
    public void Null_query_returns_empty()
    {
        var result = QueryStringParser.Parse(null);
        Assert.Empty(result);
    }

    [Fact]
    public void Empty_string_query_returns_empty()
    {
        var result = QueryStringParser.Parse("");
        Assert.Empty(result);
    }

    [Fact]
    public void Single_key_value_pair()
    {
        var result = QueryStringParser.Parse("name=John");
        Assert.Single(result);
        Assert.Equal("John", result["name"][0]);
    }

    [Fact]
    public void Multiple_key_value_pairs()
    {
        var result = QueryStringParser.Parse("name=John&age=30");
        Assert.Equal(2, result.Count);
        Assert.Equal("John", result["name"][0]);
        Assert.Equal("30", result["age"][0]);
    }

    [Fact]
    public void Multi_valued_key()
    {
        var result = QueryStringParser.Parse("tag=dotnet&tag=csharp");
        Assert.Single(result);
        Assert.Equal(["dotnet", "csharp"], result["tag"]);
    }

    [Fact]
    public void Bare_key_without_equals()
    {
        var result = QueryStringParser.Parse("active");
        Assert.Single(result);
        Assert.Equal("", result["active"][0]);
    }

    [Fact]
    public void Empty_key_filtered_out()
    {
        var result = QueryStringParser.Parse("=value");
        Assert.Empty(result);
    }

    [Fact]
    public void Empty_bare_key_filtered_out()
    {
        var result = QueryStringParser.Parse("");
        Assert.Empty(result);
    }

    [Fact]
    public void Urldecoded_keys_and_values()
    {
        var result = QueryStringParser.Parse("full%20name=John%20Doe");
        Assert.Equal("John Doe", result["full name"][0]);
    }

    [Fact]
    public void Plus_is_not_decoded_as_space()
    {
        // Per RFC 3986 / URLSearchParams, '+' is literal '+', not space.
        var result = QueryStringParser.Parse("q=hello+world");
        Assert.Equal("hello+world", result["q"][0]);
    }

    [Fact]
    public void Empty_pairs_are_skipped()
    {
        var result = QueryStringParser.Parse("a=1&&b=2");
        Assert.Equal(2, result.Count);
        Assert.Equal("1", result["a"][0]);
        Assert.Equal("2", result["b"][0]);
    }

    [Fact]
    public void Empty_value_after_equals()
    {
        var result = QueryStringParser.Parse("key=");
        Assert.Single(result);
        Assert.Equal("", result["key"][0]);
    }

    [Fact]
    public void RouterState_GetQuery_returns_first_value()
    {
        var state = new RouterState(
            Matched: [],
            CurrentDepth: 0,
            Parameters: new Dictionary<string, string>(),
            Query: new Dictionary<string, IReadOnlyList<string>>
            {
                ["name"] = ["Alice", "Bob"]
            },
            Url: "/test?name=Alice&name=Bob",
            Path: "/test",
            Fragment: null
        );

        Assert.Equal("Alice", state.GetQuery("name"));
    }

    [Fact]
    public void RouterState_GetQuery_returns_null_when_key_missing()
    {
        var state = new RouterState(
            Matched: [],
            CurrentDepth: 0,
            Parameters: new Dictionary<string, string>(),
            Query: new Dictionary<string, IReadOnlyList<string>>(),
            Url: "/test",
            Path: "/test",
            Fragment: null
        );

        Assert.Null(state.GetQuery("missing"));
    }

    [Fact]
    public void RouterState_GetQueryValues_returns_all_values()
    {
        var state = new RouterState(
            Matched: [],
            CurrentDepth: 0,
            Parameters: new Dictionary<string, string>(),
            Query: new Dictionary<string, IReadOnlyList<string>>
            {
                ["tag"] = ["dotnet", "csharp"]
            },
            Url: "/test?tag=dotnet&tag=csharp",
            Path: "/test",
            Fragment: null
        );

        Assert.Equal(["dotnet", "csharp"], state.GetQueryValues("tag"));
    }

    [Fact]
    public void RouterState_GetQueryValues_returns_empty_when_key_missing()
    {
        var state = new RouterState(
            Matched: [],
            CurrentDepth: 0,
            Parameters: new Dictionary<string, string>(),
            Query: new Dictionary<string, IReadOnlyList<string>>(),
            Url: "/test",
            Path: "/test",
            Fragment: null
        );

        Assert.Empty(state.GetQueryValues("missing"));
    }

    [Fact]
    public void Query_keys_are_case_sensitive()
    {
        var result = QueryStringParser.Parse("Tab=A&tab=B");
        Assert.Equal(2, result.Count);
        Assert.Equal("A", result["Tab"][0]);
        Assert.Equal("B", result["tab"][0]);
    }

    [Fact]
    public void RouterState_Fragment_is_accessible()
    {
        var state = new RouterState(
            Matched: [],
            CurrentDepth: 0,
            Parameters: new Dictionary<string, string>(),
            Query: new Dictionary<string, IReadOnlyList<string>>(),
            Url: "/test#section1",
            Path: "/test",
            Fragment: "section1"
        );

        Assert.Equal("section1", state.Fragment);
    }
}
