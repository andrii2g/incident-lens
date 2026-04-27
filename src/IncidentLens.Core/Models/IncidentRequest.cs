namespace IncidentLens.Core.Models;

public sealed class IncidentRequest
{
    public string Symptom { get; init; } = string.Empty;
    public DateTimeOffset FromUtc { get; init; }
    public DateTimeOffset ToUtc { get; init; }
    public string? Service { get; init; }
    public string? Environment { get; init; }
}
