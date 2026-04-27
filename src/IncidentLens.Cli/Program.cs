using System.Text.Json;
using IncidentLens.Core;
using IncidentLens.Core.Models;
using IncidentLens.Core.Rendering;

var options = ParseArgs(args);
if (options.ContainsKey("help") || args.Length == 0)
{
    PrintUsage();
    return;
}

var configPath = Required(options, "config");
var symptom = options.GetValueOrDefault("symptom", string.Empty);
var from = ParseDate(Required(options, "from"), "from");
var to = ParseDate(Required(options, "to"), "to");
var outputDirectory = options.GetValueOrDefault("out", "./out/incidentlens-run");

var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    WriteIndented = true
};

var configJson = await File.ReadAllTextAsync(configPath);
var config = JsonSerializer.Deserialize<IncidentLensConfig>(configJson, jsonOptions)
             ?? throw new InvalidOperationException("Could not read IncidentLens config.");

var request = new IncidentRequest
{
    Symptom = symptom,
    FromUtc = from.ToUniversalTime(),
    ToUtc = to.ToUniversalTime(),
    Service = options.GetValueOrDefault("service"),
    Environment = options.GetValueOrDefault("environment")
};

Directory.CreateDirectory(outputDirectory);

using var httpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(60)
};

var runner = new IncidentLensRunner(config, httpClient);
var result = await runner.RunAsync(request);

var evidencePath = Path.Combine(outputDirectory, "evidence.json");
var reportPath = Path.Combine(outputDirectory, "report.md");
var mermaidPath = Path.Combine(outputDirectory, "timeline.mmd");
var aiContextPath = Path.Combine(outputDirectory, "ai-context.md");

await File.WriteAllTextAsync(evidencePath, JsonSerializer.Serialize(result.Evidence, jsonOptions));
await File.WriteAllTextAsync(reportPath, new MarkdownReportRenderer().Render(result, config));
await File.WriteAllTextAsync(mermaidPath, new MermaidTimelineRenderer().Render(result, config));
await File.WriteAllTextAsync(aiContextPath, new AiContextRenderer().Render(result, config));

Console.WriteLine("IncidentLens run completed.");
Console.WriteLine($"Evidence:   {evidencePath}");
Console.WriteLine($"Report:     {reportPath}");
Console.WriteLine($"Mermaid:    {mermaidPath}");
Console.WriteLine($"AI context: {aiContextPath}");

static Dictionary<string, string> ParseArgs(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (!arg.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = arg[2..];
        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            result[key] = args[++i];
        }
        else
        {
            result[key] = "true";
        }
    }
    return result;
}

static string Required(Dictionary<string, string> options, string key)
{
    if (!options.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
    {
        throw new ArgumentException($"Missing required argument --{key}.");
    }
    return value;
}

static DateTimeOffset ParseDate(string value, string name)
{
    if (!DateTimeOffset.TryParse(value, out var parsed))
    {
        throw new ArgumentException($"Could not parse --{name} as a date/time: {value}");
    }
    return parsed;
}

static void PrintUsage()
{
    Console.WriteLine("""
IncidentLens

Usage:
  incidentlens --config incidentlens.json --symptom "web UI freezes" --from "2026-04-27T10:00:00Z" --to "2026-04-27T10:45:00Z" [--service web-ui] [--environment prod] [--out ./out/run]

Required:
  --config       Path to IncidentLens JSON config.
  --from         Start of investigation window. Prefer UTC ISO-8601.
  --to           End of investigation window. Prefer UTC ISO-8601.

Optional:
  --symptom      Text used for Elasticsearch simple_query_string search.
  --service      Service name filter for Elasticsearch, and report metadata.
  --environment  Environment filter for Elasticsearch, and report metadata.
  --out          Output directory. Defaults to ./out/incidentlens-run.
""");
}
