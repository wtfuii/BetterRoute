using BetterRoute.Routing;

namespace BetterRoute.Tests;

public class GuardResultTests
{
    [Fact]
    public void Ok_is_Continue()
    {
        Assert.IsType<GuardResult.Continue>(GuardResult.Ok);
    }

    [Fact]
    public void Stop_is_Cancel()
    {
        Assert.IsType<GuardResult.Cancel>(GuardResult.Stop);
    }

    [Fact]
    public void To_creates_Redirect_with_correct_target()
    {
        var result = GuardResult.To("/login");
        var redirect = Assert.IsType<GuardResult.Redirect>(result);
        Assert.Equal("/login", redirect.Target);
    }

    [Fact]
    public void Pattern_match_is_exhaustive_over_subtypes()
    {
        var result = GuardResult.To("/dashboard");

        var text = result switch
        {
            GuardResult.Continue => "continue",
            GuardResult.Cancel => "cancel",
            GuardResult.Redirect r => r.Target,
            _ => "unknown"
        };

        Assert.Equal("/dashboard", text);
    }

    [Fact]
    public void Redirect_instances_with_same_target_are_equal()
    {
        var a = new GuardResult.Redirect("/x");
        var b = new GuardResult.Redirect("/x");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Redirect_instances_with_different_targets_are_not_equal()
    {
        var a = new GuardResult.Redirect("/x");
        var b = new GuardResult.Redirect("/y");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Continue_and_Cancel_are_not_equal()
    {
        Assert.NotEqual<GuardResult>(new GuardResult.Continue(), new GuardResult.Cancel());
    }

    [Fact]
    public void Continue_and_Redirect_are_not_equal()
    {
        Assert.NotEqual<GuardResult>(
            new GuardResult.Continue(),
            new GuardResult.Redirect("/x"));
    }
}
