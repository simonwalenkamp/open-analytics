using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenAnalytics.models;

namespace OpenAnalytics;

internal static class HtmlReporter
{
    private const string Unknown = "(unknown)";
    private const string AssistantRole = "assistant";
    private const string UserRole = "user";
    private const string CompletedStatus = "completed";
    private const string TodoWriteTool = "todowrite";
    private const string PlanAgent = "plan";
    private const string PlanMode = "plan";
    private const int MaxReadErrors = 10;
    private const string TemplateFileName = "ReportTemplate.html";

    public static string Write(AnalyticsData data) =>
        WriteHtml(BuildReport(data, null, data.Errors));

    public static string WriteNoData(IReadOnlyList<ReadError> errors) =>
        WriteHtml(BuildReport(null,
            "No coding agent usage data was found. Looked for opencode, Claude Code, Codex, and GitHub Copilot CLI data in their default locations.",
            errors));

    private static string WriteHtml(string html)
    {
        var path = Path.Combine(Path.GetTempPath(), $"open-analytics-{DateTime.Now:yyyyMMdd-HHmmss-fff}.html");
        File.WriteAllText(path, html);
        return path;
    }

    private static string BuildReport(AnalyticsData? data, string? errorMessage, IReadOnlyList<ReadError> errors)
    {
        var template = LoadTemplate();
        var content = errorMessage is not null
            ? string.Join(Environment.NewLine,
                RenderTemplate(ExtractTemplate(template, "error-panel"), new Dictionary<string, string>
                {
                    ["message"] = Escape(errorMessage)
                }),
                // Even with no sessions, surface why each reader came up empty so a
                // user with only a broken store sees the cause, not just "no data".
                RenderReadErrors(template, errors))
            : BuildContent(template, data!);

        return RenderTemplate(RemoveTemplateDefinitions(template), new Dictionary<string, string>
        {
            ["generatedAt"] = Escape(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
            ["content"] = content
        });
    }

    private static string BuildContent(string template, AnalyticsData data) =>
        string.Join(Environment.NewLine, RenderSummary(template, data), RenderHarnessBreakdown(template, data),
            RenderReadErrors(template, data.Errors), RenderScorecard(template, data));

    private static string RenderSummary(string template, AnalyticsData data)
    {
        var cards = new[]
        {
            RenderMetricCard(template, "Sessions", data.Sessions.Count.ToString()),
            RenderMetricCard(template, "Avg session", AverageSessionDuration(data.Sessions)),
            RenderMetricCard(template, "Messages", data.MessageCount.ToString()),
            RenderMetricCard(template, "Parts", data.PartCount.ToString()),
            RenderMetricCard(template, "Read errors", data.Errors.Count.ToString())
        };

        return RenderTemplate(ExtractTemplate(template, "summary"), new Dictionary<string, string>
        {
            ["cards"] = string.Join(Environment.NewLine, cards)
        });
    }

    private static string RenderMetricCard(string template, string label, string value) =>
        RenderTemplate(ExtractTemplate(template, "metric-card"), new Dictionary<string, string>
        {
            ["label"] = Escape(label),
            ["value"] = Escape(value)
        });

    private static string RenderHarnessBreakdown(string template, AnalyticsData data)
    {
        var rows = data.Sessions
            .GroupBy(session => session.Harness)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => RenderTemplate(ExtractTemplate(template, "summary-by-harness-row"),
                new Dictionary<string, string>
                {
                    ["harness"] = Escape(group.Key),
                    ["sessions"] = group.Count().ToString(),
                    ["messages"] = group.Sum(session => session.Messages.Count).ToString()
                }))
            .ToList();

        if (rows.Count == 0)
        {
            return string.Empty;
        }

        return RenderTemplate(ExtractTemplate(template, "summary-by-harness"), new Dictionary<string, string>
        {
            ["rows"] = string.Join(Environment.NewLine, rows)
        });
    }

    private static string AverageSessionDuration(IEnumerable<Session> sessions)
    {
        var durations = sessions.Select(SessionDurationSeconds).OfType<double>().ToList();
        return durations.Count == 0 ? "-" : FormatDuration(durations.Average());
    }

    private static string RenderReadErrors(string template, IReadOnlyList<ReadError> errors)
    {
        if (errors.Count <= 0)
        {
            return string.Empty;
        }

        var items = errors
            .Take(MaxReadErrors)
            .Select(error => RenderTemplate(ExtractTemplate(template, "read-error-item"), new Dictionary<string, string>
            {
                ["path"] = Escape(error.Path),
                ["message"] = Escape(error.Message)
            }))
            .ToList();

        if (errors.Count > MaxReadErrors)
        {
            items.Add(RenderTemplate(ExtractTemplate(template, "read-error-more"), new Dictionary<string, string>
            {
                ["count"] = (errors.Count - MaxReadErrors).ToString()
            }));
        }

        return RenderTemplate(ExtractTemplate(template, "read-errors"), new Dictionary<string, string>
        {
            ["items"] = string.Join(Environment.NewLine, items)
        });
    }

    private static string RenderScorecard(string template, AnalyticsData data)
    {
        var scores = BuildModelScores(data);

        if (scores.Count == 0)
        {
            return ExtractTemplate(template, "scorecard-empty");
        }

        var orderedScores = OrderedScores(scores).ToList();
        var cards = new[] { RenderScorecardVolume(template, orderedScores) }
            .Concat(ScoreMetrics()
            .Select(metric => RenderScorecardMetric(template, metric, orderedScores))
            .ToList());

        return RenderTemplate(ExtractTemplate(template, "scorecard"), new Dictionary<string, string>
        {
            ["cards"] = string.Join(Environment.NewLine, cards)
        });
    }

    private static string LoadTemplate()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, TemplateFileName);
        return File.ReadAllText(File.Exists(outputPath)
            ? outputPath
            : Path.Combine(Directory.GetCurrentDirectory(), TemplateFileName));
    }

    private static string ExtractTemplate(string template, string id)
    {
        var match = Regex.Match(
            template,
            $"<template\\s+id=\"{Regex.Escape(id)}\"\\s*>(.*?)</template>",
            RegexOptions.Singleline);

        if (!match.Success)
        {
            throw new InvalidOperationException($"Report template fragment not found: {id}");
        }

        return match.Groups[1].Value.Trim();
    }

    private static string RemoveTemplateDefinitions(string template) =>
        Regex.Replace(template, @"\s*<template\s+id=""[^""]+""\s*>.*?</template>", string.Empty,
            RegexOptions.Singleline);

    private static string RenderTemplate(string template, IReadOnlyDictionary<string, string> values)
    {
        foreach (var (key, value) in values)
        {
            template = template.Replace($"{{{{{key}}}}}", value);
        }

        return template;
    }

    private static Dictionary<string, ModelScore> BuildModelScores(AnalyticsData data)
    {
        var scores = new Dictionary<string, ModelScore>();

        foreach (var session in data.Sessions)
        {
            foreach (var message in session.Messages)
            {
                RecordMessageMetrics(scores, session.Harness, message);
            }
        }

        foreach (var session in data.Sessions)
        {
            RecordSessionMetrics(scores, session);
        }

        return scores;
    }

    private static void RecordMessageMetrics(Dictionary<string, ModelScore> scores, string harness, Message message)
    {
        var modelKey = ModelKey(harness, message);

        if (message.ModelId is not null)
        {
            RecordModelVolume(GetScore(scores, modelKey, harness), message);
        }

        if (message.Role == AssistantRole)
        {
            RecordAssistantResponse(GetScore(scores, modelKey, harness), message);
        }

        foreach (var part in message.Parts.Where(IsCompletedEditingTool))
        {
            GetScore(scores, modelKey, harness).Quality.Record(LeftErrorDiagnostic(part.State));
        }
    }

    private static void RecordModelVolume(ModelScore score, Message message)
    {
        score.Messages++;
        score.Sessions.Add(message.SessionId);

        var latencySeconds = LatencySeconds(message);
        if (latencySeconds is not null)
        {
            score.TotalLatencySeconds += latencySeconds.Value;
            score.LatencySamples++;
        }

        score.InputTokens += message.Tokens?.Input ?? 0;
        score.OutputTokens += message.Tokens?.Output ?? 0;
    }

    private static void RecordAssistantResponse(ModelScore score, Message message)
    {
        score.AssistantMessages++;
        if (message.WasAborted)
        {
            score.Interrupts++;
        }
    }

    private static void RecordSessionMetrics(
        Dictionary<string, ModelScore> scores,
        Session session)
    {
        var score = GetScore(scores, SessionKey(session), session.Harness);
        var durationSeconds = SessionDurationSeconds(session);

        if (durationSeconds is not null)
        {
            score.TotalSessionDurationSeconds += durationSeconds.Value;
            score.SessionDurationSamples++;
        }

        if (session.Reverted)
        {
            score.Reverts++;
        }

        if (!IsTopLevelCodingSession(session))
        {
            return;
        }

        var parts = session.Messages.SelectMany(m => m.Parts).ToList();
        var hasCompletedEdit = parts.Any(IsCompletedEditingTool);
        var hasTodo = parts.Any(p => p.Tool == TodoWriteTool);
        var hasPlanMode = UsedPlanMode(session);
        var netChange = (session.Summary?.Additions ?? 0) + (session.Summary?.Deletions ?? 0);

        if (hasTodo)
        {
            score.TodoSessions++;
        }
        else
        {
            score.NoTodoSessions++;
        }

        if (hasPlanMode)
        {
            score.PlanModeSessions++;
        }
        else
        {
            score.NoPlanModeSessions++;
        }

        if (hasCompletedEdit && netChange == 0 && !session.Reverted)
        {
            score.ZeroLanded++;
        }

        if (hasTodo && !hasCompletedEdit)
        {
            score.TodoNotDone++;
        }

        if (hasPlanMode && !hasCompletedEdit)
        {
            score.PlanModeNotDone++;
        }

        if (TodoRevisedBeforeExecution(session))
        {
            score.TodoRevised++;
        }

        if (PlanModeRevisedBeforeExecution(session))
        {
            score.PlanModeRevised++;
        }
    }

    private static bool IsTopLevelCodingSession(Session session) =>
        session.ParentId is null && session.Agent is not PlanAgent and not "ask" and not "explore";

    /// <summary>
    /// Plan mode in opencode is recorded per-message via the message <c>mode</c>
    /// field (not as a separate child session). A coding session "used plan mode"
    /// when at least one of its messages was sent while in plan mode.
    /// </summary>
    private static bool UsedPlanMode(Session session) =>
        session.Messages.Any(message => message.Mode == PlanMode);

    private static IEnumerable<KeyValuePair<string, ModelScore>> OrderedScores(Dictionary<string, ModelScore> scores) =>
        scores
            .OrderByDescending(x => x.Value.Messages)
            .ThenByDescending(x => x.Value.Quality.Edits)
            .ThenBy(x => x.Key);

    private static IEnumerable<ScoreMetric> ScoreMetrics()
    {
        yield return new ScoreMetric("Avg. messages per session.",
            "Messages divided by distinct sessions for the model.",
            score => score.Sessions.Count == 0 ? null : score.MessagesPerSession,
            value => $"{value:F1}",
            Capability: Capabilities.Messages);
        yield return new ScoreMetric("Avg. tokens in per session.",
            "Average reported input tokens per distinct session for the model.",
            score => score.Sessions.Count == 0 || score.InputTokens == 0 ? null : score.InputTokensPerSession,
            value => FormatTokens((long)Math.Round(value)),
            Capability: Capabilities.Tokens);
        yield return new ScoreMetric("Avg. tokens out per session.",
            "Average reported output tokens per distinct session for the model.",
            score => score.Sessions.Count == 0 || score.OutputTokens == 0 ? null : score.OutputTokensPerSession,
            value => FormatTokens((long)Math.Round(value)),
            Capability: Capabilities.Tokens);
        yield return new ScoreMetric("Avg. response time.",
            "Average completion latency for non-aborted messages with both created and completed timestamps.",
            score => score.AvgLatencySeconds,
            value => $"{value:F1}s",
            SortDirection.Ascending,
            Capabilities.Latency);
        yield return new ScoreMetric("Avg. time per session.",
            "Average session duration from first user message to last completed assistant output.",
            score => score.AvgSessionDurationSeconds,
            FormatDuration,
            SortDirection.Ascending,
            Capabilities.Duration);
        yield return new ScoreMetric("Avg. amount of edits per session.",
            "Average completed edit, write, or apply_patch tool calls per distinct session for the model.",
            score => score.Sessions.Count == 0 ? null : score.EditsPerSession,
            value => $"{value:F1}",
            Capability: Capabilities.Edits);
        yield return new ScoreMetric("Avg. amount of responses interrupted.",
            "Percentage of assistant messages that were aborted, for the model.",
            score => score.AssistantMessages == 0 ? null : (double)score.Interrupts / score.AssistantMessages * 100,
            value => $"{value:F1}%",
            Capability: Capabilities.Interrupts);
        yield return new ScoreMetric("Plan revised",
            "Percentage of plan-mode coding sessions where the user responded after the plan and plan mode was used again before execution.",
            score => score.PlanModeRevisedPercentage,
            value => $"{value:F1}%",
            Capability: Capabilities.Plan);
        yield return new ScoreMetric("Plan mode usage",
            "Percentage of top-level coding sessions that used plan mode (at least one message sent in plan mode).",
            score => score.PlanModeSessionPercentage,
            value => $"{value:F1}%",
            Capability: Capabilities.Plan);
        yield return new ScoreMetric("Plan not executed",
            "Percentage of plan-mode coding sessions where no completed edit followed.",
            score => score.PlanModeNotDonePercentage,
            value => $"{value:F1}%",
            Capability: Capabilities.Plan);
    }

    /// <summary>
    /// Capability ids identify what a metric needs from the underlying data. Not
    /// every harness stores everything (e.g. Copilot persists only session-level
    /// aggregates), so a metric is only shown as a real number for harnesses that
    /// <see cref="Supports"/> its capability; the rest render as "n/a".
    /// </summary>
    private static class Capabilities
    {
        public const string Messages = "messages";
        public const string Tokens = "tokens";
        public const string Latency = "latency";
        public const string Duration = "duration";
        public const string Edits = "edits";
        public const string Interrupts = "interrupts";
        public const string Plan = "plan";
    }

    /// <summary>
    /// Whether <paramref name="harness"/> physically records enough data to
    /// compute the given <paramref name="capability"/>. Unknown harnesses are
    /// assumed fully capable so a new reader is not silently blanked out.
    /// </summary>
    private static bool Supports(string harness, string capability) => harness switch
    {
        "opencode" => true,
        "claude-code" => capability is not Capabilities.Latency,
        "codex" => capability is not (Capabilities.Latency or Capabilities.Plan),
        "copilot" => capability is Capabilities.Tokens or Capabilities.Duration or Capabilities.Interrupts,
        _ => true
    };

    private static string RenderScorecardVolume(
        string template,
        IEnumerable<KeyValuePair<string, ModelScore>> scores)
    {
        var rows = scores.Select(score => RenderTemplate(ExtractTemplate(template, "scorecard-volume-row"),
            new Dictionary<string, string>
            {
                ["model"] = Escape(score.Key),
                ["messages"] = score.Value.Messages.ToString(),
                ["sessions"] = score.Value.Sessions.Count.ToString(),
                ["tokens"] = FormatTokens(score.Value.OutputTokens)
            }));

        return RenderTemplate(ExtractTemplate(template, "scorecard-volume-card"), new Dictionary<string, string>
        {
            ["rows"] = string.Join(Environment.NewLine, rows)
        });
    }

    private static string RenderScorecardMetric(
        string template,
        ScoreMetric metric,
        IReadOnlyList<KeyValuePair<string, ModelScore>> scores)
    {
        // Harnesses that can compute this metric: sort by value as before,
        // dropping entries with no data. Harnesses that physically cannot
        // provide it are listed afterwards as "n/a" so the gap is explicit
        // rather than an invisible omission.
        var supported = OrderByMetric(scores
                .Where(score => Supports(score.Value.Harness, metric.Capability))
                .Select(score => new MetricScore(score.Key, metric.Value(score.Value)))
                .Where(score => score.Value is not null),
                metric.SortDirection)
            .Select(score => RenderMetricValue(template, score.Key, metric.Format(score.Value!.Value)));

        var unsupported = scores
            .Where(score => !Supports(score.Value.Harness, metric.Capability))
            .OrderBy(score => score.Key)
            .Select(score => RenderMetricValue(template, score.Key, "n/a"));

        return RenderTemplate(ExtractTemplate(template, "scorecard-metric-card"), new Dictionary<string, string>
        {
            ["label"] = Escape(metric.Label),
            ["description"] = Escape(metric.Description),
            ["values"] = string.Join(Environment.NewLine, supported.Concat(unsupported))
        });
    }

    private static string RenderMetricValue(string template, string key, string value) =>
        RenderTemplate(ExtractTemplate(template, "scorecard-metric-value"), new Dictionary<string, string>
        {
            ["model"] = Escape(key),
            ["value"] = Escape(value)
        });

    private static IOrderedEnumerable<MetricScore> OrderByMetric(
        IEnumerable<MetricScore> scores,
        SortDirection sortDirection) =>
        sortDirection == SortDirection.Ascending
            ? scores.OrderBy(score => score.Value!.Value).ThenBy(score => score.Key)
            : scores.OrderByDescending(score => score.Value!.Value).ThenBy(score => score.Key);

    private enum SortDirection
    {
        Ascending,
        Descending
    }

    private sealed record MetricScore(string Key, double? Value);

    private sealed record SessionEvent(DateTimeOffset? At, int Order, string Id, string Type);

    private sealed record ScoreMetric(
        string Label,
        string Description,
        Func<ModelScore, double?> Value,
        Func<double, string> Format,
        SortDirection SortDirection = SortDirection.Descending,
        string Capability = "");

    private static ModelScore GetScore(Dictionary<string, ModelScore> scores, string key, string harness)
    {
        if (scores.TryGetValue(key, out var score)) return score;

        score = new ModelScore { Harness = harness };
        scores[key] = score;

        return score;
    }

    /// <summary>Compact token count: 12345 -> "12.3k", 1234567 -> "1.2M".</summary>
    private static string FormatTokens(long tokens) => tokens switch
    {
        0 => "-",
        >= 1_000_000 => $"{tokens / 1_000_000d:F1}M",
        >= 1_000 => $"{tokens / 1_000d:F1}k",
        _ => tokens.ToString()
    };

    private static string Escape(string value) =>
        WebUtility.HtmlEncode(value);

    /// <summary>
    /// The scorecard key a whole session's session-level metrics are attributed
    /// to: the session's harness plus its dominant (most-used) model, so the
    /// session counts land on the same <c>harness/provider/model</c> row as that
    /// model's message-level metrics.
    /// </summary>
    private static string SessionKey(Session session)
    {
        var dominant = session.Messages
            .Where(m => m.ModelId is not null)
            .GroupBy(m => ModelKey(session.Harness, m))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefault();

        return dominant ?? $"{session.Harness}/{Unknown}";
    }

    private static string ModelKey(string harness, Message message) =>
        message.ModelId is null
            ? $"{harness}/{Unknown}"
            : $"{harness}/{message.ProviderId ?? Unknown}/{message.ModelId}";

    private static double? LatencySeconds(Message message)
    {
        if (message.WasAborted || message.CreatedAt is null || message.CompletedAt is null)
        {
            return null;
        }

        var seconds = (message.CompletedAt.Value - message.CreatedAt.Value).TotalSeconds;
        return seconds >= 0 ? seconds : null;
    }

    private static double? SessionDurationSeconds(Session session)
    {
        var firstUserMessage = session.Messages
            .Where(message => message.Role == UserRole && message.CreatedAt is not null)
            .OrderBy(message => message.CreatedAt)
            .Select(message => message.CreatedAt)
            .FirstOrDefault();

        var lastModelOutput = session.Messages
            .Where(message => message.Role == AssistantRole && message.CompletedAt is not null)
            .OrderByDescending(message => message.CompletedAt)
            .Select(message => message.CompletedAt)
            .FirstOrDefault();

        if (firstUserMessage is null || lastModelOutput is null)
        {
            return null;
        }

        var seconds = (lastModelOutput.Value - firstUserMessage.Value).TotalSeconds;
        return seconds >= 0 ? seconds : null;
    }

    private static string FormatDuration(double seconds)
    {
        if (seconds < 60)
        {
            return $"{seconds:F1}s";
        }

        if (seconds < 3600)
        {
            return $"{seconds / 60:F1}m";
        }

        return $"{seconds / 3600:F1}h";
    }

    private static bool IsEditingTool(string? tool) =>
        tool is "edit" or "write" or "apply_patch";

    private static bool IsCompletedEditingTool(Part part) =>
        IsEditingTool(part.Tool) && part.ToolStatus == CompletedStatus;

    /// <summary>
    /// Detects sessions where a todowrite task list was suggested, the user asked for
    /// changes, and a new task list was generated before any execution — i.e. the user
    /// pushed back on the proposal before letting it run.
    ///
    /// Defined as: at least two <c>todowrite</c> tool calls occur with a user message in
    /// between them, and that second task list happens before the session's first
    /// completed editing tool call. Walking events in chronological order
    /// (messages by CreatedAt, parts by StartedAt — see OpencodeReader) lets us
    /// distinguish revision-during-discussion from the agent self-revising its todo
    /// list mid-execution. The user message requirement is a heuristic for "the user
    /// asked for changes" rather than the agent rewriting its own list unprompted.
    /// </summary>
    private static bool TodoRevisedBeforeExecution(Session session)
    {
        var sawPlan = false;
        var sawUserSincePlan = false;

        foreach (var message in session.Messages)
        {
            // A user turn after a task list is the "asked for changes" signal. Only
            // counts once a task list has already been proposed.
            if (message.Role == UserRole && sawPlan)
            {
                sawUserSincePlan = true;
            }

            foreach (var part in message.Parts)
            {
                // Execution started before a revision landed: this session does not
                // match "revised before being allowed to execute".
                if (IsCompletedEditingTool(part))
                {
                    return false;
                }

                if (part.Tool != TodoWriteTool)
                {
                    continue;
                }

                // A second task list, preceded by a user turn, before any completed edit.
                if (sawPlan && sawUserSincePlan)
                {
                    return true;
                }

                sawPlan = true;
                sawUserSincePlan = false;
            }
        }

        return false;
    }

    private static bool PlanModeRevisedBeforeExecution(Session session)
    {
        var events = session.Messages
            .SelectMany(message => PlanRevisionEvents(message))
            .OrderBy(e => e.At ?? DateTimeOffset.MaxValue)
            .ThenBy(e => e.Order)
            .ThenBy(e => e.Id);

        var sawPlan = false;
        var sawUserSincePlan = false;

        foreach (var item in events)
        {
            if (item.Type == "edit")
            {
                return false;
            }

            if (item.Type == "user" && sawPlan)
            {
                sawUserSincePlan = true;
                continue;
            }

            if (item.Type != "plan")
            {
                continue;
            }

            if (sawPlan && sawUserSincePlan)
            {
                return true;
            }

            sawPlan = true;
            sawUserSincePlan = false;
        }

        return false;
    }

    private static IEnumerable<SessionEvent> PlanRevisionEvents(Message message)
    {
        if (message.Role == UserRole)
        {
            yield return new SessionEvent(message.CreatedAt, 0, message.Id, "user");
        }

        if (message.Mode == PlanMode)
        {
            yield return new SessionEvent(message.CreatedAt, 1, message.Id, "plan");
        }

        foreach (var part in message.Parts.Where(IsCompletedEditingTool))
        {
            yield return new SessionEvent(part.StartedAt ?? message.CreatedAt, 2, part.Id, "edit");
        }
    }

    /// <summary>
    /// Returns true if the edit left an error-severity diagnostic <em>in a file the
    /// edit actually touched</em>.
    ///
    /// The opencode <c>state.metadata.diagnostics</c> blob is a snapshot of the whole
    /// workspace after the edit, keyed by absolute file path. Counting an error in ANY
    /// of those files (including files this edit never touched) wrongly blames the edit
    /// for pre-existing or unrelated breakage. So we first determine which file(s) the
    /// edit changed and only inspect diagnostics for those paths.
    ///
    /// Diagnostics use the LSP severity scale where 1 == Error (2 = Warning, 3 = Info,
    /// 4 = Hint). If the touched-file set cannot be determined (no diff/input info), we
    /// return false rather than over-attribute an error.
    /// </summary>
    private static bool LeftErrorDiagnostic(JsonElement? state)
    {
        if (!TryGetDiagnostics(state, out var value, out var metadata, out var diagnostics))
        {
            return false;
        }

        var editedFiles = EditedFilePaths(value, metadata);
        return editedFiles.Count > 0 && HasErrorDiagnostic(diagnostics, editedFiles);
    }

    private static bool TryGetDiagnostics(
        JsonElement? state,
        out JsonElement value,
        out JsonElement metadata,
        out JsonElement diagnostics)
    {
        value = default;
        metadata = default;
        diagnostics = default;

        if (state is not { ValueKind: JsonValueKind.Object } stateValue
            || !stateValue.TryGetProperty("metadata", out var metadataValue)
            || metadataValue.ValueKind != JsonValueKind.Object
            || !metadataValue.TryGetProperty("diagnostics", out var diagnosticsValue)
            || diagnosticsValue.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        value = stateValue;
        metadata = metadataValue;
        diagnostics = diagnosticsValue;
        return true;
    }

    private static bool HasErrorDiagnostic(JsonElement diagnostics, HashSet<string> editedFiles)
    {
        return diagnostics
            .EnumerateObject()
            .Where(file => editedFiles.Contains(file.Name) && file.Value.ValueKind == JsonValueKind.Array)
            .SelectMany(file => file.Value.EnumerateArray())
            .Any(IsErrorDiagnostic);
    }

    private static bool IsErrorDiagnostic(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty("severity", out var severity)
        && severity.ValueKind == JsonValueKind.Number
        && severity.TryGetInt32(out var value)
        && value == 1;

    /// <summary>
    /// Resolves the absolute path(s) of the file(s) an editing tool changed, so error
    /// attribution can be scoped to them. Tools expose this differently:
    ///   - edit / write : single path at <c>state.input.filePath</c>.
    ///   - apply_patch  : one or more paths at <c>state.metadata.files[].filePath</c>
    ///                    (a patch can touch several files).
    /// </summary>
    private static HashSet<string> EditedFilePaths(JsonElement state, JsonElement metadata)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);

        AddInputFilePath(paths, state);
        AddPatchFilePaths(paths, metadata);
        AddPatchDiffFilePath(paths, metadata);

        return paths;
    }

    private static void AddInputFilePath(HashSet<string> paths, JsonElement state)
    {
        if (state.TryGetProperty("input", out var input)
            && TryGetString(input, "filePath") is { Length: > 0 } path)
        {
            paths.Add(path);
        }
    }

    private static void AddPatchFilePaths(HashSet<string> paths, JsonElement metadata)
    {
        if (!metadata.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var file in files.EnumerateArray())
        {
            if (TryGetString(file, "filePath") is { Length: > 0 } path)
            {
                paths.Add(path);
            }
        }
    }

    private static void AddPatchDiffFilePath(HashSet<string> paths, JsonElement metadata)
    {
        if (metadata.TryGetProperty("filediff", out var filediff)
            && TryGetString(filediff, "file") is { Length: > 0 } path)
        {
            paths.Add(path);
        }
    }

    private static string? TryGetString(JsonElement value, string propertyName)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }
}
