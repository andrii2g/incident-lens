namespace A2G.IncidentLens.Core.Models;

public sealed class EvidenceItem
{
    public DateTimeOffset Timestamp { get; init; }
    public string Source { get; init; } = "unknown";
    public string Kind { get; init; } = "event";
    public string Severity { get; init; } = "info";
    public string Title { get; init; } = "Untitled evidence";
    public string? Summary { get; init; }
    public string? Service { get; init; }
    public string? Environment { get; init; }
    public string? Host { get; init; }
    public Dictionary<string, string> Labels { get; init; } = new();
    public string? Link { get; init; }
    public double RelevanceScore { get; init; } = 0.5;

    // Optional. Renderers should avoid exposing Raw in AI context unless explicitly allowed.
    public string? Raw { get; init; }
}
