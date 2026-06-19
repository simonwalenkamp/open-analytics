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

    private static readonly EnumerationOptions RecursiveEnumeration = new()
    {
        RecurseSubdirectories = true,
        // Skip files/directories we can't open instead of throwing partway
        // through enumeration.
        IgnoreInaccessible = true
    };

    /// <summary>
    /// Lazily enumerates files matching <paramref name="searchPattern"/> under
    /// <paramref name="root"/>. Enumeration is inherently lazy, so IO/permission
    /// failures surface while the caller iterates rather than up front;
    /// <see cref="EnumerationOptions.IgnoreInaccessible"/> skips the common cases,
    /// and any remaining failure is recorded as a <see cref="ReadError"/> and ends
    /// enumeration cleanly instead of aborting the whole reader.
    /// </summary>
    public static IEnumerable<string> EnumerateFiles(
        string root,
        string searchPattern,
        string sourcePrefix,
        List<ReadError> errors)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        IEnumerator<string> enumerator;
        try
        {
            enumerator = Directory.EnumerateFiles(root, searchPattern, RecursiveEnumeration).GetEnumerator();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add(new ReadError($"{sourcePrefix}:{root}", ex.Message));
            yield break;
        }

        using (enumerator)
        {
            while (true)
            {
                string current;
                try
                {
                    // MoveNext drives the lazy directory walk, so an unreadable
                    // subtree throws here rather than at the call site.
                    if (!enumerator.MoveNext())
                    {
                        yield break;
                    }

                    current = enumerator.Current;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    errors.Add(new ReadError($"{sourcePrefix}:{root}", ex.Message));
                    yield break;
                }

                yield return current;
            }
        }
    }
}
