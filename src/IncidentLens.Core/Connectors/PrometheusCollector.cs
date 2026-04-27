using System.Globalization;
using System.Text.Json;
using IncidentLens.Core.Models;

namespace IncidentLens.Core.Connectors;

public sealed class PrometheusCollector : IEvidenceCollector
{
    private readonly PrometheusOptions _options;
    private readonly HttpClient _httpClient;

    public PrometheusCollector(PrometheusOptions options, HttpClient httpClient)
    {
        _options = options;
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<EvidenceItem>> CollectAsync(IncidentRequest request, CancellationToken cancellationToken)
    {
        if (_options.Queries.Length == 0)
        {
            return [];
        }

        var evidence = new List<EvidenceItem>();
        foreach (var query in _options.Queries)
        {
            if (string.IsNullOrWhiteSpace(query.Query))
            {
                continue;
            }

            var endpoint = BuildEndpoint(request, query);
            using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                evidence.Add(CreateCollectorError(query, $"Prometheus request failed: {(int)response.StatusCode} {response.ReasonPhrase}", json));
                continue;
            }

            try
            {
                evidence.AddRange(ParseQueryRangeResponse(query, json));
            }
            catch (Exception ex)
            {
                evidence.Add(CreateCollectorError(query, $"Could not parse Prometheus response for '{query.Name}': {ex.Message}", null));
            }
        }

        return evidence;
    }

    private Uri BuildEndpoint(IncidentRequest request, PrometheusQueryDefinition query)
    {
        var baseUrl = _options.Url.TrimEnd('/') + "/";
        var parameters = new Dictionary<string, string>
        {
            ["query"] = query.Query,
            ["start"] = request.FromUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["end"] = request.ToUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["step"] = string.IsNullOrWhiteSpace(_options.Step) ? "60s" : _options.Step
        };

        var queryString = string.Join('&', parameters.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return new Uri(new Uri(baseUrl), $"api/v1/query_range?{queryString}");
    }

    private static IReadOnlyList<EvidenceItem> ParseQueryRangeResponse(PrometheusQueryDefinition query, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("status", out var status) || status.GetString() != "success")
        {
            return [CreateCollectorError(query, $"Prometheus query '{query.Name}' did not return success status.", json)];
        }

        if (!root.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("result", out var result) ||
            result.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var evidence = new List<EvidenceItem>();
        foreach (var series in result.EnumerateArray())
        {
            var labels = ExtractLabels(series);
            if (!series.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var maxValue = double.MinValue;
            DateTimeOffset? maxTimestamp = null;
            foreach (var sample in values.EnumerateArray())
            {
                if (sample.ValueKind != JsonValueKind.Array || sample.GetArrayLength() < 2)
                {
                    continue;
                }

                var sampleValues = sample.EnumerateArray().ToArray();
                var timestamp = ParseUnixTimestamp(sampleValues[0]);
                var value = ParsePrometheusValue(sampleValues[1]);
                if (timestamp is null || value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
                {
                    continue;
                }

                if (value > maxValue)
                {
                    maxValue = value.Value;
                    maxTimestamp = timestamp;
                }
            }

            if (maxTimestamp is null || maxValue < query.Threshold)
            {
                continue;
            }

            labels["query"] = query.Name;
            labels["threshold"] = query.Threshold.ToString("G4", CultureInfo.InvariantCulture);
            labels["max_value"] = maxValue.ToString("G6", CultureInfo.InvariantCulture);

            evidence.Add(new EvidenceItem
            {
                Timestamp = maxTimestamp.Value,
                Source = "prometheus",
                Kind = "metric",
                Severity = string.IsNullOrWhiteSpace(query.Severity) ? "info" : query.Severity.Trim().ToLowerInvariant(),
                Title = $"{query.Name}: max {maxValue.ToString("G6", CultureInfo.InvariantCulture)}",
                Summary = $"PromQL: {query.Query}",
                Service = labels.TryGetValue("job", out var job) ? job : null,
                Host = labels.TryGetValue("instance", out var instance) ? instance : null,
                Labels = labels,
                RelevanceScore = CalculateRelevance(maxValue, query.Threshold)
            });
        }

        return evidence;
    }

    private static Dictionary<string, string> ExtractLabels(JsonElement series)
    {
        var labels = new Dictionary<string, string>();
        if (!series.TryGetProperty("metric", out var metric) || metric.ValueKind != JsonValueKind.Object)
        {
            return labels;
        }

        foreach (var property in metric.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                labels[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return labels;
    }

    private static DateTimeOffset? ParseUnixTimestamp(JsonElement value)
    {
        double seconds;
        if (value.ValueKind == JsonValueKind.Number)
        {
            seconds = value.GetDouble();
        }
        else if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            seconds = parsed;
        }
        else
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeMilliseconds((long)(seconds * 1000));
    }

    private static double? ParsePrometheusValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedString))
        {
            return parsedString;
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.GetDouble();
        }

        return null;
    }

    private static double CalculateRelevance(double value, double threshold)
    {
        if (threshold <= 0)
        {
            return value > 0 ? 0.6 : 0.2;
        }

        return Math.Clamp(value / threshold / 2.0, 0.2, 1.0);
    }

    private static EvidenceItem CreateCollectorError(PrometheusQueryDefinition query, string title, string? raw)
    {
        return new EvidenceItem
        {
            Timestamp = DateTimeOffset.UtcNow,
            Source = "prometheus",
            Kind = "collector-error",
            Severity = "error",
            Title = title,
            Summary = $"Query: {query.Query}",
            RelevanceScore = 1.0,
            Raw = raw
        };
    }
}
