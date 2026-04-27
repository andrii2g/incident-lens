namespace A2G.IncidentLens.Core.Models;

public sealed class IncidentLensConfig
{
    public ElasticsearchOptions Elasticsearch { get; set; } = new();
    public PrometheusOptions Prometheus { get; set; } = new();
    public ReportOptions Report { get; set; } = new();
}

public sealed class ElasticsearchOptions
{
    public string Url { get; set; } = "http://localhost:9200";
    public string[] Indexes { get; set; } = [];
    public string TimeField { get; set; } = "@timestamp";
    public string[] MessageFields { get; set; } = ["message", "error.message", "log.message"];
    public string? ServiceField { get; set; } = "service.name";
    public string? EnvironmentField { get; set; } = "service.environment";
    public string? HostField { get; set; } = "host.name";
    public string? SeverityField { get; set; } = "log.level";
    public int MaxDocuments { get; set; } = 50;
    public string? ApiKey { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed class PrometheusOptions
{
    public string Url { get; set; } = "http://localhost:9090";
    public string Step { get; set; } = "60s";
    public PrometheusQueryDefinition[] Queries { get; set; } = [];
}

public sealed class PrometheusQueryDefinition
{
    public string Name { get; set; } = "Prometheus query";
    public string Query { get; set; } = string.Empty;
    public double Threshold { get; set; } = 0.0;
    public string Severity { get; set; } = "info";
}

public sealed class ReportOptions
{
    public int MaxTimelineItems { get; set; } = 40;
    public bool IncludeRaw { get; set; } = false;
}
