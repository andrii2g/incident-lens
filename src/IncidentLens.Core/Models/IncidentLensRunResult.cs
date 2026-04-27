namespace IncidentLens.Core.Models;

public sealed class IncidentLensRunResult
{
    public IncidentRequest Request { get; init; } = new();
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<EvidenceItem> Evidence { get; init; } = [];
}
