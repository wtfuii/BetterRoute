namespace BetterRoute.Routing.Internal;

/// <summary>
/// Internal control-flow result for the guard pipeline.
/// Mirrors <see cref="GuardResult"/> but is separate from the public API.
/// </summary>
internal abstract record GuardPipelineResult
{
    public sealed record Continue : GuardPipelineResult;
    public sealed record Cancel : GuardPipelineResult;
    public sealed record Redirect(string Target) : GuardPipelineResult;

    private GuardPipelineResult() { }
}
