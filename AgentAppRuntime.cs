using System.Diagnostics;
using System.Net.Http;

namespace industrial_comm_tool;

internal static class AgentAppRuntime
{
    internal const string BackendHealthUrl = "http://127.0.0.1:4317/api/health";
    internal const string FrontendUrl = "http://127.0.0.1:5173";
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2),
    };
    private static readonly SemaphoreSlim StartLock = new(1, 1);

    public static async Task EnsureRunningAsync(Action<string>? reportStatus = null, CancellationToken cancellationToken = default)
    {
        var appPath = GetAgentAppPath();
        var backendStartRequested = false;
        var frontendStartRequested = false;

        await StartLock.WaitAsync(cancellationToken);
        try
        {
            if (!Directory.Exists(appPath))
            {
                throw AgentAppStartupException.MissingAgentApp(appPath);
            }

            reportStatus?.Invoke($"正在检查前端地址：\r\n{FrontendUrl}");
            if (!await CanGetAsync(FrontendUrl, cancellationToken))
            {
                reportStatus?.Invoke("前端地址没通，正在后台启动 5173。");
                StartWorkbenchService(appPath, "前端", "dev:frontend", "frontend-workbench");
                frontendStartRequested = true;
            }

            reportStatus?.Invoke($"正在检查后端 /api/health：\r\n{BackendHealthUrl}");
            if (!await CanGetAsync(BackendHealthUrl, cancellationToken))
            {
                reportStatus?.Invoke("后端 /api/health 没通，正在后台启动 4317。");
                StartWorkbenchService(appPath, "后端", "dev:backend", "backend-workbench");
                backendStartRequested = true;
            }
        }
        finally
        {
            StartLock.Release();
        }

        reportStatus?.Invoke("正在等前端页面和后端接口准备好。");
        await WaitUntilReadyAsync(appPath, frontendStartRequested, backendStartRequested, reportStatus, cancellationToken);
    }

    private static async Task WaitUntilReadyAsync(
        string appPath,
        bool frontendStartRequested,
        bool backendStartRequested,
        Action<string>? reportStatus,
        CancellationToken cancellationToken)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(75);
        var frontendReady = false;
        var backendReady = false;

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            frontendReady = await CanGetAsync(FrontendUrl, cancellationToken);
            backendReady = await CanGetAsync(BackendHealthUrl, cancellationToken);

            if (frontendReady && backendReady)
            {
                return;
            }

            reportStatus?.Invoke(BuildWaitingStatus(frontendReady, backendReady));
            await Task.Delay(1000, cancellationToken);
        }

        frontendReady = await CanGetAsync(FrontendUrl, cancellationToken);
        backendReady = await CanGetAsync(BackendHealthUrl, cancellationToken);

        throw AgentAppStartupException.ServiceNotReady(
            frontendReady,
            backendReady,
            frontendStartRequested,
            backendStartRequested,
            appPath);
    }

    private static string BuildWaitingStatus(bool frontendReady, bool backendReady)
    {
        if (!frontendReady && !backendReady)
        {
            return "前端地址和后端 /api/health 还没通，继续等。";
        }

        if (!frontendReady)
        {
            return "后端 /api/health 已通，前端地址还没通。";
        }

        return "前端地址已通，后端 /api/health 还没通。";
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
        bool frontendReady,
        bool backendReady,
        bool frontendStartRequested,
        bool backendStartRequested,
        string appPath)
    {
        if (!frontendReady && !backendReady)
        {
            var startText = frontendStartRequested || backendStartRequested
                ? "程序已经尝试自动启动前端和后端，但 75 秒内没等到。"
                : "程序检测到前端和后端都还没准备好。";

            return new AgentAppStartupException(
                "Agent 工作台服务没准备好",
                $"{startText}\r\n\r\n前端地址：\r\n{AgentAppRuntime.FrontendUrl}\r\n\r\n后端检查：\r\n{AgentAppRuntime.BackendHealthUrl}\r\n\r\n先看日志：\r\n{FormatLogHint(appPath, "frontend-workbench", "backend-workbench")}\r\n\r\n常见原因：npm 依赖没装、5173/4317 端口被占用，或启动脚本报错。",
                "Both frontend and backend endpoints are unavailable.");
        }

        if (!frontendReady)
        {
            var startText = frontendStartRequested
                ? "程序已经尝试自动启动前端，但 75 秒内没打开。"
                : "程序检测到前端地址还没准备好。";

            return new AgentAppStartupException(
                "前端页面没打开",
                $"{startText}\r\n\r\n后端 /api/health 已经通过，问题卡在前端地址：\r\n{AgentAppRuntime.FrontendUrl}\r\n\r\n先看日志：\r\n{FormatLogHint(appPath, "frontend-workbench")}\r\n\r\n常见原因：5173 端口被占用、前端构建失败，或 Vite preview 没起来。",
                "Frontend endpoint is unavailable.");
        }

        var backendStartText = backendStartRequested
            ? "程序已经尝试自动启动后端，但 75 秒内 /api/health 还是没通过。"
            : "程序检测到后端 /api/health 还没准备好。";

        return new AgentAppStartupException(
            "后端接口没准备好",
            $"{backendStartText}\r\n\r\n前端地址已经能访问，问题卡在后端检查：\r\n{AgentAppRuntime.BackendHealthUrl}\r\n\r\n前端页面可能能打开，但图表数据会空或报错。\r\n\r\n先看日志：\r\n{FormatLogHint(appPath, "backend-workbench")}\r\n\r\n常见原因：4317 端口被占用、后端启动失败，或日志文件读取报错。",
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
