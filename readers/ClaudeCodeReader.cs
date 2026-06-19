using System.Text.Json;
using OpenAnalytics.Helpers;
using OpenAnalytics.models;

namespace OpenAnalytics.Readers;

/// <summary>
/// Reads Claude Code session transcripts from <c>~/.claude/projects/**/*.jsonl</c>.
/// Each file is one session. Claude streams a single logical assistant turn
/// across several JSONL lines that all repeat the same <c>message.id</c> and an
/// identical <c>usage</c> block (one line per content block: thinking, text,
/// tool_use). We therefore key assistant messages by <c>message.id</c>, count
/// tokens once, and collect tool_use blocks (deduped by their own id) across all
/// lines of that turn.
///
/// Claude also records <c>type: "user"</c> lines for two distinct things: real
/// human prompts (<c>message.content</c> is a string or contains a text block)
/// and tool-result envelopes the harness sends back to the model
/// (<c>message.content</c> is a list of <c>tool_result</c> blocks). Only the
/// former become user <see cref="Message"/>s; the latter carry each edit's
/// success/failure/denial (via <c>is_error</c>) which we correlate back to the
/// originating <c>tool_use</c> so failed or denied edits are not counted as
/// completed edits.
/// </summary>
internal sealed class ClaudeCodeReader : IHarnessReader
{
    private const string HarnessName = "claude-code";
    private const string Provider = "anthropic";
    private const string PlanMode = "plan";

    private readonly string _root;

    public ClaudeCodeReader(string? root = null) =>
        _root = root ?? DefaultRoot();

    public string Harness => HarnessName;

    public static string DefaultRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "projects");

    public bool IsAvailable() => Directory.Exists(_root);

    public IReadOnlyList<Session> Read(List<ReadError> errors)
    {
        var sessions = new List<Session>();

        foreach (var file in JsonlReader.EnumerateFiles(_root, "*.jsonl", "claude", errors))
        {
            var session = ReadSession(file, errors);
            if (session is not null)
            {
                sessions.Add(session);
            }
        }

        return sessions
            .OrderByDescending(session => session.UpdatedAt ?? session.CreatedAt)
            .ThenBy(session => session.Id)
            .ToList();
    }

    private static Session? ReadSession(string file, List<ReadError> errors)
    {
        var sessionId = Path.GetFileNameWithoutExtension(file);
        var builders = new Dictionary<string, MessageBuilder>();
        var order = new List<string>();
        var aborted = new HashSet<string>();
        // tool_use id -> whether its tool_result reported an error (failure or
        // user denial). Edits are only credited as completed when a non-error
        // result arrives.
        var toolResults = new Dictionary<string, bool>();
        var planMode = false;
        var anonymous = 0;
        DateTimeOffset? createdAt = null;
        DateTimeOffset? updatedAt = null;

        JsonlReader.ForEachRecord(file, "claude", errors, record =>
        {
            // Track the session's real time span across every timestamped record,
            // not just the ones that become messages — tool_result envelopes carry
            // no message but still bound when the session was active.
            if (JsonHelpers.FromIso8601(TryGetString(record, "timestamp")) is { } timestamp)
            {
                createdAt = createdAt is null || timestamp < createdAt ? timestamp : createdAt;
                updatedAt = updatedAt is null || timestamp > updatedAt ? timestamp : updatedAt;
            }

            switch (TryGetString(record, "type"))
            {
                case "permission-mode":
                    planMode = TryGetString(record, "permissionMode") == PlanMode;
                    break;
                case "assistant":
                    HandleAssistant(record, sessionId, planMode, builders, order);
                    break;
                case "user":
                    HandleUser(record, sessionId, planMode, builders, order, aborted, toolResults, ref anonymous);
                    break;
            }
        });

        if (order.Count == 0)
        {
            return null;
        }

        var session = new Session
        {
            Harness = HarnessName,
            Id = sessionId,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        foreach (var key in order)
        {
            session.Messages.Add(builders[key].Build(aborted.Contains(key), toolResults));
        }

        return session;
    }

    private static void HandleAssistant(
        JsonElement record,
        string sessionId,
        bool planMode,
        Dictionary<string, MessageBuilder> builders,
        List<string> order)
    {
        if (!record.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var id = TryGetString(message, "id");
        if (id is null)
        {
            return;
        }

        // Claude Code logs harness-generated notices ("Not logged in",
        // "No response requested") as assistant messages with model
        // "<synthetic>". These are not real model usage, so keep them out of
        // the scorecard.
        var model = TryGetString(message, "model");
        if (model == "<synthetic>")
        {
            return;
        }

        if (!builders.TryGetValue(id, out var builder))
        {
            builder = new MessageBuilder
            {
                Id = id,
                SessionId = sessionId,
                Role = "assistant",
                ProviderId = Provider,
                ModelId = model,
                Mode = planMode ? PlanMode : null,
                CreatedAt = JsonHelpers.FromIso8601(TryGetString(record, "timestamp")),
                Tokens = ReadUsage(message)
            };
            builders[id] = builder;
            order.Add(id);
        }

        CollectEditParts(message, builder);
    }

    private static void HandleUser(
        JsonElement record,
        string sessionId,
        bool planMode,
        Dictionary<string, MessageBuilder> builders,
        List<string> order,
        HashSet<string> aborted,
        Dictionary<string, bool> toolResults,
        ref int anonymous)
    {
        // A user turn that interrupted an assistant response names the aborted
        // assistant message; mirror opencode's "MessageAbortedError" signal onto it.
        if (TryGetString(record, "interruptedMessageId") is { } interruptedId)
        {
            aborted.Add(interruptedId);
        }

        // Claude reuses type:"user" for tool-result envelopes the harness feeds
        // back to the model. Record each result's status (for edit correlation)
        // but do not let it become a user message — only genuine human text does.
        CollectToolResults(record, toolResults);
        if (!HasUserText(record))
        {
            return;
        }

        var key = TryGetString(record, "uuid") ?? $"user-{anonymous++}";
        if (builders.ContainsKey(key))
        {
            return;
        }

        builders[key] = new MessageBuilder
        {
            Id = key,
            SessionId = sessionId,
            Role = "user",
            Mode = planMode ? PlanMode : null,
            CreatedAt = JsonHelpers.FromIso8601(TryGetString(record, "timestamp"))
        };
        order.Add(key);
    }

    /// <summary>
    /// True when a user record carries actual human text. Claude stores prompts
    /// either as a plain string <c>message.content</c> or as a content array that
    /// includes a <c>text</c> block; tool-result envelopes contain only
    /// <c>tool_result</c> blocks and return false.
    /// </summary>
    private static bool HasUserText(JsonElement record)
    {
        if (!record.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("content", out var content))
        {
            return false;
        }

        return content.ValueKind switch
        {
            JsonValueKind.String => !string.IsNullOrWhiteSpace(content.GetString()),
            JsonValueKind.Array => content.EnumerateArray().Any(block =>
                block.ValueKind == JsonValueKind.Object && TryGetString(block, "type") == "text"),
            _ => false
        };
    }

    /// <summary>
    /// Records the success/failure of each <c>tool_result</c> block (keyed by the
    /// originating <c>tool_use</c> id) so completed-edit accounting can exclude
    /// failed or user-denied edits, which Claude marks with <c>is_error: true</c>.
    /// </summary>
    private static void CollectToolResults(JsonElement record, Dictionary<string, bool> toolResults)
    {
        if (!record.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object
                || TryGetString(block, "type") != "tool_result"
                || TryGetString(block, "tool_use_id") is not { } toolId)
            {
                continue;
            }

            var isError = block.TryGetProperty("is_error", out var flag)
                && flag.ValueKind == JsonValueKind.True;

            // If any result line for this tool reports an error, treat the edit as
            // not completed (results stream once per tool_use, but be defensive).
            toolResults[toolId] = toolResults.GetValueOrDefault(toolId) || isError;
        }
    }

    private static void CollectEditParts(JsonElement message, MessageBuilder builder)
    {
        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object
                || TryGetString(block, "type") != "tool_use"
                || TryGetString(block, "id") is not { } toolId)
            {
                continue;
            }

            if (MapEditTool(TryGetString(block, "name")) is { } tool)
            {
                builder.EditParts[toolId] = tool;
            }
        }
    }

    /// <summary>
    /// Maps Claude Code editing tools onto the canonical tool names the metrics
    /// engine recognizes (<see cref="HtmlReporter"/>.IsEditingTool). Non-editing
    /// tools return null and are ignored.
    /// </summary>
    private static string? MapEditTool(string? name) => name switch
    {
        "Edit" or "MultiEdit" or "NotebookEdit" => "edit",
        "Write" => "write",
        _ => null
    };

    private static TokenUsage? ReadUsage(JsonElement message)
    {
        if (!message.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new TokenUsage(GetLong(usage, "input_tokens"), GetLong(usage, "output_tokens"));
    }

    private static long? GetLong(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
            ? number
            : null;

    private static string? TryGetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private sealed class MessageBuilder
    {
        public required string Id { get; init; }
        public required string SessionId { get; init; }
        public string? Role { get; init; }
        public string? ProviderId { get; init; }
        public string? ModelId { get; init; }
        public string? Mode { get; init; }
        public DateTimeOffset? CreatedAt { get; init; }
        public TokenUsage? Tokens { get; init; }

        /// <summary>Edit tool-use id → canonical tool name, deduped across streamed lines.</summary>
        public Dictionary<string, string> EditParts { get; } = [];

        public Message Build(bool aborted, IReadOnlyDictionary<string, bool> toolResults)
        {
            var message = new Message
            {
                Id = Id,
                SessionId = SessionId,
                Role = Role,
                ProviderId = ProviderId,
                ModelId = ModelId,
                Mode = Mode,
                ErrorName = aborted ? "MessageAbortedError" : null,
                CreatedAt = CreatedAt,
                CompletedAt = CreatedAt,
                Tokens = Tokens
            };

            foreach (var (toolId, tool) in EditParts)
            {
                message.Parts.Add(new Part
                {
                    Id = toolId,
                    MessageId = Id,
                    Tool = tool,
                    ToolStatus = EditStatus(toolId, toolResults),
                    StartedAt = CreatedAt
                });
            }

            return message;
        }

        /// <summary>
        /// An edit only counts as "completed" once Claude returns a non-error
        /// tool_result for it. A result flagged <c>is_error</c> (failure or user
        /// denial) is "error"; a tool_use with no result yet (interrupted or
        /// truncated turn) stays "pending". Only "completed" feeds the edit metrics.
        /// </summary>
        private static string EditStatus(string toolId, IReadOnlyDictionary<string, bool> toolResults) =>
            toolResults.TryGetValue(toolId, out var isError)
                ? isError ? "error" : "completed"
                : "pending";
    }
}
