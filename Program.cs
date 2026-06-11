using OpenAnalytics;

var homeFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var storagePath = Path.Combine(homeFolder, ".local", "share", "opencode", "opencode.db");

if (!File.Exists(storagePath))
{
    Console.Error.WriteLine($"Database path does not exist: {storagePath}");
    return 1;
}

var reader = new OpencodeStorageReader(storagePath);
var data = reader.Read();

ConsoleReporter.Summary(storagePath, data);

ConsoleReporter.Models(data);
return 0;