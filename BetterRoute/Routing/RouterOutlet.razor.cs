using Microsoft.AspNetCore.Components;

namespace BetterRoute.Routing;

public partial class RouterOutlet : ComponentBase
{
    [CascadingParameter] public RouterState? State { get; set; }

    [CascadingParameter] public GuardRegistrar? GuardRegistrar { get; set; }

    private bool HasChild => State is not null
        && State.CurrentDepth + 1 < State.Matched.Count
        && State.Matched[State.CurrentDepth + 1].Definition.Component is not null;

    private RouterState ChildState => State!.AtDepth(State.CurrentDepth + 1);

    private Type ChildComponent => ChildState.Current.Definition.Component!;
}
