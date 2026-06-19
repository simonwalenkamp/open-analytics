using Microsoft.Data.Sqlite;
using OpenAnalytics.Helpers;
using OpenAnalytics.models;

namespace OpenAnalytics.Readers;

/// <summary>
/// Reads GitHub Copilot CLI usage from <c>~/.copilot/data.db</c>. Copilot only
/// persists session-level aggregates (the per-message <c>chats/</c> folders are
/// empty), so we synthesize a minimal two-message session per row: a user
/// message at <c>created_at</c> and an assistant message at <c>updated_at</c>
/// carrying the model, summed token totals, and the interruption flag. This
/// supports session count, tokens-in/out per session, session duration, and
/// interrupt rate; per-message, edit, latency, and plan metrics have no source
/// data and surface as n/a.
/// </summary>
internal sealed class CopilotReader : IHarnessReader
{
    private const string HarnessName = "copilot";

    private readonly string _storagePath;

    public CopilotReader(string? storagePath = null) =>
        _storagePath = storagePath ?? DefaultStoragePath();

    public string Harness => HarnessName;

    public static string DefaultStoragePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".copilot", "data.db");

    public bool IsAvailable() => File.Exists(_storagePath);

    public IReadOnlyList<Session> Read(List<ReadError> errors)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = _storagePath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT
                                  id,
                                  model,
                                  provider_id,
                                  was_interrupted,
                                  total_input_tokens,
                                  total_output_tokens,
                                  created_at,
                                  updated_at,
                                  agent
                              FROM sessions
                              """;

        var sessions = new List<Session>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            sessions.Add(BuildSession(reader));
        }

        return sessions
            .OrderByDescending(session => session.UpdatedAt ?? session.CreatedAt)
            .ThenBy(session => session.Id)
            .ToList();
    }

    private static Session BuildSession(SqliteDataReader reader)
    {
        var id = reader.GetString(0);
        var model = SqliteHelper.GetString(reader, 1);
        var provider = SqliteHelper.GetString(reader, 2);
        var interrupted = (SqliteHelper.GetLong(reader, 3) ?? 0) != 0;
        var inputTokens = SqliteHelper.GetLong(reader, 4);
        var outputTokens = SqliteHelper.GetLong(reader, 5);
        var createdAt = JsonHelpers.FromIso8601(SqliteHelper.GetString(reader, 6));
        var updatedAt = JsonHelpers.FromIso8601(SqliteHelper.GetString(reader, 7));
        var agent = SqliteHelper.GetString(reader, 8);

        var session = new Session
        {
            Harness = HarnessName,
            Id = id,
            Agent = agent,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        session.Messages.Add(new Message
        {
            Id = $"{id}-user",
            SessionId = id,
            Role = "user",
            CreatedAt = createdAt,
            CompletedAt = createdAt
        });

        session.Messages.Add(new Message
        {
            Id = $"{id}-assistant",
            SessionId = id,
            Role = "assistant",
            ProviderId = provider,
            ModelId = model,
            ErrorName = interrupted ? "MessageAbortedError" : null,
            CreatedAt = updatedAt ?? createdAt,
            CompletedAt = updatedAt ?? createdAt,
            Tokens = new TokenUsage(inputTokens, outputTokens)
        });

        return session;
    }
}
