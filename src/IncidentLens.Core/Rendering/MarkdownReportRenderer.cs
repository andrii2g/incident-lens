using A2G.IncidentLens.Core.Models;
using System.Text;

namespace A2G.IncidentLens.Core.Rendering;

public sealed class MarkdownReportRenderer
{
    public string Render(IncidentLensRunResult result, IncidentLensConfig config)
    {
        var sb = new StringBuilder();
        var request = result.Request;
        var evidence = result.Evidence.OrderBy(x => x.Timestamp).ToList();
        var maxTimelineItems = Math.Clamp(config.Report.MaxTimelineItems, 1, 200);

        sb.AppendLine("# IncidentLens Report");
        sb.AppendLine();
        sb.AppendLine("## Request");
        sb.AppendLine();
        sb.AppendLine($"- **Symptom:** {EscapeMarkdown(TextRedactor.Redact(request.Symptom))}");
        sb.AppendLine($"- **Window:** {request.FromUtc:O} -> {request.ToUtc:O}");
        sb.AppendLine($"- **Service:** {EscapeMarkdown(request.Service ?? "not specified")}");
        sb.AppendLine($"- **Environment:** {EscapeMarkdown(request.Environment ?? "not specified")}");
        sb.AppendLine($"- **Generated:** {result.GeneratedAtUtc:O}");
        sb.AppendLine();

        sb.AppendLine("## Evidence Summary");
        sb.AppendLine();
        if (evidence.Count == 0)
        {
            sb.AppendLine("No evidence was collected for this request. This can mean the incident had no matching logs/metrics, the query pack is too narrow, the time window is wrong, or source access is incomplete.");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"Collected **{evidence.Count}** evidence item(s).");
            sb.AppendLine();
            sb.AppendLine("| Source | Count |");
            sb.AppendLine("|---|---:|");
            foreach (var group in evidence.GroupBy(x => x.Source).OrderByDescending(g => g.Count()))
            {
                sb.AppendLine($"| {EscapeMarkdown(group.Key)} | {group.Count()} |");
            }

            sb.AppendLine();
            sb.AppendLine("| Severity | Count |");
            sb.AppendLine("|---|---:|");
            foreach (var group in evidence.GroupBy(x => x.Severity).OrderByDescending(g => g.Count()))
            {
                sb.AppendLine($"| {EscapeMarkdown(group.Key)} | {group.Count()} |");
            }

            sb.AppendLine();
        }

        sb.AppendLine("## Confirmed Observations");
        sb.AppendLine();
        if (evidence.Count == 0)
        {
            sb.AppendLine("- No confirmed observations. No evidence matched the configured probes.");
        }
        else
        {
            foreach (var item in evidence
                         .OrderByDescending(x => SeverityRank(x.Severity))
                         .ThenByDescending(x => x.RelevanceScore)
                         .Take(12))
            {
                sb.AppendLine($"- **{EscapeMarkdown(item.Severity)}** from **{EscapeMarkdown(item.Source)}** at `{item.Timestamp:O}` - {EscapeMarkdown(TextRedactor.Redact(item.Title))}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Timeline");
        sb.AppendLine();
        if (evidence.Count == 0)
        {
            sb.AppendLine("No timeline is available because no evidence was collected.");
        }
        else
        {
            sb.AppendLine("| Time UTC | Source | Severity | Title |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var item in evidence.Take(maxTimelineItems))
            {
                sb.AppendLine($"| `{item.Timestamp:O}` | {EscapeTable(item.Source)} | {EscapeTable(item.Severity)} | {EscapeTable(TextRedactor.Redact(item.Title))} |");
            }

            if (evidence.Count > maxTimelineItems)
            {
                sb.AppendLine($"| ... | ... | ... | {evidence.Count - maxTimelineItems} more item(s) omitted by report limit | ");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Missing Data / Limits");
        sb.AppendLine();
        sb.AppendLine("- This report only includes configured Elasticsearch indexes/data streams and configured Prometheus queries.");
        sb.AppendLine("- It does not inspect Grafana dashboards, Alertmanager alerts, Kubernetes events, traces, deployments, feature flags, or cloud provider events.");
        sb.AppendLine("- A metric evidence item means the configured threshold was reached; it does not prove causality.");
        sb.AppendLine("- A log evidence item means a matching document was found; it may still be unrelated noise.");
        sb.AppendLine();

        sb.AppendLine("## Recommended Next Checks");
        sb.AppendLine();
        if (evidence.Count == 0)
        {
            sb.AppendLine("- Widen the time range by 15-30 minutes on both sides.");
            sb.AppendLine("- Remove or loosen the symptom text filter.");
            sb.AppendLine("- Confirm the Elasticsearch index/data stream names and timestamp field.");
            sb.AppendLine("- Confirm Prometheus query labels match the target service/environment.");
        }
        else
        {
            sb.AppendLine("- Review the highest-severity observations first.");
            sb.AppendLine("- Compare metric timestamps with nearby log entries.");
            sb.AppendLine("- Check whether the same pattern appears in a wider time window.");
            sb.AppendLine("- Paste `ai-context.md` into an approved AI assistant for a second-pass summary and hypothesis list.");
        }

        if (config.Report.IncludeRaw)
        {
            sb.AppendLine();
            sb.AppendLine("## Raw Evidence Snippets");
            sb.AppendLine();
            foreach (var item in evidence.Where(x => !string.IsNullOrWhiteSpace(x.Raw)).Take(maxTimelineItems))
            {
                sb.AppendLine($"### {EscapeMarkdown(item.Source)} `{item.Timestamp:O}`");
                sb.AppendLine();
                sb.AppendLine("```json");
                sb.AppendLine(item.Raw);
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static int SeverityRank(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" => 5,
            "error" => 4,
            "warning" => 3,
            "info" => 2,
            "debug" => 1,
            _ => 0
        };
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("*", "\\*").Replace("_", "\\_");
    }

    private static string EscapeTable(string value)
    {
        return EscapeMarkdown(value).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }
}
