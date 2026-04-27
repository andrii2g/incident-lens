using System.Text.RegularExpressions;

namespace A2G.IncidentLens.Core.Rendering;

internal static partial class TextRedactor
{
    private static readonly string[] SensitiveKeyHints =
    [
        "password",
        "passwd",
        "secret",
        "token",
        "apikey",
        "api_key",
        "authorization",
        "cookie",
        "session"
    ];

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var redacted = EmailRegex().Replace(value, "[email]");
        redacted = BearerRegex().Replace(redacted, "Bearer [token]");
        redacted = ApiKeyLikeRegex().Replace(redacted, "$1=[redacted]");
        return redacted;
    }

    public static Dictionary<string, string> RedactLabels(Dictionary<string, string> labels)
    {
        var result = new Dictionary<string, string>();
        foreach (var pair in labels)
        {
            var normalizedKey = pair.Key.Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();
            if (SensitiveKeyHints.Any(normalizedKey.Contains))
            {
                result[pair.Key] = "[redacted]";
            }
            else
            {
                result[pair.Key] = Redact(pair.Value);
            }
        }
        return result;
    }

    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"(?i)\b(api[_-]?key|token|secret|password)\s*=\s*[^\s,;]+", RegexOptions.CultureInvariant)]
    private static partial Regex ApiKeyLikeRegex();
}
