using System.Text;
using A2G.IncidentLens.Core.Models;

namespace A2G.IncidentLens.Core.Rendering;

public sealed class MermaidTimelineRenderer
{
    public string Render(IncidentLensRunResult result, IncidentLensConfig config)
    {
        var evidence = result.Evidence.OrderBy(x => x.Timestamp).Take(Math.Clamp(config.Report.MaxTimelineItems, 1, 200)).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");

        if (evidence.Count == 0)
        {
            sb.AppendLine("    e0[\"No evidence collected for this request\"]");
            return sb.ToString();
        }

        for (var i = 0; i < evidence.Count; i++)
        {
            var item = evidence[i];
            var label = $"{item.Timestamp:HH:mm:ss} UTC<br/>{item.Source} / {item.Severity}<br/>{TextRedactor.Redact(item.Title)}";
            sb.AppendLine($"    e{i}[\"{EscapeMermaid(label)}\"]");
        }

        for (var i = 0; i < evidence.Count - 1; i++)
        {
            sb.AppendLine($"    e{i} --> e{i + 1}");
        }

        return sb.ToString();
    }

    private static string EscapeMermaid(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "'", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }
}
