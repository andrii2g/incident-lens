using IncidentLens.Core.Models;

namespace IncidentLens.Core.Connectors;

public interface IEvidenceCollector
{
    Task<IReadOnlyList<EvidenceItem>> CollectAsync(IncidentRequest request, CancellationToken cancellationToken);
}
