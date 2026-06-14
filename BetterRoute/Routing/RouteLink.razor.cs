using Microsoft.AspNetCore.Components;

namespace BetterRoute.Routing;

/// <summary>
/// A drop-in replacement for <c>&lt;a&gt;</c> that uses client-side navigation
/// instead of full-page reloads. Supports both path-based links (<see cref="Href"/>)
/// and named-route links (<see cref="Name"/> + <see cref="Params"/>).
/// </summary>
/// <remarks>
/// <para><b>Path-based usage:</b></para>
/// <code>&lt;RouteLink Href="users/42/profile"&gt;Profile&lt;/RouteLink&gt;</code>
/// <para><b>Named-route usage:</b></para>
/// <code>&lt;RouteLink Name="user.post" Params="new { userId = 42, postId = 7 }"&gt;Post 7&lt;/RouteLink&gt;</code>
/// </remarks>
public partial class RouteLink : ComponentBase
{
    /// <summary>
    /// The target path. Use this for simple path-based navigation.
    /// Mutually exclusive with <see cref="Name"/>.
    /// </summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>
    /// The named route to navigate to. Uses <see cref="RouterState.ResolveUrl"/>
    /// to build the href and <see cref="RouterState.NavigateTo(string,IReadOnlyDictionary{string, string}?,bool)"/>
    /// on click. Mutually exclusive with <see cref="Href"/>.
    /// </summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>
    /// Optional parameters for named-route navigation.
    /// Accepts either an <c>IReadOnlyDictionary&lt;string, string&gt;</c> or an
    /// anonymous object (properties are converted via <c>Convert.ToString</c>).
    /// Extra keys not in the route template are appended as query string parameters.
    /// </summary>
    [Parameter] public object? Params { get; set; }

    /// <summary>
    /// Link content (text, markup, etc.).
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Additional attributes splatted onto the <c>&lt;a&gt;</c> element
    /// (e.g., <c>class</c>, <c>style</c>, <c>title</c>, <c>target</c>).
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    [CascadingParameter] private RouterState? State { get; set; }
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private string _resolvedHref = "";
    private IReadOnlyDictionary<string, string>? _effectiveParams;

    protected override void OnParametersSet()
    {
        // Resolve the href value.
        if (Name is not null)
        {
            if (Href is not null)
                throw new InvalidOperationException(
                    "RouteLink cannot have both Href and Name set. Choose one.");

            _effectiveParams = ConvertParams(Params);

            if (State is not null)
            {
                _resolvedHref = State.ResolveUrl(Name, _effectiveParams);
            }
            else
            {
                // Fallback: when no RouterState is available (e.g., outside a
                // BetterRouter), render the route name as the href so the link
                // is still visible. ResolveUrl will work once state cascades.
                _resolvedHref = Name;
            }
        }
        else if (Href is not null)
        {
            _effectiveParams = null;
            _resolvedHref = Href;
        }
        else
        {
            throw new InvalidOperationException(
                "RouteLink requires either Href or Name to be set.");
        }
    }

    private void OnClick()
    {
        if (Name is not null && State is not null)
        {
            State.NavigateTo(Name, _effectiveParams);
        }
        else
        {
            Nav.NavigateTo(_resolvedHref, forceLoad: false);
        }
    }

    private static IReadOnlyDictionary<string, string> ConvertParams(object? parameters)
    {
        if (parameters is null)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        if (parameters is IReadOnlyDictionary<string, string> dict)
            return dict;

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in parameters.GetType().GetProperties())
        {
            var value = prop.GetValue(parameters);
            result[prop.Name] = value is null
                ? string.Empty
                : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }
        return result;
    }
}
