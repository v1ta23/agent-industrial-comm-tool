using System.Diagnostics;
using System.Net.Http;

namespace industrial_comm_tool;

internal static class AgentAppRuntime
{
    private const string BackendHealthUrl = "http://127.0.0.1:4317/api/health";
    private const string FrontendUrl = "http://127.0.0.1:5173";
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2),
    };
    private static readonly SemaphoreSlim StartLock = new(1, 1);
    private static bool s_backendStartedByApp;
    private static bool s_frontendStartedByApp;

    public static async Task EnsureRunningAsync(Action<string>? reportStatus = null, CancellationToken cancellationToken = default)
    {
        await StartLock.WaitAsync(cancellationToken);
        try
        {
            var appPath = GetAgentAppPath();
            if (!Directory.Exists(appPath))
            {
                throw new DirectoryNotFoundException($"找不到 Agent 应用目录：{appPath}");
            }

            if (!await CanGetAsync(BackendHealthUrl, cancellationToken))
            {
                reportStatus?.Invoke("后端服务没开，正在后台启动 4317。");
                StartNpmScript(appPath, "dev:backend", "backend-workbench");
                s_backendStartedByApp = true;
            }

            if (!await CanGetAsync(FrontendUrl, cancellationToken))
            {
                reportStatus?.Invoke("前端页面没开，正在后台启动 5173。");
                StartNpmScript(appPath, "dev:frontend", "frontend-workbench");
                s_frontendStartedByApp = true;
            }
        }
        finally
        {
            StartLock.Release();
        }

        reportStatus?.Invoke("正在等 Agent 工作台准备好。");
        await WaitUntilReadyAsync(cancellationToken);
    }

    private static async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(75);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await CanGetAsync(BackendHealthUrl, cancellationToken) &&
                await CanGetAsync(FrontendUrl, cancellationToken))
            {
                return;
            }

            await Task.Delay(1000, cancellationToken);
        }

        var startedText = s_backendStartedByApp || s_frontendStartedByApp
            ? "已尝试后台启动，但服务没有在 75 秒内准备好。"
            : "检测到服务还没有准备好。";
        throw new TimeoutException($"{startedText}\r\n\r\n日志位置：\r\n{GetAgentAppPath()}");
    }

    private static async Task<bool> CanGetAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static void StartNpmScript(string workingDirectory, string scriptName, string logPrefix)
    {
        var outLog = Path.Combine(workingDirectory, $"{logPrefix}.out.log");
        var errLog = Path.Combine(workingDirectory, $"{logPrefix}.err.log");
        var command = $"/c \"npm.cmd run {scriptName} > \"\"{outLog}\"\" 2> \"\"{errLog}\"\"\"";

        Process.Start(new ProcessStartInfo("cmd.exe", command)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
    }

    public static string GetAgentAppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "agent-app");
            if (File.Exists(Path.Combine(candidate, "package.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "agent-app"));
    }
}
