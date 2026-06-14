using System.Collections.ObjectModel;

namespace BetterRoute.Routing;

/// <summary>
/// One level of a successful match: the route node that matched and the parameters
/// its own segments captured (excluding parameters captured by ancestors).
/// </summary>
/// <param name="Definition">The <see cref="RouteDefinition"/> that matched at this tree depth.</param>
/// <param name="SegmentParameters">
/// Parameters captured from this route node's own <c>:param</c> segments.
/// Does not include parameters captured by ancestor or descendant routes.
/// </param>
public sealed record MatchedRoute(
    RouteDefinition Definition,
    IReadOnlyDictionary<string, string> SegmentParameters)
{
    private IReadOnlyDictionary<string, Type>? _allComponents;

    /// <summary>
    /// All components available at this matched level, keyed by outlet name.
    /// The default outlet component is keyed under <c>""</c> (empty string).
    /// Named outlets from <see cref="RouteDefinition.Components"/> are keyed
    /// by their name. Returns an empty dictionary when no components are defined.
    /// </summary>
    public IReadOnlyDictionary<string, Type> AllComponents
    {
        get
        {
            if (_allComponents is not null)
                return _allComponents;

            var dict = new Dictionary<string, Type>(StringComparer.Ordinal);

            if (Definition.Component is not null)
                dict[""] = Definition.Component;

            if (Definition.Components is { Count: > 0 } components)
            {
                foreach (var (key, value) in components)
                    dict[key] = value;
            }

            _allComponents = new ReadOnlyDictionary<string, Type>(dict);
            return _allComponents;
        }
    }
}
