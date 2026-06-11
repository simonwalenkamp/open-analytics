# Open Analytics

Local-first analytics tooling for reading opencode usage data.

This first version only reads and structures local opencode usage data. It does not upload, persist, score, or analyze raw prompts.

## Run

```bash
dotnet run
```

By default, the app reads:

```text
~/.local/share/opencode/opencode.db
```
