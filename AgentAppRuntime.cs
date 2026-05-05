using System.Diagnostics;
using System.Net.Http;

namespace industrial_comm_tool;

internal static class AgentAppRuntime
{
    internal const string BackendHealthUrl = "http://127.0.0.1:4317/api/health";
    internal const string WorkbenchUrl = "http://127.0.0.1:4317/workbench";
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2),
    };
    private static readonly SemaphoreSlim StartLock = new(1, 1);

    public static async Task EnsureRunningAsync(Action<string>? reportStatus = null, CancellationToken cancellationToken = default)
    {
        var appPath = GetAgentAppPath();
        var serviceStartRequested = false;

        await StartLock.WaitAsync(cancellationToken);
        try
        {
            if (!Directory.Exists(appPath))
            {
                throw AgentAppStartupException.MissingAgentApp(appPath);
            }

            reportStatus?.Invoke($"正在检查 Agent 工作台地址：\r\n{WorkbenchUrl}");
            var workbenchReady = await CanGetAsync(WorkbenchUrl, cancellationToken);

            reportStatus?.Invoke($"正在检查 Agent 后端：\r\n{BackendHealthUrl}");
            var backendReady = await CanGetAsync(BackendHealthUrl, cancellationToken);

            if (!workbenchReady && backendReady)
            {
                reportStatus?.Invoke("4317 后端已通，Agent 页面没出来，正在重新构建工作台页面。");
                StartWorkbenchService(appPath, "Agent 工作台页面", "build --workspace frontend", "frontend-build");
                serviceStartRequested = true;
            }
            else if (!workbenchReady)
            {
                reportStatus?.Invoke("Agent 工作台没通，正在后台启动 4317。");
                StartWorkbenchService(appPath, "Agent 工作台", "dev:workbench", "backend-workbench");
                serviceStartRequested = true;
            }
        }
        finally
        {
            StartLock.Release();
        }

        reportStatus?.Invoke("正在等 Agent 工作台准备好。");
        await WaitUntilReadyAsync(appPath, serviceStartRequested, reportStatus, cancellationToken);
    }

    private static async Task WaitUntilReadyAsync(
        string appPath,
        bool serviceStartRequested,
        Action<string>? reportStatus,
        CancellationToken cancellationToken)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(75);
        var workbenchReady = false;
        var backendReady = false;

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            workbenchReady = await CanGetAsync(WorkbenchUrl, cancellationToken);
            backendReady = await CanGetAsync(BackendHealthUrl, cancellationToken);

            if (workbenchReady && backendReady)
            {
                return;
            }

            reportStatus?.Invoke(BuildWaitingStatus(workbenchReady, backendReady));
            await Task.Delay(1000, cancellationToken);
        }

        workbenchReady = await CanGetAsync(WorkbenchUrl, cancellationToken);
        backendReady = await CanGetAsync(BackendHealthUrl, cancellationToken);

        throw AgentAppStartupException.ServiceNotReady(
            workbenchReady,
            backendReady,
            serviceStartRequested,
            appPath);
    }

    private static string BuildWaitingStatus(bool workbenchReady, bool backendReady)
    {
        if (!workbenchReady && !backendReady)
        {
            return "Agent 页面和 /api/health 还没通，继续等 4317。";
        }

        if (!workbenchReady)
        {
            return "4317 后端已通，/workbench 页面还没出来。";
        }

        return "/workbench 页面已通，/api/health 还没通。";
    }

    private static async Task<bool> CanGetAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static void StartWorkbenchService(string appPath, string serviceName, string scriptName, string logPrefix)
    {
        try
        {
            StartNpmScript(appPath, scriptName, logPrefix);
        }
        catch (Exception ex)
        {
            throw AgentAppStartupException.StartCommandFailed(serviceName, scriptName, appPath, logPrefix, ex);
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

internal sealed class AgentAppStartupException : Exception
{
    private AgentAppStartupException(string title, string userMessage, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Title = title;
        UserMessage = userMessage;
    }

    public string Title { get; }

    public string UserMessage { get; }

    public static AgentAppStartupException MissingAgentApp(string appPath)
    {
        return new AgentAppStartupException(
            "找不到 Agent 应用",
            $"程序没找到 Agent 工作台目录：\r\n{appPath}\r\n\r\n先确认项目里还有 agent-app\\package.json。",
            $"Missing agent-app directory: {appPath}");
    }

    public static AgentAppStartupException StartCommandFailed(
        string serviceName,
        string scriptName,
        string appPath,
        string logPrefix,
        Exception innerException)
    {
        return new AgentAppStartupException(
            $"{serviceName}启动命令执行失败",
            $"程序想启动 {serviceName}，但启动命令没跑起来。\r\n\r\n命令：npm run {scriptName}\r\n目录：{appPath}\r\n\r\n先看日志：\r\n{FormatLogHint(appPath, logPrefix)}\r\n\r\n详细信息：{innerException.Message}",
            $"Failed to start {serviceName} with npm run {scriptName}",
            innerException);
    }

    public static AgentAppStartupException ServiceNotReady(
        bool workbenchReady,
        bool backendReady,
        bool serviceStartRequested,
        string appPath)
    {
        if (!workbenchReady && !backendReady)
        {
            var startText = serviceStartRequested
                ? "程序已经尝试自动准备 Agent 工作台，但 75 秒内没等到。"
                : "程序检测到 Agent 工作台还没准备好。";

            return new AgentAppStartupException(
                "Agent 工作台服务没准备好",
                $"{startText}\r\n\r\n工作台地址：\r\n{AgentAppRuntime.WorkbenchUrl}\r\n\r\n后端检查：\r\n{AgentAppRuntime.BackendHealthUrl}\r\n\r\n先看日志：\r\n{FormatLogHint(appPath, "backend-workbench", "frontend-build")}\r\n\r\n常见原因：npm 依赖没装、4317 端口被占用，或前端构建/后端启动脚本报错。",
                "Workbench endpoints are unavailable.");
        }

        if (!workbenchReady)
        {
            return new AgentAppStartupException(
                "Agent 页面没打开",
                $"4317 后端 /api/health 已经通过，但 /workbench 页面没出来。\r\n\r\n工作台地址：\r\n{AgentAppRuntime.WorkbenchUrl}\r\n\r\n先看日志：\r\n{FormatLogHint(appPath, "backend-workbench", "frontend-build")}\r\n\r\n常见原因：前端 dist 没构建、旧的 4317 后端进程还没重启，或静态文件托管报错。",
                "Workbench page is unavailable.");
        }

        return new AgentAppStartupException(
            "后端接口没准备好",
            $"/workbench 页面已经能访问，但 /api/health 没通过。\r\n\r\n后端检查：\r\n{AgentAppRuntime.BackendHealthUrl}\r\n\r\n页面可能能打开，但图表数据会空或报错。\r\n\r\n先看日志：\r\n{FormatLogHint(appPath, "backend-workbench")}\r\n\r\n常见原因：4317 端口被其他服务占用、后端路由启动失败，或旧进程没重启。",
            "Backend health endpoint is unavailable.");
    }

    private static string FormatLogHint(string appPath, params string[] logPrefixes)
    {
        return string.Join(
            "\r\n",
            logPrefixes.SelectMany(prefix => new[]
            {
                Path.Combine(appPath, $"{prefix}.out.log"),
                Path.Combine(appPath, $"{prefix}.err.log"),
            }));
    }
}
