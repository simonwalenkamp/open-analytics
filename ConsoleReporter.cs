namespace OpenAnalytics;

internal static class ConsoleReporter
{
    public static void Summary(string storagePath, OpencodeStorage data)
    {
        Console.WriteLine("Open Analytics - opencode storage reader");
        Console.WriteLine($"Database: {storagePath}");
        Console.WriteLine();
        Console.WriteLine($"Sessions: {data.Sessions.Count}");
        Console.WriteLine($"Messages: {data.MessageCount}");
        Console.WriteLine($"Parts: {data.PartCount}");
        Console.WriteLine($"Read errors: {data.Errors.Count}");
        Console.WriteLine();

        if (data.Errors.Count <= 0) return;

        Console.WriteLine("Read Errors");
        foreach (var error in data.Errors.Take(10))
        {
            Console.WriteLine($"- {error.Path}: {error.Message}");
        }

        if (data.Errors.Count > 10)
        {
            Console.WriteLine($"- ... {data.Errors.Count - 10} more");
        }

        Console.WriteLine();
    }

    public static void Models(OpencodeStorage data)
    {
        Console.WriteLine();
        Console.WriteLine("Models");
        var modelGroups = data.Sessions
            .SelectMany(x => x.Messages)
            .Where(x => x.ModelId is not null)
            .GroupBy(x => new { x.ProviderId, x.ModelId })
            .OrderByDescending(x => x.Count());

        foreach (var model in modelGroups)
        {
            var sessionCount = model.Select(x => x.SessionId).Distinct().Count();
            var averageMessagesPerSession = sessionCount == 0 ? 0 : (decimal)model.Count() / sessionCount;
            Console.WriteLine(
                $"- {model.Key.ProviderId ?? "(unknown)"}/{model.Key.ModelId}: {model.Count()} messages, {sessionCount} sessions, {averageMessagesPerSession:F1} messages/session");
        }
    }
}