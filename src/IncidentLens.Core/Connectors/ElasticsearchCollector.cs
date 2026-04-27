using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using A2G.IncidentLens.Core.Models;

namespace A2G.IncidentLens.Core.Connectors;

public sealed class ElasticsearchCollector : IEvidenceCollector
{
    private readonly ElasticsearchOptions _options;
    private readonly HttpClient _httpClient;

    public ElasticsearchCollector(ElasticsearchOptions options, HttpClient httpClient)
    {
        _options = options;
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<EvidenceItem>> CollectAsync(IncidentRequest request, CancellationToken cancellationToken)
    {
        if (_options.Indexes.Length == 0)
        {
            return [];
        }

        var endpoint = BuildEndpoint();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        AddAuthentication(httpRequest);

        var body = BuildSearchBody(request);
        httpRequest.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return [CreateCollectorError(request, $"Elasticsearch request failed: {(int)response.StatusCode} {response.ReasonPhrase}", json)];
        }

        try
        {
            return ParseSearchResponse(request, json);
        }
        catch (Exception ex)
        {
            return [CreateCollectorError(request, $"Could not parse Elasticsearch response: {ex.Message}", null)];
        }
    }

    private Uri BuildEndpoint()
    {
        var baseUrl = _options.Url.TrimEnd('/') + "/";
        var indexSelector = string.Join(',', _options.Indexes.Select(x => x.Trim()).Where(x => x.Length > 0));
        return new Uri(new Uri(baseUrl), $"{indexSelector}/_search");
    }

    private JsonObject BuildSearchBody(IncidentRequest request)
    {
        var must = new JsonArray
        {
            new JsonObject
            {
                ["range"] = new JsonObject
                {
                    [_options.TimeField] = new JsonObject
                    {
                        ["gte"] = request.FromUtc.UtcDateTime.ToString("O"),
                        ["lte"] = request.ToUtc.UtcDateTime.ToString("O"),
                        ["format"] = "strict_date_optional_time"
                    }
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(request.Symptom))
        {
            must.Add(new JsonObject
            {
                ["simple_query_string"] = new JsonObject
                {
                    ["query"] = request.Symptom,
                    ["fields"] = ToJsonArray(_options.MessageFields.Length == 0 ? ["message"] : _options.MessageFields),
                    ["default_operator"] = "and"
                }
            });
        }

        var filter = new JsonArray();
        if (!string.IsNullOrWhiteSpace(request.Service) && !string.IsNullOrWhiteSpace(_options.ServiceField))
        {
            filter.Add(new JsonObject
            {
                ["term"] = new JsonObject
                {
                    [$"{_options.ServiceField}.keyword"] = request.Service
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Environment) && !string.IsNullOrWhiteSpace(_options.EnvironmentField))
        {
            filter.Add(new JsonObject
            {
                ["term"] = new JsonObject
                {
                    [$"{_options.EnvironmentField}.keyword"] = request.Environment
                }
            });
        }

        var boolQuery = new JsonObject
        {
            ["must"] = must
        };

        if (filter.Count > 0)
        {
            boolQuery["filter"] = filter;
        }

        return new JsonObject
        {
            ["size"] = Math.Clamp(_options.MaxDocuments, 1, 500),
            ["track_total_hits"] = true,
            ["query"] = new JsonObject
            {
                ["bool"] = boolQuery
            },
            ["sort"] = new JsonArray
            {
                new JsonObject
                {
                    [_options.TimeField] = new JsonObject
                    {
                        ["order"] = "asc"
                    }
                }
            }
        };
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                array.Add(value);
            }
        }

        return array;
    }

    private void AddAuthentication(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", _options.ApiKey);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password))
        {
            var raw = Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(raw));
        }
    }

    private IReadOnlyList<EvidenceItem> ParseSearchResponse(IncidentRequest request, string json)
    {
        using var document = JsonDocument.Parse(json);
        var evidence = new List<EvidenceItem>();

        if (!document.RootElement.TryGetProperty("hits", out var hitsRoot) ||
            !hitsRoot.TryGetProperty("hits", out var hits) ||
            hits.ValueKind != JsonValueKind.Array)
        {
            return evidence;
        }

        foreach (var hit in hits.EnumerateArray())
        {
            if (!hit.TryGetProperty("_source", out var source) || source.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var timestamp = ExtractDate(source, _options.TimeField) ?? request.FromUtc;
            var title = FirstNonEmpty(_options.MessageFields.Select(field => ExtractString(source, field))) ?? "Elasticsearch document";
            var index = hit.TryGetProperty("_index", out var indexValue) ? indexValue.GetString() : null;
            var score = hit.TryGetProperty("_score", out var scoreValue) && scoreValue.ValueKind == JsonValueKind.Number
                ? scoreValue.GetDouble()
                : 0.0;

            var labels = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(index))
            {
                labels["index"] = index;
            }

            if (score > 0)
            {
                labels["elastic_score"] = score.ToString("G4", System.Globalization.CultureInfo.InvariantCulture);
            }

            evidence.Add(new EvidenceItem
            {
                Timestamp = timestamp,
                Source = "elasticsearch",
                Kind = "log",
                Severity = NormalizeSeverity(ExtractString(source, _options.SeverityField) ?? "info"),
                Title = Truncate(title, 180),
                Summary = Truncate(title, 500),
                Service = ExtractString(source, _options.ServiceField),
                Environment = ExtractString(source, _options.EnvironmentField),
                Host = ExtractString(source, _options.HostField),
                Labels = labels,
                RelevanceScore = Math.Clamp(score / 10.0, 0.1, 1.0)
            });
        }

        return evidence;
    }

    private static EvidenceItem CreateCollectorError(IncidentRequest request, string title, string? raw)
    {
        return new EvidenceItem
        {
            Timestamp = DateTimeOffset.UtcNow,
            Source = "elasticsearch",
            Kind = "collector-error",
            Severity = "error",
            Title = title,
            Summary = "The Elasticsearch collector could not complete. Check endpoint, credentials, index names, and network access.",
            RelevanceScore = 1.0,
            Raw = raw
        };
    }

    private static string NormalizeSeverity(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "critical" or "crit" or "fatal" => "critical",
            "error" or "err" => "error",
            "warning" or "warn" => "warning",
            "debug" => "debug",
            _ => "info"
        };
    }

    private static string? FirstNonEmpty(IEnumerable<string?> values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(0, maxLength - 3)] + "...";
    }

    private static string? ExtractString(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static DateTimeOffset? ExtractDate(JsonElement root, string path)
    {
        var value = ExtractString(root, path);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, out var parsed) ? parsed.ToUniversalTime() : null;
    }
}
