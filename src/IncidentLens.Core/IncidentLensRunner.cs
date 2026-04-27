using IncidentLens.Core.Connectors;
using IncidentLens.Core.Models;

namespace IncidentLens.Core;

public sealed class IncidentLensRunner
{
    private readonly IncidentLensConfig _config;
    private readonly HttpClient _httpClient;

    public IncidentLensRunner(IncidentLensConfig config, HttpClient httpClient)
    {
        _config = config;
        _httpClient = httpClient;
    }

    public async Task<IncidentLensRunResult> RunAsync(IncidentRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var collectors = new IEvidenceCollector[]
        {
            new ElasticsearchCollector(_config.Elasticsearch, _httpClient),
            new PrometheusCollector(_config.Prometheus, _httpClient)
        };

        var evidence = new List<EvidenceItem>();
        foreach (var collector in collectors)
        {
            var collected = await collector.CollectAsync(request, cancellationToken);
            evidence.AddRange(collected);
        }

        return new IncidentLensRunResult
        {
            Request = request,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Evidence = evidence
                .OrderBy(x => x.Timestamp)
                .ThenByDescending(x => x.RelevanceScore)
                .ToList()
        };
    }

    private static void ValidateRequest(IncidentRequest request)
    {
        if (request.FromUtc == default)
        {
            throw new ArgumentException("FromUtc is required.");
        }

        if (request.ToUtc == default)
        {
            throw new ArgumentException("ToUtc is required.");
        }

        if (request.ToUtc <= request.FromUtc)
        {
            throw new ArgumentException("ToUtc must be greater than FromUtc.");
        }
    }
}
