# Open Analytics

Local analytics tooling for reading coding agent usage data. It produces an HTML
scorecard broken down by `harness/provider/model`, covering opencode, Claude
Code, Codex, and GitHub Copilot CLI.

## How to run

First, install dependencies:

```bash
asdf install
```

Then, run the app:

```bash
dotnet run
```

## Data sources

Each harness's data is read read-only from its default location. Any harness
whose data is absent is simply skipped.

| Harness | Location |
| --- | --- |
| opencode | `~/.local/share/opencode/opencode.db` |
| Claude Code | `~/.claude/projects/**/*.jsonl` |
| Codex | `~/.codex/sessions/**/rollout-*.jsonl` |
| GitHub Copilot CLI | `~/.copilot/data.db` |

## Metric coverage

Not every harness records the data a metric needs. Where a harness physically
cannot provide a metric (for example, Copilot CLI only stores session-level
aggregates), that entry is shown as `n/a` rather than omitted. Response-time
(latency) is only available for opencode, which is the only harness that records
a per-message completion timestamp.