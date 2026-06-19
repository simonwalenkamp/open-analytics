using System.Diagnostics;
using OpenAnalytics;
using OpenAnalytics.models;
using OpenAnalytics.Readers;

IHarnessReader[] readers =
[
    new OpencodeReader(),
    new ClaudeCodeReader(),
    new CodexReader(),
    new CopilotReader()
];

var errors = new List<ReadError>();
var sessions = new List<Session>();

foreach (var reader in readers.Where(reader => reader.IsAvailable()))
{
    try
    {
        sessions.AddRange(reader.Read(errors));
    }
    catch (Exception exception)
    {
        // A single failing harness must not sink the whole report.
        errors.Add(new ReadError(reader.Harness, exception.Message));
    }
}

if (sessions.Count == 0)
{
    OpenReport(HtmlReporter.WriteNoData());
    return 1;
}

OpenReport(HtmlReporter.Write(new AnalyticsData(sessions, errors)));
return 0;

static void OpenReport(string path)
{
    try
    {
        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true
        });
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Report generated, but could not be opened: {exception.Message}");
        Console.Error.WriteLine(path);
    }
}
