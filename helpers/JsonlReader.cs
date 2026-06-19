using System.Text.Json;
using OpenAnalytics.models;

namespace OpenAnalytics.Helpers;

/// <summary>
/// Shared line-by-line JSONL parsing for the file-based harness readers
/// (Claude Code, Codex). Each non-empty line is parsed independently; a line
/// that fails to parse is recorded as a <see cref="ReadError"/> and skipped so
/// one corrupt line never loses the rest of the file.
/// </summary>
internal static class JsonlReader
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Invokes <paramref name="onRecord"/> for each successfully parsed line in
    /// the file. The caller owns the <see cref="JsonElement"/> only for the
    /// duration of the callback (it is cloned by callers that retain it).
    /// </summary>
    public static void ForEachRecord(
        string path,
        string sourcePrefix,
        List<ReadError> errors,
        Action<JsonElement> onRecord)
    {
        IEnumerable<string> lines;
        try
        {
            lines = File.ReadLines(path);
        }
        catch (IOException ex)
        {
            errors.Add(new ReadError($"{sourcePrefix}:{path}", ex.Message));
            return;
        }

        var lineNumber = 0;
        foreach (var line in lines)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = TryParse(line, $"{sourcePrefix}:{path}#{lineNumber}", errors);
            if (document is null)
            {
                continue;
            }

            onRecord(document.RootElement);
        }
    }

    private static JsonDocument? TryParse(string line, string source, List<ReadError> errors)
    {
        try
        {
            return JsonDocument.Parse(line, DocumentOptions);
        }
        catch (JsonException ex)
        {
            errors.Add(new ReadError(source, ex.Message));
            return null;
        }
    }

    public static IEnumerable<string> EnumerateFiles(string root, string searchPattern)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateFiles(root, searchPattern, SearchOption.AllDirectories);
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }
}
