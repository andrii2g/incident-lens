using System.Text;
using IncidentLens.Core.Models;

namespace IncidentLens.Core.Rendering;

public sealed class AiContextRenderer
{
    public string Render(IncidentLensRunResult result, IncidentLensConfig config)
    {
        var evidence = result.Evidence.OrderBy(x => x.Timestamp).Take(Math.Clamp(config.Report.MaxTimelineItems, 1, 200)).ToList();
        var request = result.Request;
        var sb = new StringBuilder();

        sb.AppendLine("# IncidentLens AI Context");
        sb.AppendLine();
        sb.AppendLine("You are analyzing sanitized evidence collected by IncidentLens. Do not assume facts that are not supported by the evidence. If evidence is missing, say what is missing and propose the next checks.");
        sb.AppendLine();
        sb.AppendLine("## Investigation Request");
        sb.AppendLine();
        sb.AppendLine($"- Symptom: {TextRedactor.Redact(request.Symptom)}");
        sb.AppendLine($"- From UTC: {request.FromUtc:O}");
        sb.AppendLine($"- To UTC: {request.ToUtc:O}");
        sb.AppendLine($"- Service: {request.Service ?? "not specified"}");
        sb.AppendLine($"- Environment: {request.Environment ?? "not specified"}");
        sb.AppendLine();
        sb.AppendLine("## Expected AI Output");
        sb.AppendLine();
        sb.AppendLine("Please return:");
        sb.AppendLine();
        sb.AppendLine("1. Executive summary in 3–5 bullets.");
        sb.AppendLine("2. Timeline of important events.");
        sb.AppendLine("3. Evidence-backed hypotheses only.");
        sb.AppendLine("4. Missing evidence / blind spots.");
        sb.AppendLine("5. Recommended next checks.");
        sb.AppendLine();
        sb.AppendLine("## Evidence");
        sb.AppendLine();

        if (evidence.Count == 0)
        {
            sb.AppendLine("No evidence items were collected.");
            return sb.ToString();
        }

        foreach (var item in evidence)
        {
            sb.AppendLine($"### {item.Timestamp:O} — {item.Source} / {item.Kind} / {item.Severity}");
            sb.AppendLine();
            sb.AppendLine($"- Title: {TextRedactor.Redact(item.Title)}");
            if (!string.IsNullOrWhiteSpace(item.Summary))
            {
                sb.AppendLine($"- Summary: {TextRedactor.Redact(item.Summary)}");
            }
            if (!string.IsNullOrWhiteSpace(item.Service))
            {
                sb.AppendLine($"- Service: {TextRedactor.Redact(item.Service)}");
            }
            if (!string.IsNullOrWhiteSpace(item.Environment))
            {
                sb.AppendLine($"- Environment: {TextRedactor.Redact(item.Environment)}");
            }
            if (!string.IsNullOrWhiteSpace(item.Host))
            {
                sb.AppendLine($"- Host: {TextRedactor.Redact(item.Host)}");
            }
            if (item.Labels.Count > 0)
            {
                sb.AppendLine("- Labels:");
                foreach (var label in TextRedactor.RedactLabels(item.Labels).OrderBy(x => x.Key))
                {
                    sb.AppendLine($"  - {label.Key}: {label.Value}");
                }
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
