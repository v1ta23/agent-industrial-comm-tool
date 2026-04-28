using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text;

namespace industrial_comm_tool;

public static class CommunicationLogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    public static string DefaultFilePath { get; } = Path.Combine(FindProjectRoot(), "data", "comm-logs.json");

    public static string DefaultExportDirectory { get; } = Path.Combine(FindProjectRoot(), "data", "exports");

    public static IReadOnlyList<CommunicationLogEntry> Load(string? filePath = null)
    {
        var path = filePath ?? DefaultFilePath;
        if (!File.Exists(path))
        {
            return Array.Empty<CommunicationLogEntry>();
        }

        var json = File.ReadAllText(path);
        var entries = JsonSerializer.Deserialize<List<CommunicationLogEntry>>(json, JsonOptions);
        if (entries is null)
        {
            return Array.Empty<CommunicationLogEntry>();
        }

        return entries;
    }

    public static void Save(IEnumerable<CommunicationLogEntry> entries, string? filePath = null)
    {
        var path = filePath ?? DefaultFilePath;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(entries, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static string ExportJson(IEnumerable<CommunicationLogEntry> entries, string? filePath = null)
    {
        var path = filePath ?? Path.Combine(DefaultExportDirectory, $"comm-logs-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(entries, JsonOptions);
        File.WriteAllText(path, json, Encoding.UTF8);
        return path;
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "industrial-comm-tool.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
