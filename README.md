# IncidentLens

IncidentLens is a small evidence collector for incident investigation.

It takes a **symptom**, **time range**, and a small config file, then collects evidence from:

- Elasticsearch indexes or data streams
- Prometheus HTTP API

It renders:

- `evidence.json` — normalized raw evidence for automation
- `report.md` — simple human-readable incident report
- `timeline.mmd` — Mermaid timeline/flow diagram
- `ai-context.md` — sanitized context that can be pasted into an AI assistant for summarization

The initial version intentionally does **not** connect AI directly to production systems. IncidentLens collects and sanitizes evidence first; AI analysis is optional and happens only on the prepared output.

## What is this tool for?

Most incident reviews start with vague symptoms like:

> web UI freezes around 10:20 UTC

IncidentLens gives you a repeatable first pass:

1. Search relevant logs/events in Elasticsearch.
2. Query selected Prometheus metrics in the same time range.
3. Normalize everything into one evidence list.
4. Render a Markdown report and a Mermaid diagram.
5. Hand sanitized evidence to AI for a second-pass explanation.

## Quick start

Create a config from the example:

```bash
cp examples/incidentlens.example.json incidentlens.json
```

Run an investigation:

```bash
dotnet run --project src/IncidentLens.Cli -- \
  --config incidentlens.json \
  --symptom "web UI freezes" \
  --from "2026-04-27T10:00:00Z" \
  --to "2026-04-27T10:45:00Z" \
  --service "web-ui" \
  --environment "prod" \
  --out ./out/incident-2026-04-27-web-ui
```

Expected output:

```text
out/incident-2026-04-27-web-ui/
├── evidence.json
├── report.md
├── timeline.mmd
└── ai-context.md
```

## Configuration

See [`examples/incidentlens.example.json`](examples/incidentlens.example.json).

The v0 config is deliberately explicit. You choose which indexes/data streams and which PromQL queries are worth collecting. There is no hidden auto-discovery and no AI planner in the first version.

## Scope

IncidentLens collects evidence from configured Elasticsearch and Prometheus sources, then writes `evidence.json`, `report.md`, `timeline.mmd`, and `ai-context.md` for review. AI is optional and should only analyze the prepared output, not connect to production systems directly.
