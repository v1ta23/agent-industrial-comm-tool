using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace industrial_comm_tool;

public sealed class AgentWorkbenchForm : Form
{
    private const string WorkbenchUrl = AgentAppRuntime.WorkbenchUrl;
    private static readonly Size DefaultWorkbenchSize = new(1560, 960);
    private static readonly Size MinimumWorkbenchSize = new(1080, 700);
    private const int OwnerWidthExtra = 360;
    private const int OwnerHeightExtra = 160;
    private const int ScreenMargin = 32;
    private static readonly string WebViewUserDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "industrial-comm-tool",
        "agent-workbench-webview2");
    private static readonly object EnvironmentLock = new();
    private static Task<CoreWebView2Environment>? s_environmentTask;

    private readonly WebView2 _webView;
    private readonly Panel _messagePanel;
    private readonly Label _messageTitle;
    private readonly Label _messageBody;
    private Task? _loadTask;
    private bool _isLoaded;

    public AgentWorkbenchForm()
    {
        Text = "Agent 工作台";
        StartPosition = FormStartPosition.CenterParent;
        Size = DefaultWorkbenchSize;
        MinimumSize = MinimumWorkbenchSize;

        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
        };
        _webView.NavigationCompleted += WebViewOnNavigationCompleted;

        _messageTitle = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
            ForeColor = Color.FromArgb(25, 28, 30),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _messageBody = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular),
            ForeColor = Color.FromArgb(67, 70, 84),
            TextAlign = ContentAlignment.TopLeft,
        };
        _messagePanel = BuildMessagePanel(_messageTitle, _messageBody);

        Controls.Add(_webView);
        Controls.Add(_messagePanel);
    }

    public static void WarmUpRuntime()
    {
        _ = GetWebViewEnvironmentAsync().ContinueWith(task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }

    public void EnsureLoadStarted()
    {
        if (!_isLoaded && !IsLoadRunning())
        {
            _loadTask = LoadWorkbenchAsync();
        }
    }

    public void CenterOverOwner(Form owner)
    {
        var ownerBounds = owner.Bounds;
        var workingArea = Screen.FromControl(owner).WorkingArea;
        EnsurePreferredSize(ownerBounds, workingArea);

        var x = ownerBounds.Left + (ownerBounds.Width - Width) / 2;
        var y = ownerBounds.Top + (ownerBounds.Height - Height) / 2;
        Location = new Point(
            Clamp(x, workingArea.Left, workingArea.Right - Width),
            Clamp(y, workingArea.Top, workingArea.Bottom - Height));
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (Visible)
        {
            EnsureLoadStarted();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    private async Task LoadWorkbenchAsync()
    {
        _isLoaded = false;

        if (!await EnsureWebViewRuntimeAsync())
        {
            return;
        }

        if (!await EnsureAgentAppAsync())
        {
            return;
        }

        try
        {
            ShowMessage("正在打开 Agent 工作台", $"WebView2、Agent 工作台地址和 /api/health 都通过了。\r\n\r\n正在打开：\r\n{WorkbenchUrl}");
            _webView.CoreWebView2.Navigate(WorkbenchUrl);
        }
        catch (Exception ex)
        {
            ShowMessage(
                "Agent 页面打开失败",
                $"WebView2 Runtime 已通过，服务检查也通过，但导航到工作台页面时失败。\r\n\r\n工作台地址：\r\n{WorkbenchUrl}\r\n\r\n先看日志：\r\n{GetWorkbenchLogHint()}\r\n\r\n详细信息：{ex.Message}");
        }
    }

    private async Task<bool> EnsureWebViewRuntimeAsync()
    {
        try
        {
            ShowMessage("正在检查 WebView2 Runtime", "这是窗口里显示网页用的运行环境。先把它确认好，再检查 4317 上的 Agent 工作台。");
            var environment = await GetWebViewEnvironmentAsync();
            await _webView.EnsureCoreWebView2Async(environment);
            return true;
        }
        catch (Exception ex)
        {
            ShowMessage(
                "WebView2 Runtime 不可用",
                $"这一步还没检查前端和后端，因为网页运行环境先没通过。\r\n\r\n请安装或修复 Microsoft Edge WebView2 Runtime，然后重新点 Agent 工作台。\r\n\r\n详细信息：{ex.Message}");
            return false;
        }
    }

    private async Task<bool> EnsureAgentAppAsync()
    {
        try
        {
            ShowMessage(
                "正在检查 Agent 工作台服务",
                $"接下来只检查 4317 这一套：\r\n\r\n工作台地址：\r\n{WorkbenchUrl}\r\n\r\n后端 /api/health：\r\n{AgentAppRuntime.BackendHealthUrl}");
            await AgentAppRuntime.EnsureRunningAsync(status => ShowMessage("正在检查 Agent 工作台服务", status));
            return true;
        }
        catch (AgentAppStartupException ex)
        {
            ShowMessage(ex.Title, ex.UserMessage);
            return false;
        }
        catch (Exception ex)
        {
            ShowMessage(
                "Agent 服务诊断失败",
                $"WebView2 Runtime 已通过，但检查 Agent 工作台时出错。\r\n\r\n工作台地址：\r\n{WorkbenchUrl}\r\n\r\n后端 /api/health：\r\n{AgentAppRuntime.BackendHealthUrl}\r\n\r\n详细信息：{ex.Message}");
            return false;
        }
    }

    private void WebViewOnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            _isLoaded = true;
            HideMessage();
            return;
        }

        _isLoaded = false;
        ShowMessage(
            "Agent 页面打开失败",
            $"WebView2 Runtime 已通过，4317 检查也通过了，但工作台页面导航失败。\r\n\r\n工作台地址：\r\n{WorkbenchUrl}\r\n\r\nWebView2 状态：{e.WebErrorStatus}\r\n\r\n可能是 4317 刚启动后又退出，或 /workbench 静态页面没发出来。\r\n\r\n先看日志：\r\n{GetWorkbenchLogHint()}");
    }

    private static Panel BuildMessagePanel(Label title, Label body)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 249, 251),
            Visible = false,
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));

        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(26),
        };

        var cardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        cardLayout.Controls.Add(title, 0, 0);
        cardLayout.Controls.Add(body, 0, 1);
        card.Controls.Add(cardLayout);
        layout.Controls.Add(card, 1, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private void ShowMessage(string title, string body)
    {
        _messageTitle.Text = title;
        _messageBody.Text = body;
        _messagePanel.Visible = true;
        _messagePanel.BringToFront();
    }

    private void HideMessage()
    {
        _messagePanel.Visible = false;
    }

    private bool IsLoadRunning()
    {
        return _loadTask is { IsCompleted: false };
    }

    private static string GetWorkbenchLogHint()
    {
        var appPath = AgentAppRuntime.GetAgentAppPath();
        return string.Join(
            "\r\n",
            Path.Combine(appPath, "backend-workbench.out.log"),
            Path.Combine(appPath, "backend-workbench.err.log"));
    }

    private static Task<CoreWebView2Environment> GetWebViewEnvironmentAsync()
    {
        lock (EnvironmentLock)
        {
            if (s_environmentTask is null || s_environmentTask.IsFaulted || s_environmentTask.IsCanceled)
            {
                s_environmentTask = CoreWebView2Environment.CreateAsync(userDataFolder: WebViewUserDataFolder);
            }

            return s_environmentTask;
        }
    }

    private static int Clamp(int value, int min, int max)
    {
        return max < min ? min : Math.Min(Math.Max(value, min), max);
    }

    private void EnsurePreferredSize(Rectangle ownerBounds, Rectangle workingArea)
    {
        if (WindowState != FormWindowState.Normal)
        {
            return;
        }

        var preferredSize = GetPreferredWindowSize(ownerBounds, workingArea);
        var maxSize = GetMaxWindowSize(workingArea);
        var targetSize = new Size(
            Width < preferredSize.Width ? preferredSize.Width : Math.Min(Width, maxSize.Width),
            Height < preferredSize.Height ? preferredSize.Height : Math.Min(Height, maxSize.Height));

        if (Size != targetSize)
        {
            Size = targetSize;
        }
    }

    private static Size GetPreferredWindowSize(Rectangle ownerBounds, Rectangle workingArea)
    {
        var maxSize = GetMaxWindowSize(workingArea);
        var preferredWidth = Math.Max(DefaultWorkbenchSize.Width, ownerBounds.Width + OwnerWidthExtra);
        var preferredHeight = Math.Max(DefaultWorkbenchSize.Height, ownerBounds.Height + OwnerHeightExtra);

        return new Size(Math.Min(preferredWidth, maxSize.Width), Math.Min(preferredHeight, maxSize.Height));
    }

    private static Size GetMaxWindowSize(Rectangle workingArea)
    {
        return new Size(
            Math.Max(MinimumWorkbenchSize.Width, workingArea.Width - ScreenMargin * 2),
            Math.Max(MinimumWorkbenchSize.Height, workingArea.Height - ScreenMargin * 2));
    }

}
