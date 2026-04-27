using A2G.IncidentLens.Core.Connectors;
using A2G.IncidentLens.Core.Models;
using Serilog;

namespace A2G.IncidentLens.Core;

public sealed class IncidentLensRunner
{
    private readonly IncidentLensConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public IncidentLensRunner(IncidentLensConfig config, HttpClient httpClient, ILogger logger)
    {
        _config = config;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IncidentLensRunResult> RunAsync(IncidentRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        _logger.Information(
            "Runner started for window {FromUtc} -> {ToUtc} with symptom {Symptom}",
            request.FromUtc,
            request.ToUtc,
            string.IsNullOrWhiteSpace(request.Symptom) ? "<none>" : request.Symptom);

        var collectors = new IEvidenceCollector[]
        {
            new ElasticsearchCollector(_config.Elasticsearch, _httpClient, _logger.ForContext<ElasticsearchCollector>()),
            new PrometheusCollector(_config.Prometheus, _httpClient, _logger.ForContext<PrometheusCollector>())
        };

        var evidence = new List<EvidenceItem>();
        foreach (var collector in collectors)
        {
            _logger.Information("Running collector {CollectorName}", collector.GetType().Name);
            var collected = await collector.CollectAsync(request, cancellationToken);
            evidence.AddRange(collected);
            _logger.Information(
                "Collector {CollectorName} produced {EvidenceCount} evidence item(s)",
                collector.GetType().Name,
                collected.Count);
        }

        var orderedEvidence = evidence
            .OrderBy(x => x.Timestamp)
            .ThenByDescending(x => x.RelevanceScore)
            .ToList();

        _logger.Information(
            "Runner completed with {EvidenceCount} total evidence item(s) including {CollectorErrorCount} collector error(s)",
            orderedEvidence.Count,
            orderedEvidence.Count(x => x.Kind == "collector-error"));

        return new IncidentLensRunResult
        {
            Request = request,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Evidence = orderedEvidence
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
