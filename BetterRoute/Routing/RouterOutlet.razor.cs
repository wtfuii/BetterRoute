using Microsoft.AspNetCore.Components;

namespace BetterRoute.Routing;

public partial class RouterOutlet : ComponentBase
{
    /// <summary>
    /// Optional outlet name. When <c>null</c> (default), renders the default
    /// <see cref="RouteDefinition.Component"/> at the next depth. When set
    /// (e.g. <c>"sidebar"</c>), renders the named component from
    /// <see cref="RouteDefinition.Components"/> at the same depth.
    /// </summary>
    [Parameter] public string? Name { get; set; }

    [CascadingParameter] public RouterState? State { get; set; }

    [CascadingParameter] public GuardRegistrar? GuardRegistrar { get; set; }

    private bool HasChild
    {
        get
        {
            if (State is null || State.CurrentDepth + 1 >= State.Matched.Count)
                return false;

            var next = State.Matched[State.CurrentDepth + 1];

            if (Name is null)
                return next.Definition.Component is not null;

            return next.Definition.Components is not null
                && next.Definition.Components.ContainsKey(Name);
        }
    }

    /// <summary>
    /// For the default outlet, advances depth so the child sees itself
    /// at <see cref="RouterState.CurrentDepth"/>. For named outlets,
    /// keeps the parent's depth — named outlets are siblings, not nested.
    /// </summary>
    private RouterState ChildState =>
        Name is null
            ? State!.AtDepth(State.CurrentDepth + 1)
            : State!.AtDepth(State.CurrentDepth);

    private Type ChildComponent
    {
        get
        {
            var next = State!.Matched[State.CurrentDepth + 1];

            if (Name is null)
                return next.Definition.Component!;

            return next.Definition.Components![Name!];
        }
    }
}
