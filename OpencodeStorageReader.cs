using Microsoft.Data.Sqlite;
using System.Text.Json;

sealed class OpencodeStorageReader(string storagePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public OpencodeStorage Read()
    {
        var errors = new List<ReadError>();
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = storagePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var sessions = ReadSessions(connection, errors);
        var messagesBySession = ReadMessages(connection, errors).GroupBy(x => x.SessionId)
            .ToDictionary(x => x.Key, x => x.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToList());
        var partsByMessage = ReadParts(connection, errors).GroupBy(x => x.MessageId)
            .ToDictionary(x => x.Key, x => x.OrderBy(p => p.StartedAt).ThenBy(p => p.Id).ToList());

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

        return new OpencodeStorage(
            sessions.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ThenBy(x => x.Id).ToList(), errors);
    }

    private static List<OpencodeSession> ReadSessions(SqliteConnection connection, List<ReadError> errors)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT id, project_id, slug, version, directory, title, summary_additions, summary_deletions, summary_files, time_created, time_updated
                              FROM session
                              """;

        var result = new List<OpencodeSession>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            result.Add(new OpencodeSession
            {
                Id = id,
                SourcePath = $"sqlite:session/{id}",
                ProjectId = GetString(reader, 1),
                Slug = GetString(reader, 2),
                Version = GetString(reader, 3),
                Directory = GetString(reader, 4),
                Title = GetString(reader, 5),
                Summary = new SessionSummary(GetInt(reader, 6), GetInt(reader, 7), GetInt(reader, 8)),
                CreatedAt = JsonHelpers.FromUnixMilliseconds(GetLong(reader, 9)),
                UpdatedAt = JsonHelpers.FromUnixMilliseconds(GetLong(reader, 10))
            });
        }

        return result;
    }

    private static List<OpencodeMessage> ReadMessages(SqliteConnection connection, List<ReadError> errors)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT id, session_id, time_created, data
                              FROM message
                              """;

        var result = new List<OpencodeMessage>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var sessionId = reader.GetString(1);
            var created = GetLong(reader, 2);
            var source = $"sqlite:message/{id}";
            var file = ReadJson<MessageFile>(source, reader.GetString(3), errors);
            if (file is null)
            {
                continue;
            }

            file.Id ??= id;
            file.SessionId ??= sessionId;
            file.Time ??= new TimeFile { Created = created };

            var message = file.ToMessage(source);
            if (message is not null)
            {
                result.Add(message);
            }
        }

        return result;
    }

    private static List<OpencodePart> ReadParts(SqliteConnection connection, List<ReadError> errors)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT id, message_id, session_id, time_created, data
                              FROM part
                              """;

        var result = new List<OpencodePart>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var messageId = reader.GetString(1);
            var sessionId = reader.GetString(2);
            var created = GetLong(reader, 3);
            var source = $"sqlite:part/{id}";
            var file = ReadJson<PartFile>(source, reader.GetString(4), errors);
            if (file is null)
            {
                continue;
            }

            file.Id ??= id;
            file.MessageId ??= messageId;
            file.SessionId ??= sessionId;
            file.Time ??= new PartTimeFile { Start = created };

            var part = file.ToPart(source);
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

    private static string? GetString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static int? GetInt(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private static long? GetLong(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
}