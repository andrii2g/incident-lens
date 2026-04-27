using A2G.IncidentLens.Core.Models;

namespace A2G.IncidentLens.Core.Connectors;

public interface IEvidenceCollector
{
    Task<IReadOnlyList<EvidenceItem>> CollectAsync(IncidentRequest request, CancellationToken cancellationToken);
}
