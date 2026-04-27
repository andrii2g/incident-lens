using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using A2G.IncidentLens.Core;
using A2G.IncidentLens.Core.Models;
using A2G.IncidentLens.Core.Rendering;

return await BuildCommand().Parse(args).InvokeAsync();

static RootCommand BuildCommand()
{
    var configOption = new Option<FileInfo>("--config", "Path to the IncidentLens JSON config file.")
    {
        Required = true
    };
    configOption.Aliases.Add("-c");

    var symptomOption = new Option<string>("--symptom", "Text used for Elasticsearch simple_query_string search.")
    {
        DefaultValueFactory = _ => string.Empty
    };

    var fromOption = new Option<DateTimeOffset>("--from", "Start of investigation window. Prefer UTC ISO-8601.")
    {
        Required = true
    };

    var toOption = new Option<DateTimeOffset>("--to", "End of investigation window. Prefer UTC ISO-8601.")
    {
        Required = true
    };

    var serviceOption = new Option<string?>("--service", "Service name filter for Elasticsearch, and report metadata.");
    var environmentOption = new Option<string?>("--environment", "Environment filter for Elasticsearch, and report metadata.");
    var outputOption = new Option<DirectoryInfo>("--out", "Output directory.")
    {
        DefaultValueFactory = _ => new DirectoryInfo(Path.Combine(".", "out", "incidentlens-run"))
    };

    var command = new RootCommand("Collect and render evidence for incident investigation.")
    {
        Options =
        {
            configOption,
            symptomOption,
            fromOption,
            toOption,
            serviceOption,
            environmentOption,
            outputOption
        }
    };

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        try
        {
            var configPath = parseResult.GetValue(configOption);
            var symptom = parseResult.GetValue(symptomOption);
            var from = parseResult.GetValue(fromOption);
            var to = parseResult.GetValue(toOption);
            var service = parseResult.GetValue(serviceOption);
            var environment = parseResult.GetValue(environmentOption);
            var outputDirectory = parseResult.GetValue(outputOption);

            if (configPath is null)
            {
                throw new ArgumentException("Missing required argument --config.");
            }

            if (!configPath.Exists)
            {
                throw new FileNotFoundException($"Config file was not found: {configPath.FullName}");
            }

            if (outputDirectory is null)
            {
                throw new ArgumentException("Missing output directory.");
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };

            var configJson = await File.ReadAllTextAsync(configPath.FullName, cancellationToken);
            var config = JsonSerializer.Deserialize<IncidentLensConfig>(configJson, jsonOptions)
                ?? throw new InvalidOperationException("Could not read IncidentLens config.");

            var request = new IncidentRequest
            {
                Symptom = symptom ?? string.Empty,
                FromUtc = from.ToUniversalTime(),
                ToUtc = to.ToUniversalTime(),
                Service = service,
                Environment = environment
            };

            outputDirectory.Create();

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            var runner = new IncidentLensRunner(config, httpClient);
            var result = await runner.RunAsync(request, cancellationToken);

            var evidencePath = Path.Combine(outputDirectory.FullName, "evidence.json");
            var reportPath = Path.Combine(outputDirectory.FullName, "report.md");
            var mermaidPath = Path.Combine(outputDirectory.FullName, "timeline.mmd");
            var aiContextPath = Path.Combine(outputDirectory.FullName, "ai-context.md");

            await File.WriteAllTextAsync(evidencePath, JsonSerializer.Serialize(result.Evidence, jsonOptions), cancellationToken);
            await File.WriteAllTextAsync(reportPath, new MarkdownReportRenderer().Render(result, config), cancellationToken);
            await File.WriteAllTextAsync(mermaidPath, new MermaidTimelineRenderer().Render(result, config), cancellationToken);
            await File.WriteAllTextAsync(aiContextPath, new AiContextRenderer().Render(result, config), cancellationToken);

            Console.WriteLine("IncidentLens run completed.");
            Console.WriteLine($"Evidence:   {evidencePath}");
            Console.WriteLine($"Report:     {reportPath}");
            Console.WriteLine($"Mermaid:    {mermaidPath}");
            Console.WriteLine($"AI context: {aiContextPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    });

    return command;
}
