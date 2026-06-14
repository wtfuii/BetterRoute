using System.Text;

namespace BetterRoute.Routing.Internal;

/// <summary>
/// Resolves redirect target URLs by substituting captured parameters,
/// resolving relative paths, and preserving query strings / fragments.
/// </summary>
internal static class RedirectTargetResolver
{
    /// <summary>
    /// Resolves a redirect <paramref name="template"/> into a final URL.
    /// </summary>
    /// <param name="template">The redirect target (may contain <c>:param</c> references and relative paths).</param>
    /// <param name="parameters">Captured route parameters.</param>
    /// <param name="currentPath">The URL path that triggered the redirect (used for relative resolution).</param>
    /// <param name="originalQuery">The query string from the original URL, if any.</param>
    /// <param name="originalFragment">The fragment from the original URL, if any.</param>
    public static string Resolve(
        string template,
        IReadOnlyDictionary<string, string> parameters,
        string currentPath,
        string? originalQuery,
        string? originalFragment)
    {
        // Step 1: Replace :name placeholders with captured values.
        var substituted = SubstituteParameters(template, parameters);

        // Step 2: Resolve relative paths against the current path.
        if (!substituted.StartsWith('/'))
            substituted = ResolveRelativePath(currentPath, substituted);

        // Step 3: Append original query/fragment unless the target already has them.
        var hasQuery = substituted.Contains('?');
        var hasFragment = substituted.Contains('#');

        if (!hasQuery && originalQuery is not null)
            substituted += "?" + originalQuery;
        if (!hasFragment && originalFragment is not null)
            substituted += "#" + originalFragment;

        return substituted;
    }

    /// <summary>
    /// Replaces <c>:paramName</c> tokens in <paramref name="template"/> with their
    /// captured values from <paramref name="parameters"/>. Parameter values are used
    /// verbatim (case preserved). Unknown parameters are left as-is.
    /// </summary>
    private static string SubstituteParameters(
        string template,
        IReadOnlyDictionary<string, string> parameters)
    {
        if (parameters.Count == 0 || !template.Contains(':'))
            return template;

        var sb = new StringBuilder(template.Length);
        var i = 0;
        while (i < template.Length)
        {
            if (template[i] == ':' && i + 1 < template.Length && IsParamStartChar(template[i + 1]))
            {
                var j = i + 1;
                while (j < template.Length && IsParamChar(template[j]))
                    j++;
                var paramName = template[(i + 1)..j];
                if (parameters.TryGetValue(paramName, out var value))
                    sb.Append(value);
                else
                    sb.Append(template[i..j]); // Leave unknown params verbatim (defensive).
                i = j;
            }
            else
            {
                sb.Append(template[i]);
                i++;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Resolves a relative path against <paramref name="basePath"/>.
    /// Supports <c>../</c> (up one level), <c>./</c> (current level), and bare segment names.
    /// </summary>
    private static string ResolveRelativePath(string basePath, string relative)
    {
        // Split base path into segments (strip leading /).
        var baseSegments = basePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var baseList = new List<string>(baseSegments);

        // Split relative path into segments.
        var relSegments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var relList = new List<string>(relSegments);

        // Process relative segments.
        var result = new List<string>(baseList);
        for (var i = 0; i < relList.Count; i++)
        {
            var segment = relList[i];
            if (segment == "..")
            {
                if (result.Count > 0)
                    result.RemoveAt(result.Count - 1);
            }
            else if (segment == ".")
            {
                // No-op — current directory.
            }
            else
            {
                result.Add(segment);
            }
        }

        if (result.Count == 0)
            return "/";

        return "/" + string.Join("/", result);
    }

    private static bool IsParamStartChar(char c) => char.IsLetter(c) || c == '_';
    private static bool IsParamChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
