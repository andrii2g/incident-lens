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

## Why this exists

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

## MVP scope

Included:

- Elasticsearch search over configured indexes/data streams
- Prometheus `query_range` over configured PromQL expressions
- normalized evidence model
- deterministic Markdown report
- Mermaid flow-style timeline
- sanitized AI context output

Not included yet:

- Grafana connector
- alert manager connector
- Kubernetes connector
- full correlation engine
- automatic symptom classification
- direct AI provider integration
- writing anything back to production systems

## AI usage model

IncidentLens produces `ai-context.md`. You can paste that file into an AI assistant and ask for:

- a short executive summary
- likely cause hypotheses
- missing evidence
- next checks
- a cleaner incident timeline

The AI should only analyze the prepared evidence. It should not query Elasticsearch, Prometheus, Kubernetes, or production systems directly.
