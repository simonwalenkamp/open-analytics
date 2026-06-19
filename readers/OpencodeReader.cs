using System.Text.Json;
using Microsoft.Data.Sqlite;
using OpenAnalytics.Helpers;
using OpenAnalytics.models;

namespace OpenAnalytics.Readers;

internal sealed class OpencodeReader : IHarnessReader
{
    private const string HarnessName = "opencode";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string _storagePath;

    public OpencodeReader(string? storagePath = null) =>
        _storagePath = storagePath ?? DefaultStoragePath();

    public string Harness => HarnessName;

    public static string DefaultStoragePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "opencode", "opencode.db");

    public bool IsAvailable() => File.Exists(_storagePath);

    public IReadOnlyList<Session> Read(List<ReadError> errors)
    {
        using var connection = new SqliteConnection(BuildReadOnlyConnectionString(_storagePath));
        connection.Open();

        var sessions = ReadSessions(connection);
        var messagesBySession = ReadMessagesBySession(connection, errors);
        var partsByMessage = ReadPartsByMessage(connection, errors);

        AttachMessagesAndParts(sessions, messagesBySession, partsByMessage);

        return SortSessions(sessions);
    }

    private static IReadOnlyDictionary<string, List<Message>> ReadMessagesBySession(
        SqliteConnection connection,
        List<ReadError> errors) =>
        ReadMessages(connection, errors)
            .GroupBy(message => message.SessionId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(message => message.CreatedAt).ThenBy(message => message.Id).ToList());

    private static IReadOnlyDictionary<string, List<Part>> ReadPartsByMessage(
        SqliteConnection connection,
        List<ReadError> errors) =>
        ReadParts(connection, errors)
            .GroupBy(part => part.MessageId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(part => part.StartedAt).ThenBy(part => part.Id).ToList());

    private static List<Session> SortSessions(IEnumerable<Session> sessions) =>
        sessions
            .OrderByDescending(session => session.UpdatedAt ?? session.CreatedAt)
            .ThenBy(session => session.Id)
            .ToList();

    private static string BuildReadOnlyConnectionString(string dataSource) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = dataSource,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

    private static void AttachMessagesAndParts(
        IEnumerable<Session> sessions,
        IReadOnlyDictionary<string, List<Message>> messagesBySession,
        IReadOnlyDictionary<string, List<Part>> partsByMessage)
    {
        foreach (var session in sessions)
        {
            if (!messagesBySession.TryGetValue(session.Id, out var messages))
            {
                continue;
            }

            foreach (var message in messages)
            {
                if (partsByMessage.TryGetValue(message.Id, out var parts))
                {
                    message.Parts.AddRange(parts);
                }
            }

            session.Messages.AddRange(messages);
        }
    }

    private static List<Session> ReadSessions(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT
                                  id,
                                  summary_additions,
                                  summary_deletions,
                                  time_created,
                                  time_updated,
                                  parent_id,
                                  agent,
                                  revert
                              FROM session
                              """;

        var result = new List<Session>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(ReadSession(reader));
        }

        return result;
    }

    private static Session ReadSession(SqliteDataReader reader)
    {
        var id = reader.GetString(0);

        var revert = SqliteHelper.GetString(reader, 7);
        return new Session
        {
            Harness = HarnessName,
            Id = id,
            Summary = new SessionSummary(SqliteHelper.GetInt(reader, 1), SqliteHelper.GetInt(reader, 2)),
            CreatedAt = JsonHelpers.FromUnixMilliseconds(SqliteHelper.GetLong(reader, 3)),
            UpdatedAt = JsonHelpers.FromUnixMilliseconds(SqliteHelper.GetLong(reader, 4)),
            ParentId = SqliteHelper.GetString(reader, 5),
            Agent = SqliteHelper.GetString(reader, 6),
            Reverted = HasRevert(revert)
        };
    }

    private static bool HasRevert(string? revert) =>
        !string.IsNullOrWhiteSpace(revert) && revert != "{}" && revert != "null";

    private static List<Message> ReadMessages(SqliteConnection connection, List<ReadError> errors)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT id, session_id, time_created, data
                              FROM message
                              """;

        var result = new List<Message>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var sessionId = reader.GetString(1);
            var created = SqliteHelper.GetLong(reader, 2);
            var source = $"sqlite:message/{id}";
            var file = ReadJson<MessageFile>(source, reader.GetString(3), errors);
            if (file is null)
            {
                continue;
            }

            file.Id ??= id;
            file.SessionId ??= sessionId;
            file.Time ??= new TimeFile { Created = created };

            var message = file.ToMessage();
            if (message is not null)
            {
                result.Add(message);
            }
        }

        return result;
    }

    private static List<Part> ReadParts(SqliteConnection connection, List<ReadError> errors)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT id, message_id, time_created, data
                              FROM part
                              """;

        var result = new List<Part>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var messageId = reader.GetString(1);
            var created = SqliteHelper.GetLong(reader, 2);
            var source = $"sqlite:part/{id}";
            var file = ReadJson<PartFile>(source, reader.GetString(3), errors);
            if (file is null)
            {
                continue;
            }

            file.Id ??= id;
            file.MessageId ??= messageId;
            file.Time ??= new PartTimeFile { Start = created };

            var part = file.ToPart();
            if (part is not null)
            {
                result.Add(part);
            }
        }

        return result;
    }

    private static T? ReadJson<T>(string source, string json, List<ReadError> errors)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            errors.Add(new ReadError(source, ex.Message));
            return default;
        }
    }
}
