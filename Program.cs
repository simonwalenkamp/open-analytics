using System.Diagnostics;
using OpenAnalytics;

var homeFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var storagePath = Path.Combine(homeFolder, ".local", "share", "opencode", "opencode.db");

if (!File.Exists(storagePath))
{
    OpenReport(HtmlReporter.WriteMissingDatabase(storagePath));
    return 1;
}

var reader = new OpencodeStorageReader(storagePath);
var data = reader.Read();

OpenReport(HtmlReporter.Write(data));
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
