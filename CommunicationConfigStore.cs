using System.Text.Encodings.Web;
using System.Text.Json;

namespace industrial_comm_tool;

public static class CommunicationConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    public static string DefaultFilePath { get; } = Path.Combine(FindProjectRoot(), "data", "connection-config.json");

    public static CommunicationConnectionConfig Load(string? filePath = null)
    {
        var path = filePath ?? DefaultFilePath;
        if (!File.Exists(path))
        {
            return CommunicationConnectionConfig.Default;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CommunicationConnectionConfig>(json, JsonOptions)
            ?? CommunicationConnectionConfig.Default;
    }

    public static void Save(CommunicationConnectionConfig config, string? filePath = null)
    {
        var path = filePath ?? DefaultFilePath;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(path, json);
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
