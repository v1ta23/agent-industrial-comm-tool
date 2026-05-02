using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace industrial_comm_tool;

public sealed class AgentWorkbenchForm : Form
{
    private const string WorkbenchUrl = "http://127.0.0.1:5173";
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
        Size = new Size(1180, 820);
        MinimumSize = new Size(900, 600);

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

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (Visible && !_isLoaded && !IsLoadRunning())
        {
            _loadTask = LoadWorkbenchAsync();
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
        try
        {
            ShowMessage("正在打开 Agent 工作台", "第一次打开需要初始化 WebView2，后面再次打开会直接复用。");
            var environment = await GetWebViewEnvironmentAsync();
            await _webView.EnsureCoreWebView2Async(environment);
            _webView.CoreWebView2.Navigate(WorkbenchUrl);
        }
        catch (Exception ex)
        {
            ShowMessage(
                "Agent 工作台启动失败",
                $"无法启动 WebView2。\r\n\r\n请确认本机已安装 Microsoft Edge WebView2 Runtime。\r\n\r\n详细信息：{ex.Message}");
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
            "Agent 工作台没有启动",
            $"{WorkbenchUrl} 暂时访问不到。\r\n\r\n请先打开两个 PowerShell 窗口并运行：\r\n\r\n" +
            $"cd {GetAgentAppPath()}\r\n" +
            "npm run dev:backend\r\n\r\n" +
            $"cd {GetAgentAppPath()}\r\n" +
            "npm run dev:frontend");
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

    private static string GetAgentAppPath()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "agent-app"));
    }
}
