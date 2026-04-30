namespace industrial_comm_tool;

public partial class Form1 : Form
{
    private readonly Dictionary<string, Button> _navButtons = new();
    private readonly List<CommunicationLogEntry> _logEntries = new();
    private CommunicationConnectionConfig _connectionConfig = CommunicationConfigStore.Load();
    private Panel _contentPanel = null!;
    private Label _connectionBadge = null!;
    private Label _simulatorStateValue = null!;
    private Label _successCountValue = null!;
    private Label _failedCountValue = null!;
    private Label _lastDurationValue = null!;
    private Label _logCountValue = null!;
    private Label _statusLineLabel = null!;
    private TextBox _sendTextBox = null!;
    private TextBox _receiveTextBox = null!;
    private DataGridView _logGrid = null!;
    private int _logIndex;
    private int _successCount;
    private int _failedCount;
    private int _simulationStep;
    private bool _simulatorConnected;
    private string _simulatorStateText = "离线";
    private string _lastDurationText = "-- ms";

    private int S(int value) => (int)Math.Round(value * (DeviceDpi / 96f));

    private static readonly Color AppBackground = Color.FromArgb(248, 249, 251);
    private static readonly Color Surface = Color.White;
    private static readonly Color SurfaceLow = Color.FromArgb(243, 244, 246);
    private static readonly Color SidebarBackground = Color.White;
    private static readonly Color SidebarActive = Color.FromArgb(236, 244, 255);
    private static readonly Color SidebarText = Color.FromArgb(31, 47, 73);
    private static readonly Color SidebarMuted = Color.FromArgb(86, 99, 119);
    private static readonly Color Border = Color.FromArgb(195, 198, 214);
    private static readonly Color SubtleBorder = Color.FromArgb(231, 234, 241);
    private static readonly Color Primary = Color.FromArgb(0, 61, 155);
    private static readonly Color Success = Color.FromArgb(0, 132, 90);
    private static readonly Color Danger = Color.FromArgb(186, 26, 26);
    private static readonly Color Warning = Color.FromArgb(163, 53, 0);
    private static readonly Color TextMain = Color.FromArgb(25, 28, 30);
    private static readonly Color TextMuted = Color.FromArgb(67, 70, 84);
    private static readonly Color TerminalBackground = Color.FromArgb(28, 28, 28);
    private static readonly Color TerminalText = Color.FromArgb(221, 226, 235);

    public Form1()
    {
        InitializeComponent();
        BuildShell();
        ShowTcpPage();
    }

    private void BuildShell()
    {
        SuspendLayout();

        Text = "工业通信模拟调试平台";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(S(980), S(780));
        MinimumSize = new Size(S(780), S(600));
        BackColor = AppBackground;
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        Controls.Clear();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(64)));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildBody(), 0, 1);
        Controls.Add(root);

        ResumeLayout(true);
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(S(26), 0, S(24), 0),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(150)));

        var titleArea = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        titleArea.RowStyles.Add(new RowStyle(SizeType.Absolute, S(38)));
        titleArea.RowStyles.Add(new RowStyle(SizeType.Absolute, S(22)));

        var title = new Label
        {
            Dock = DockStyle.Fill,
            Text = "IndustrialControl Systems",
            Font = new Font(Font.FontFamily, 15F, FontStyle.Bold),
            ForeColor = TextMain,
            TextAlign = ContentAlignment.BottomLeft,
        };

        var subtitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "工业通信模拟调试平台",
            ForeColor = TextMuted,
            TextAlign = ContentAlignment.TopLeft,
        };

        var rightTools = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
        };
        rightTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rightTools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        rightTools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(88)));

        var searchBox = CreateSearchBox();
        var nodeLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "772-Alpha",
            ForeColor = SidebarMuted,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        var version = new Label
        {
            Dock = DockStyle.Fill,
            Text = "模拟 V0.2",
            ForeColor = Primary,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font(Font.FontFamily, 9F, FontStyle.Bold),
        };

        searchBox.Visible = false;
        rightTools.Controls.Add(searchBox, 0, 0);
        rightTools.Controls.Add(nodeLabel, 1, 0);
        rightTools.Controls.Add(version, 2, 0);

        titleArea.Controls.Add(title, 0, 0);
        titleArea.Controls.Add(subtitle, 0, 1);
        layout.Controls.Add(titleArea, 0, 0);
        layout.Controls.Add(rightTools, 1, 0);
        header.Controls.Add(layout);
        return header;
    }

    private static Control CreateSearchBox()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 14, 14, 12),
            BackColor = Surface,
        };

        var input = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = "Search...",
            ForeColor = Color.FromArgb(102, 112, 133),
            BackColor = SurfaceLow,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular),
            ReadOnly = true,
            TabStop = false,
        };

        host.Controls.Add(input);
        return host;
    }

    private Control BuildBody()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            ColumnCount = 2,
            RowCount = 1,
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(164)));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        body.Controls.Add(BuildSidebar(), 0, 0);

        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            Padding = new Padding(S(14), S(18), S(14), S(18)),
        };
        body.Controls.Add(_contentPanel, 1, 0);

        return body;
    }

    private Control BuildSidebar()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SidebarBackground,
            Padding = new Padding(S(12), S(22), S(8), S(16)),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(58)));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(312)));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var section = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        section.RowStyles.Add(new RowStyle(SizeType.Absolute, S(30)));
        section.RowStyles.Add(new RowStyle(SizeType.Absolute, S(22)));
        section.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "系统节点",
            ForeColor = TextMain,
            Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft,
        }, 0, 0);
        section.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "ID: 772-Alpha",
            ForeColor = SidebarMuted,
            TextAlign = ContentAlignment.TopLeft,
        }, 0, 1);
        layout.Controls.Add(section, 0, 0);

        var items = new[]
        {
            ("tcp", "▦  TCP 调试"),
            ("serial", "◇  串口调试"),
            ("modbus", "▣  Modbus"),
            ("logs", "▤  通信日志"),
            ("dashboard", "▥  数据看板"),
            ("settings", "⌁  模拟配置"),
        };

        var navStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };

        foreach (var item in items)
        {
            var button = CreateNavButton(item.Item1, item.Item2);
            navStack.Controls.Add(button);
            _navButtons[item.Item1] = button;
        }
        layout.Controls.Add(navStack, 0, 1);

        var hint = new Label
        {
            Dock = DockStyle.Bottom,
            ForeColor = SidebarMuted,
            Text = "当前只使用内置模拟设备。\r\n真实硬件接入放到后面做。",
            Height = S(80),
            TextAlign = ContentAlignment.BottomLeft,
        };
        layout.Controls.Add(hint, 0, 2);
        sidebar.Controls.Add(layout);

        return sidebar;
    }

    private Button CreateNavButton(string key, string text)
    {
        var button = new Button
        {
            Tag = key,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = SidebarBackground,
            ForeColor = SidebarText,
            Size = new Size(S(138), S(42)),
            Margin = new Padding(0, 0, 0, S(10)),
            Padding = new Padding(S(14), 0, 0, 0),
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderColor = Surface;
        button.FlatAppearance.BorderSize = 1;
        button.Click += (_, _) =>
        {
            if (key == "tcp")
            {
                ShowTcpPage();
                return;
            }

            if (key == "logs")
            {
                ShowLogConfigPage();
                return;
            }

            if (key == "dashboard")
            {
                ShowAgentDashboardPage();
                return;
            }

            ShowPlaceholderPage(text);
        };

        return button;
    }

    private void ShowTcpPage()
    {
        SetActiveNav("tcp");
        _simulatorConnected = false;
        _contentPanel.SuspendLayout();
        _contentPanel.Controls.Clear();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            ColumnCount = 1,
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(74)));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(166)));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(260)));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildPageTitle("TCP/IP 协议配置", "配置模拟 Socket，观察指令返回和通信日志。"), 0, 0);
        root.Controls.Add(BuildConnectionPanel(), 0, 1);
        root.Controls.Add(BuildCommandArea(), 0, 2);
        root.Controls.Add(BuildLogPanel(), 0, 3);

        _contentPanel.Controls.Add(root);
        _contentPanel.ResumeLayout(true);

        AddDemoRows();
    }

    private void ShowLogConfigPage()
    {
        SetActiveNav("logs");
        _contentPanel.SuspendLayout();
        _contentPanel.Controls.Clear();

        var totalValue = CreateMetricValue("0", TextMuted);
        var successValue = CreateMetricValue("0", Success);
        var failedValue = CreateMetricValue("0", Danger);
        var avgDurationValue = CreateMetricValue("-- ms", Primary);
        var logStatusLabel = CreateInlineStatus("等待读取日志");
        var configStatusLabel = CreateInlineStatus($"配置文件：{CommunicationConfigStore.DefaultFilePath}");
        var logGrid = CreateStoredLogGrid();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            ColumnCount = 1,
            RowCount = 3,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(74)));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(126)));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var overviewPanel = CreateSurfacePanel();
        var overviewLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(S(16), S(10), S(16), S(12)),
            ColumnCount = 1,
            RowCount = 2,
        };
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(28)));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        overviewLayout.Controls.Add(CreateSectionTitle("日志概览"), 0, 0);

        var metricGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 4,
            RowCount = 1,
        };
        for (var i = 0; i < 4; i++)
        {
            metricGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }

        metricGrid.Controls.Add(CreateOverviewMetricCard("总日志", totalValue), 0, 0);
        metricGrid.Controls.Add(CreateOverviewMetricCard("成功", successValue), 1, 0);
        metricGrid.Controls.Add(CreateOverviewMetricCard("异常", failedValue), 2, 0);
        metricGrid.Controls.Add(CreateOverviewMetricCard("平均耗时", avgDurationValue), 3, 0);
        overviewLayout.Controls.Add(metricGrid, 0, 1);
        overviewPanel.Controls.Add(overviewLayout);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, S(10), 0, 0),
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));

        var logPanel = CreateSurfacePanel();
        logPanel.Margin = new Padding(0, 0, S(8), 0);
        var logLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(S(16), S(12), S(16), S(16)),
            ColumnCount = 1,
            RowCount = 4,
        };
        logLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(32)));
        logLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(40)));
        logLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(40)));
        logLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var logPathRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 2,
            RowCount = 1,
        };
        logPathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(70)));
        logPathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        logPathRow.Controls.Add(CreateFieldLabel("JSON 路径"), 0, 0);
        var logPathBox = CreateInput(CommunicationLogStore.DefaultFilePath);
        logPathBox.ReadOnly = true;
        logPathBox.Dock = DockStyle.Fill;
        logPathBox.BackColor = SurfaceLow;
        logPathRow.Controls.Add(logPathBox, 1, 0);

        var logToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        var refreshButton = CreateSecondaryButton("刷新");
        var exportButton = CreatePrimaryButton("导出JSON");
        var openFolderButton = CreateSecondaryButton("打开目录");
        exportButton.Size = new Size(S(86), S(34));
        openFolderButton.Size = new Size(S(88), S(34));
        logStatusLabel.Width = S(360);
        logToolbar.Controls.Add(refreshButton);
        logToolbar.Controls.Add(exportButton);
        logToolbar.Controls.Add(openFolderButton);
        logToolbar.Controls.Add(logStatusLabel);

        logLayout.Controls.Add(CreateSectionTitle("通信日志列表"), 0, 0);
        logLayout.Controls.Add(logPathRow, 0, 1);
        logLayout.Controls.Add(logToolbar, 0, 2);
        logLayout.Controls.Add(logGrid, 0, 3);
        logPanel.Controls.Add(logLayout);

        var configPanel = CreateSurfacePanel();
        configPanel.Margin = new Padding(S(8), 0, 0, 0);
        var configLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(S(16), S(12), S(16), S(16)),
            ColumnCount = 1,
            RowCount = 8,
        };
        configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(32)));
        configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(54)));
        configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(54)));
        configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(54)));
        configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(54)));
        configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(54)));
        configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(52)));
        configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var deviceModelBox = CreateComboBox(new[] { "温控采集器", "压力传感器", "通用 PLC" });
        var ipBox = CreateInput(_connectionConfig.IpAddress);
        var portBox = CreateInput(_connectionConfig.Port);
        var commandBox = CreateInput(_connectionConfig.DefaultCommand);
        var displayModeBox = CreateComboBox(new[] { "HEX", "ASCII" });
        SetComboBoxValue(deviceModelBox, _connectionConfig.DeviceModel);
        SetComboBoxValue(displayModeBox, _connectionConfig.DisplayMode);

        var configButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Height = S(38),
        };
        var saveConfigButton = CreatePrimaryButton("保存配置");
        var loadConfigButton = CreateSecondaryButton("读取配置");
        saveConfigButton.Size = new Size(S(88), S(34));
        loadConfigButton.Size = new Size(S(88), S(34));
        configButtons.Controls.Add(saveConfigButton);
        configButtons.Controls.Add(loadConfigButton);

        configLayout.Controls.Add(CreateSectionTitle("常用连接配置"), 0, 0);
        configLayout.Controls.Add(CreateFieldStack("设备模型", deviceModelBox), 0, 1);
        configLayout.Controls.Add(CreateFieldStack("模拟地址", ipBox), 0, 2);
        configLayout.Controls.Add(CreateFieldStack("端口", portBox), 0, 3);
        configLayout.Controls.Add(CreateFieldStack("默认指令", commandBox), 0, 4);
        configLayout.Controls.Add(CreateFieldStack("显示格式", displayModeBox), 0, 5);
        configLayout.Controls.Add(configButtons, 0, 6);
        configLayout.Controls.Add(configStatusLabel, 0, 7);
        configPanel.Controls.Add(configLayout);

        content.Controls.Add(logPanel, 0, 0);
        content.Controls.Add(configPanel, 1, 0);

        root.Controls.Add(BuildPageTitle("通信日志与配置", "读取 JSON 日志，保存下一次 TCP 调试要用的常用参数。"), 0, 0);
        root.Controls.Add(overviewPanel, 0, 1);
        root.Controls.Add(content, 0, 2);
        _contentPanel.Controls.Add(root);
        _contentPanel.ResumeLayout(true);

        refreshButton.Click += (_, _) => RefreshLogRows("已刷新");
        exportButton.Click += (_, _) =>
        {
            var entries = CommunicationLogStore.Load();
            if (entries.Count == 0)
            {
                SetStatus(logStatusLabel, "没有日志可导出", Danger);
                return;
            }

            var confirmResult = MessageBox.Show(
                this,
                $"确认导出当前 {entries.Count} 条通信日志为 JSON 备份？\r\n\r\n导出目录：{CommunicationLogStore.DefaultExportDirectory}",
                "确认导出",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (confirmResult != DialogResult.Yes)
            {
                SetStatus(logStatusLabel, "已取消导出", TextMuted);
                return;
            }

            var exportPath = CommunicationLogStore.ExportJson(entries);
            SetStatus(logStatusLabel, $"已导出JSON：{exportPath}", Success);
        };
        openFolderButton.Click += (_, _) =>
        {
            var directory = Path.GetDirectoryName(CommunicationLogStore.DefaultFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", directory)
                {
                    UseShellExecute = true,
                });
            }
        };
        saveConfigButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(ipBox.Text) || string.IsNullOrWhiteSpace(portBox.Text))
            {
                SetStatus(configStatusLabel, "地址和端口不能为空", Danger);
                return;
            }

            if (!int.TryParse(portBox.Text.Trim(), out _))
            {
                SetStatus(configStatusLabel, "端口必须是数字", Danger);
                return;
            }

            _connectionConfig = new CommunicationConnectionConfig(
                deviceModelBox.Text,
                ipBox.Text.Trim(),
                portBox.Text.Trim(),
                commandBox.Text.Trim(),
                displayModeBox.Text);
            CommunicationConfigStore.Save(_connectionConfig);
            SetStatus(configStatusLabel, "配置已保存，回到 TCP 调试页会自动带上这些值", Success);
        };
        loadConfigButton.Click += (_, _) =>
        {
            _connectionConfig = CommunicationConfigStore.Load();
            SetComboBoxValue(deviceModelBox, _connectionConfig.DeviceModel);
            ipBox.Text = _connectionConfig.IpAddress;
            portBox.Text = _connectionConfig.Port;
            commandBox.Text = _connectionConfig.DefaultCommand;
            SetComboBoxValue(displayModeBox, _connectionConfig.DisplayMode);
            SetStatus(configStatusLabel, "配置已读取", Success);
        };

        RefreshLogRows("已读取");

        void RefreshLogRows(string message)
        {
            var entries = CommunicationLogStore.Load();
            PopulateStoredLogGrid(logGrid, entries);
            UpdateLogPageSummary(entries, totalValue, successValue, failedValue, avgDurationValue);
            SetStatus(logStatusLabel, $"{message}：{entries.Count} 条", TextMuted);
        }
    }

    private void ShowAgentDashboardPage()
    {
        SetActiveNav("dashboard");
        _contentPanel.SuspendLayout();
        _contentPanel.Controls.Clear();

        var entries = CommunicationLogStore.Load();
        var successCount = entries.Count(entry => entry.Status == "Success");
        var failedCount = entries.Count - successCount;
        var successRate = entries.Count == 0 ? "0%" : $"{Math.Round(successCount * 100d / entries.Count)}%";
        var durationEntries = entries.Where(entry => entry.DurationMs > 0).ToList();
        var avgDuration = durationEntries.Count == 0
            ? "-- ms"
            : $"{Math.Round(durationEntries.Average(entry => entry.DurationMs))} ms";

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            ColumnCount = 1,
            RowCount = 2,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(72)));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var panel = CreateSurfacePanel();
        panel.Padding = new Padding(S(16));

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(38)));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(92)));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 48));

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(86)));
        toolbar.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = $"已读取 {entries.Count} 条 JSON 日志。数据来自：{CommunicationLogStore.DefaultFilePath}",
            ForeColor = TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        var refreshButton = CreateSecondaryButton("刷新");
        refreshButton.Dock = DockStyle.Fill;
        refreshButton.Click += (_, _) => ShowAgentDashboardPage();
        toolbar.Controls.Add(refreshButton, 1, 0);
        layout.Controls.Add(toolbar, 0, 0);

        var metricGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(0, S(8), 0, S(8)),
        };
        for (var i = 0; i < 4; i++)
        {
            metricGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }
        metricGrid.Controls.Add(CreateOverviewMetricCard("总日志", CreateMetricValue(entries.Count.ToString(), TextMuted)), 0, 0);
        metricGrid.Controls.Add(CreateOverviewMetricCard("成功率", CreateMetricValue(successRate, Success)), 1, 0);
        metricGrid.Controls.Add(CreateOverviewMetricCard("失败次数", CreateMetricValue(failedCount.ToString(), failedCount > 0 ? Danger : TextMuted)), 2, 0);
        metricGrid.Controls.Add(CreateOverviewMetricCard("平均耗时", CreateMetricValue(avgDuration, Primary)), 3, 0);
        layout.Controls.Add(metricGrid, 0, 1);

        var chartGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 0, 0, S(8)),
        };
        chartGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        chartGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        chartGrid.Controls.Add(CreateNativeChartCard("响应耗时", new DurationTrendChart(durationEntries)), 0, 0);
        chartGrid.Controls.Add(CreateNativeChartCard("成功 / 失败", new StatusCountChart(successCount, failedCount)), 1, 0);
        layout.Controls.Add(chartGrid, 0, 2);

        var detailGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        detailGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        detailGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));

        var recentGrid = CreateStoredLogGrid();
        recentGrid.Margin = new Padding(0);
        PopulateStoredLogGrid(recentGrid, entries.Reverse().Take(8).ToList());
        detailGrid.Controls.Add(CreateNativeLogCard(recentGrid), 0, 0);
        detailGrid.Controls.Add(CreateNativeAdviceCard(entries), 1, 0);
        layout.Controls.Add(detailGrid, 0, 3);

        panel.Controls.Add(layout);
        root.Controls.Add(BuildPageTitle("数据看板", "软件内置看板，直接读取 JSON 日志，不需要先打开网页。"), 0, 0);
        root.Controls.Add(panel, 0, 1);

        _contentPanel.Controls.Add(root);
        _contentPanel.ResumeLayout(true);
    }

    private Control CreateNativeChartCard(string titleText, Control chart)
    {
        var panel = CreateSurfacePanel();
        panel.Margin = new Padding(0, 0, S(10), 0);
        panel.Padding = new Padding(S(14), S(12), S(14), S(12));

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(28)));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        chart.Dock = DockStyle.Fill;
        layout.Controls.Add(CreateSectionTitle(titleText), 0, 0);
        layout.Controls.Add(chart, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateNativeLogCard(DataGridView grid)
    {
        var panel = CreateSurfacePanel();
        panel.Margin = new Padding(0, 0, S(10), 0);
        panel.Padding = new Padding(S(14), S(12), S(14), S(12));

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(28)));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(CreateSectionTitle("最近通信日志"), 0, 0);
        layout.Controls.Add(grid, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateNativeAdviceCard(IReadOnlyList<CommunicationLogEntry> entries)
    {
        var panel = CreateSurfacePanel();
        panel.Padding = new Padding(S(14), S(12), S(14), S(12));

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(28)));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(CreateSectionTitle("排查建议"), 0, 0);

        var advice = new Label
        {
            Dock = DockStyle.Fill,
            Text = BuildNativeDashboardAdvice(entries),
            ForeColor = TextMain,
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Regular),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, S(8), 0, 0),
        };
        layout.Controls.Add(advice, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private static string BuildNativeDashboardAdvice(IReadOnlyList<CommunicationLogEntry> entries)
    {
        if (entries.Count == 0)
        {
            return "还没有通信日志。\r\n\r\n先回到 TCP 调试页，连接模拟设备并发送一次指令。";
        }

        var failedEntries = entries.Where(entry => entry.Status != "Success").ToList();
        if (failedEntries.Count == 0)
        {
            return "当前没有失败记录。\r\n\r\n可以继续观察响应耗时，或者下一步接串口 / Modbus 数据。";
        }

        var latestFailed = failedEntries.Last();
        var topError = failedEntries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ErrorType))
            .GroupBy(entry => entry.ErrorType)
            .OrderByDescending(group => group.Count())
            .FirstOrDefault();

        var errorText = topError is null ? "未知异常" : topError.Key;
        return $"当前有 {failedEntries.Count} 条失败记录。\r\n\r\n最新失败：{latestFailed.Content}\r\n\r\n主要问题：{errorText}\r\n\r\n先检查连接状态、IP、端口和设备是否在线。";
    }

    private Control BuildPageTitle(string titleText, string subtitleText)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            ColumnCount = 1,
            RowCount = 3,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, S(30)));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, S(18)));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, S(14)));

        var title = new Label
        {
            Dock = DockStyle.Fill,
            Text = titleText,
            Font = new Font(Font.FontFamily, 17F, FontStyle.Bold),
            ForeColor = TextMain,
            TextAlign = ContentAlignment.BottomLeft,
        };

        var subtitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = subtitleText,
            ForeColor = TextMuted,
            Font = new Font(Font.FontFamily, 9.5F, FontStyle.Regular),
            TextAlign = ContentAlignment.TopLeft,
        };

        var modeNotice = new Label
        {
            Dock = DockStyle.Fill,
            Text = "SYSTEM STATUS: NOMINAL  |  PLC CONNECTION: SIMULATED",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = SidebarMuted,
            BackColor = AppBackground,
            Padding = new Padding(S(12), 0, S(12), 0),
            Margin = new Padding(0, S(8), 0, 0),
        };

        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(subtitle, 0, 1);
        panel.Controls.Add(modeNotice, 0, 2);
        return panel;
    }

    private Control BuildSummaryPanel()
    {
        _statusLineLabel = new Label
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceLow,
            ForeColor = TextMuted,
            Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Regular),
            Padding = new Padding(S(10), 0, S(10), 0),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        UpdateStatusLine();
        return _statusLineLabel;
    }

    private Control BuildMetricsStrip()
    {
        _simulatorStateValue = CreateMetricValue("离线", TextMuted);
        _successCountValue = CreateMetricValue("0", Success);
        _failedCountValue = CreateMetricValue("0", Danger);
        _lastDurationValue = CreateMetricValue("-- ms", Primary);
        _logCountValue = CreateMetricValue("0", TextMuted);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 5,
            RowCount = 1,
            Padding = new Padding(0, S(4), 0, 0),
        };

        for (var i = 0; i < 5; i++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        }

        grid.Controls.Add(CreateMetricCard("状态", _simulatorStateValue), 0, 0);
        grid.Controls.Add(CreateMetricCard("成功", _successCountValue), 1, 0);
        grid.Controls.Add(CreateMetricCard("异常", _failedCountValue), 2, 0);
        grid.Controls.Add(CreateMetricCard("耗时", _lastDurationValue), 3, 0);
        grid.Controls.Add(CreateMetricCard("日志", _logCountValue), 4, 0);
        return grid;
    }

    private Control BuildConnectionPanel()
    {
        var panel = CreateSurfacePanel();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(S(14), S(10), S(14), S(10)),
            ColumnCount = 1,
            RowCount = 3,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(28)));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(54)));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(38)));

        var title = CreateSectionTitle("Socket 连接设置");
        layout.Controls.Add(title, 0, 0);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(142)));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(110)));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(68)));

        var deviceModelBox = CreateComboBox(new[] { "温控采集器", "压力传感器", "通用 PLC" });
        SetComboBoxValue(deviceModelBox, _connectionConfig.DeviceModel);
        var ipBox = CreateInput(_connectionConfig.IpAddress);
        var portBox = CreateInput(_connectionConfig.Port);

        var connectButton = CreatePrimaryButton("连接");
        var disconnectButton = CreateSecondaryButton("断开");

        _connectionBadge = new Label
        {
            Text = "未连接",
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(229, 233, 239),
            ForeColor = TextMuted,
            Dock = DockStyle.Top,
            Height = S(34),
        };

        var tip = new Label
        {
            Text = "内置模拟规则",
            ForeColor = TextMuted,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        connectButton.Click += (_, _) =>
        {
            _simulatorConnected = true;
            SetSimulatorState("在线", Success);
            _connectionBadge.Text = "已连接";
            _connectionBadge.BackColor = Color.FromArgb(220, 244, 232);
            _connectionBadge.ForeColor = Success;
            _receiveTextBox.Text = "模拟设备已上线，可以发送 01 03 00 00 00 02。";
            AddLogRow("TCP-Sim", "System", $"连接 {deviceModelBox.Text} {ipBox.Text}:{portBox.Text}", "Success", "12", "");
        };

        disconnectButton.Click += (_, _) =>
        {
            _simulatorConnected = false;
            SetSimulatorState("离线", TextMuted);
            _connectionBadge.Text = "未连接";
            _connectionBadge.BackColor = Color.FromArgb(229, 233, 239);
            _connectionBadge.ForeColor = TextMuted;
            _receiveTextBox.Text = "模拟设备已断开。";
            AddLogRow("TCP-Sim", "System", "断开模拟设备", "Success", "0", "");
        };

        fields.Controls.Add(CreateFieldStack("设备模型", deviceModelBox), 0, 0);
        fields.Controls.Add(CreateFieldStack("模拟地址", ipBox), 1, 0);
        fields.Controls.Add(CreateFieldStack("端口", portBox), 2, 0);

        var actionRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
        };
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(88)));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(88)));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(104)));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        connectButton.Dock = DockStyle.Top;
        disconnectButton.Dock = DockStyle.Top;
        _connectionBadge.Dock = DockStyle.Top;
        actionRow.Controls.Add(connectButton, 0, 0);
        actionRow.Controls.Add(disconnectButton, 1, 0);
        actionRow.Controls.Add(_connectionBadge, 2, 0);
        actionRow.Controls.Add(tip, 3, 0);

        layout.Controls.Add(fields, 0, 1);
        layout.Controls.Add(actionRow, 0, 2);
        panel.Controls.Add(layout);

        return panel;
    }

    private Control BuildCommandArea()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, S(10), 0, S(12)),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        grid.Controls.Add(BuildSendPanel(), 0, 0);
        grid.Controls.Add(BuildReceivePanel(), 1, 0);
        return grid;
    }

    private Control BuildSendPanel()
    {
        var panel = CreateSurfacePanel();
        panel.Margin = new Padding(0, 0, S(8), 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(S(14), S(10), S(14), S(12)),
            ColumnCount = 1,
            RowCount = 4,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(26)));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(28)));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(40)));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(CreateSectionTitle("指令发送"), 0, 0);

        var hexRadio = new RadioButton
        {
            Text = "HEX",
            Checked = true,
            ForeColor = TextMain,
            AutoSize = true,
        };
        var asciiRadio = new RadioButton
        {
            Text = "ASCII",
            ForeColor = TextMain,
            AutoSize = true,
        };
        asciiRadio.Checked = _connectionConfig.DisplayMode.Equals("ASCII", StringComparison.OrdinalIgnoreCase);
        hexRadio.Checked = !asciiRadio.Checked;

        var modePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        hexRadio.Margin = new Padding(0, S(5), S(12), 0);
        asciiRadio.Margin = new Padding(0, S(5), 0, 0);
        modePanel.Controls.Add(hexRadio);
        modePanel.Controls.Add(asciiRadio);

        var commandPresetBox = CreateComboBox(new[]
        {
            "读寄存器 | 01 03 00 00 00 02",
            "写寄存器 | 01 06 00 01 00 01",
            "超时 | FF FF",
            "状态 | STATUS",
        });
        var presetButton = CreateSecondaryButton("填入");
        var presetGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 0, 0, S(4)),
        };
        presetGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        presetGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(88)));
        presetButton.Dock = DockStyle.Top;
        presetButton.Margin = new Padding(S(6), 0, 0, 0);
        presetGrid.Controls.Add(commandPresetBox, 0, 0);
        presetGrid.Controls.Add(presetButton, 1, 0);

        _sendTextBox = new TextBox
        {
            Text = _connectionConfig.DefaultCommand,
            Multiline = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(252, 253, 255),
            ForeColor = TextMain,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point),
            Margin = new Padding(0),
        };

        var sendButton = CreatePrimaryButton("发送");
        var clearButton = CreateSecondaryButton("清空");

        var commandGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 2,
            RowCount = 1,
        };
        commandGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        commandGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(88)));

        var buttonStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(S(6), 0, 0, 0),
            ColumnCount = 1,
            RowCount = 4,
        };
        buttonStack.RowStyles.Add(new RowStyle(SizeType.Absolute, S(34)));
        buttonStack.RowStyles.Add(new RowStyle(SizeType.Absolute, S(10)));
        buttonStack.RowStyles.Add(new RowStyle(SizeType.Absolute, S(34)));
        buttonStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sendButton.Dock = DockStyle.Top;
        clearButton.Dock = DockStyle.Top;
        sendButton.Margin = new Padding(0);
        clearButton.Margin = new Padding(0);
        buttonStack.Controls.Add(sendButton, 0, 0);
        buttonStack.Controls.Add(clearButton, 0, 2);

        presetButton.Click += (_, _) =>
        {
            var text = commandPresetBox.Text;
            var separatorIndex = text.IndexOf('|');
            var command = separatorIndex >= 0 ? text[(separatorIndex + 1)..].Trim() : text.Trim();
            _sendTextBox.Text = command;
            asciiRadio.Checked = command.Equals("STATUS", StringComparison.OrdinalIgnoreCase);
            hexRadio.Checked = !asciiRadio.Checked;
        };

        sendButton.Click += (_, _) =>
        {
            var content = _sendTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                AddLogRow("TCP-Sim", "Send", "空指令", "Failed", "0", "EmptyCommand");
                _receiveTextBox.Text = "没有发送内容，模拟设备不会返回。";
                return;
            }

            if (!_simulatorConnected)
            {
                AddLogRow("TCP-Sim", "Send", content, "Failed", "0", "Disconnected");
                _receiveTextBox.Text = "模拟设备未连接，请先点连接。";
                return;
            }

            var mode = hexRadio.Checked ? "HEX" : "ASCII";
            AddLogRow("TCP-Sim", "Send", $"{mode}: {content}", "Success", "0", "");

            var response = CreateSimulatedResponse(content, mode);
            _receiveTextBox.Text = response.Content;
            AddLogRow("TCP-Sim", "Receive", response.Content, response.Status, response.DurationMs, response.ErrorType);
        };

        clearButton.Click += (_, _) => _sendTextBox.Clear();

        commandGrid.Controls.Add(_sendTextBox, 0, 0);
        commandGrid.Controls.Add(buttonStack, 1, 0);

        layout.Controls.Add(modePanel, 0, 1);
        layout.Controls.Add(presetGrid, 0, 2);
        layout.Controls.Add(commandGrid, 0, 3);
        panel.Controls.Add(layout);

        return panel;
    }

    private Control BuildReceivePanel()
    {
        var panel = CreateSurfacePanel();
        panel.Margin = new Padding(S(8), 0, 0, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(S(14), S(10), S(14), S(12)),
            ColumnCount = 1,
            RowCount = 3,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(26)));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(26)));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(CreateSectionTitle("原始数据流"), 0, 0);

        var status = new Label
        {
            Text = "最近一次设备响应，按原始内容显示",
            ForeColor = TextMuted,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _receiveTextBox = new TextBox
        {
            ReadOnly = true,
            Multiline = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = TerminalBackground,
            ForeColor = TerminalText,
            Text = "等待设备返回...",
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point),
            Margin = new Padding(0),
        };

        layout.Controls.Add(status, 0, 1);
        layout.Controls.Add(_receiveTextBox, 0, 2);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildLogPanel()
    {
        var panel = CreateSurfacePanel();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(S(16), S(12), S(16), S(16)),
            ColumnCount = 1,
            RowCount = 2,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(34)));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = CreateSectionTitle("结构化通信日志");
        layout.Controls.Add(title, 0, 0);

        _logGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Surface,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = SubtleBorder,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = S(36),
            RowTemplate = { Height = S(32) },
        };
        _logGrid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceLow;
        _logGrid.ColumnHeadersDefaultCellStyle.ForeColor = TextMain;
        _logGrid.ColumnHeadersDefaultCellStyle.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
        _logGrid.DefaultCellStyle.BackColor = Surface;
        _logGrid.DefaultCellStyle.ForeColor = TextMain;
        _logGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 237, 255);
        _logGrid.DefaultCellStyle.SelectionForeColor = TextMain;
        _logGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);

        _logGrid.Columns.Add("Time", "时间");
        _logGrid.Columns.Add("Protocol", "协议");
        _logGrid.Columns.Add("Direction", "方向");
        _logGrid.Columns.Add("Content", "内容");
        _logGrid.Columns.Add("Status", "状态");
        _logGrid.Columns.Add("Duration", "耗时(ms)");
        _logGrid.Columns.Add("ErrorType", "异常类型");

        _logGrid.Columns["Time"]!.FillWeight = 95;
        _logGrid.Columns["Protocol"]!.FillWeight = 70;
        _logGrid.Columns["Direction"]!.FillWeight = 75;
        _logGrid.Columns["Content"]!.FillWeight = 320;
        _logGrid.Columns["Status"]!.FillWeight = 70;
        _logGrid.Columns["Duration"]!.FillWeight = 80;
        _logGrid.Columns["ErrorType"]!.FillWeight = 90;

        layout.Controls.Add(_logGrid, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private DataGridView CreateStoredLogGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Surface,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = SubtleBorder,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = S(36),
            RowTemplate = { Height = S(32) },
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceLow;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMain;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = TextMain;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 237, 255);
        grid.DefaultCellStyle.SelectionForeColor = TextMain;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);

        grid.Columns.Add("Time", "时间");
        grid.Columns.Add("Protocol", "协议");
        grid.Columns.Add("Direction", "方向");
        grid.Columns.Add("Content", "内容");
        grid.Columns.Add("Status", "状态");
        grid.Columns.Add("Duration", "耗时(ms)");
        grid.Columns.Add("ErrorType", "异常类型");

        grid.Columns["Time"]!.FillWeight = 95;
        grid.Columns["Protocol"]!.FillWeight = 70;
        grid.Columns["Direction"]!.FillWeight = 75;
        grid.Columns["Content"]!.FillWeight = 260;
        grid.Columns["Status"]!.FillWeight = 70;
        grid.Columns["Duration"]!.FillWeight = 80;
        grid.Columns["ErrorType"]!.FillWeight = 90;

        return grid;
    }

    private void PopulateStoredLogGrid(DataGridView grid, IReadOnlyList<CommunicationLogEntry> entries)
    {
        grid.Rows.Clear();

        foreach (var entry in entries)
        {
            var rowIndex = grid.Rows.Add(
                FormatGridTime(entry.Time),
                entry.Protocol,
                entry.Direction,
                entry.Content,
                entry.Status,
                entry.DurationMs.ToString(),
                entry.ErrorType);

            var row = grid.Rows[rowIndex];
            row.Cells["Status"].Style.ForeColor = entry.Status == "Success" ? Success : Danger;
            row.Cells["Direction"].Style.ForeColor = entry.Direction == "Receive" ? Primary : TextMain;
        }
    }

    private void UpdateLogPageSummary(
        IReadOnlyList<CommunicationLogEntry> entries,
        Label totalValue,
        Label successValue,
        Label failedValue,
        Label avgDurationValue)
    {
        var successCount = entries.Count(entry => entry.Status == "Success");
        var failedCount = entries.Count - successCount;
        var durationEntries = entries.Where(entry => entry.DurationMs > 0).ToList();
        var avgDuration = durationEntries.Count == 0
            ? "-- ms"
            : $"{Math.Round(durationEntries.Average(entry => entry.DurationMs))} ms";

        UpdateMetricLabel(totalValue, entries.Count.ToString(), TextMuted);
        UpdateMetricLabel(successValue, successCount.ToString(), Success);
        UpdateMetricLabel(failedValue, failedCount.ToString(), failedCount > 0 ? Danger : TextMuted);
        UpdateMetricLabel(avgDurationValue, avgDuration, Primary);
    }

    private Panel CreateSurfacePanel()
    {
        return new BorderedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            BorderStyle = BorderStyle.None,
            BorderColor = SubtleBorder,
        };
    }

    private Label CreateInlineStatus(string text)
    {
        return new Label
        {
            Dock = DockStyle.Top,
            AutoEllipsis = true,
            Text = text,
            ForeColor = TextMuted,
            Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            Height = S(34),
            Margin = new Padding(S(8), 0, 0, 0),
        };
    }

    private Control CreateOverviewMetricCard(string title, Label valueLabel)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceLow,
            BorderStyle = BorderStyle.None,
            Margin = new Padding(0, 0, S(8), 0),
            Padding = new Padding(S(12), S(8), S(12), S(8)),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(22)));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            ForeColor = TextMuted,
            Font = new Font("Microsoft YaHei UI", 8.8F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        valueLabel.TextAlign = ContentAlignment.MiddleRight;
        valueLabel.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(valueLabel, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private static Control CreateMetricCard(string title, Label valueLabel)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceLow,
            BorderStyle = BorderStyle.None,
            Margin = new Padding(0, 0, /*S(8) handled by padding below*/ 8, 0),
            Padding = new Padding(8, 0, 8, 0),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            ForeColor = TextMuted,
            Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        valueLabel.TextAlign = ContentAlignment.MiddleRight;
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(valueLabel, 1, 0);
        card.Controls.Add(layout);
        return card;
    }

    private static Label CreateMetricValue(string text, Color color)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            ForeColor = color,
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };
    }

    private static Label CreateSectionTitle(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            ForeColor = TextMain,
            TextAlign = ContentAlignment.MiddleLeft,
        };
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            ForeColor = TextMuted,
            Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
        };
    }

    private TextBox CreateInput(string text)
    {
        return new TextBox
        {
            Text = text,
            AutoSize = false,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Surface,
            ForeColor = TextMain,
            Dock = DockStyle.Top,
            Height = S(34),
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular),
            Margin = new Padding(0),
        };
    }

    private ComboBox CreateComboBox(IEnumerable<string> items)
    {
        var comboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Top,
            Height = S(34),
            BackColor = Surface,
            ForeColor = TextMain,
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular),
            Margin = new Padding(0),
        };

        foreach (var item in items)
        {
            comboBox.Items.Add(item);
        }

        if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }

        return comboBox;
    }

    private static void SetComboBoxValue(ComboBox comboBox, string value)
    {
        var index = comboBox.FindStringExact(value);
        if (index >= 0)
        {
            comboBox.SelectedIndex = index;
            return;
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            comboBox.Items.Add(value);
            comboBox.SelectedItem = value;
        }
    }

    private Button CreatePrimaryButton(string text)
    {
        var button = CreateButton(text);
        button.BackColor = Primary;
        button.ForeColor = Color.White;
        return button;
    }

    private Button CreateSecondaryButton(string text)
    {
        var button = CreateButton(text);
        button.BackColor = Surface;
        button.ForeColor = TextMain;
        return button;
    }

    private Button CreateButton(string text)
    {
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(S(72), S(34)),
            Height = S(34),
            Margin = new Padding(0, 0, S(8), 0),
            Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
        };
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private Control CreateFieldStack(string labelText, Control input)
    {
        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, S(12), 0),
        };
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, S(16)));
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, S(34)));

        stack.Controls.Add(CreateFieldLabel(labelText), 0, 0);
        stack.Controls.Add(input, 0, 1);
        return stack;
    }

    private void AddDemoRows()
    {
        _logGrid.Rows.Clear();
        _logEntries.Clear();
        _logIndex = 0;
        _successCount = 0;
        _failedCount = 0;
        _lastDurationText = "-- ms";
        SetSimulatorState("离线", TextMuted);
        UpdateMetricLabel(_successCountValue, "0", Success);
        UpdateMetricLabel(_failedCountValue, "0", Danger);
        UpdateMetricLabel(_lastDurationValue, "-- ms", Primary);
        UpdateMetricLabel(_logCountValue, "0", TextMuted);
        UpdateStatusLine();

        var savedEntries = CommunicationLogStore.Load();
        if (savedEntries.Count > 0)
        {
            foreach (var entry in savedEntries)
            {
                AddLogEntry(entry, persist: false);
            }

            return;
        }

        AddLogRow("TCP-Sim", "System", "界面已启动", "Success", "0", "");
        AddLogRow("TCP-Sim", "System", "内置模拟设备就绪", "Success", "0", "");
        AddLogRow("TCP-Sim", "Receive", "示例返回：01 03 04 00 64 00 C8", "Success", "35", "");
    }

    private SimulatedResponse CreateSimulatedResponse(string command, string mode)
    {
        _simulationStep++;

        var normalized = command.Trim().ToUpperInvariant();
        var durationMs = (28 + _simulationStep * 7 % 46).ToString();

        if (normalized.Contains("TIMEOUT") || normalized.Contains("FF FF"))
        {
            return new SimulatedResponse("模拟超时：设备没有在规定时间内返回。", "Failed", "1500", "Timeout");
        }

        if (normalized.Contains("ERROR"))
        {
            return new SimulatedResponse("模拟异常：设备返回错误状态。", "Failed", durationMs, "DeviceError");
        }

        if (mode == "ASCII")
        {
            if (normalized.Contains("STATUS"))
            {
                return new SimulatedResponse("OK;RUN=1;TEMP=26.4;PRESSURE=0.62MPA", "Success", durationMs, "");
            }

            return new SimulatedResponse($"OK;ECHO={command.Trim()}", "Success", durationMs, "");
        }

        if (normalized.StartsWith("01 03", StringComparison.Ordinal))
        {
            var temperature = 100 + _simulationStep * 3;
            var pressure = 200 + _simulationStep * 5;
            return new SimulatedResponse($"01 03 04 {FormatRegisterValue(temperature)} {FormatRegisterValue(pressure)}", "Success", durationMs, "");
        }

        if (normalized.StartsWith("01 06", StringComparison.Ordinal))
        {
            return new SimulatedResponse("01 06 00 01 00 01", "Success", durationMs, "");
        }

        if (normalized.Contains("00 99"))
        {
            return new SimulatedResponse("01 83 02", "Failed", durationMs, "ModbusException");
        }

        return new SimulatedResponse($"AA 55 {_simulationStep:X2} 00", "Success", durationMs, "");
    }

    private static string FormatRegisterValue(int value)
    {
        return $"{value / 256:X2} {value % 256:X2}";
    }

    private void AddLogRow(string protocol, string direction, string content, string status, string durationMs, string errorType)
    {
        var entry = new CommunicationLogEntry(
            (_logEntries.Count + 1).ToString(),
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            protocol,
            direction,
            content,
            status,
            ParseDurationMs(durationMs),
            errorType);

        AddLogEntry(entry, persist: true);
    }

    private void AddLogEntry(CommunicationLogEntry entry, bool persist)
    {
        if (_logGrid is null)
        {
            return;
        }

        _logEntries.Add(entry);
        _logIndex = _logEntries.Count;
        UpdateMetricLabel(_logCountValue, _logIndex.ToString(), TextMuted);
        UpdateStatusLine();
        var rowIndex = _logGrid.Rows.Add(
            FormatGridTime(entry.Time),
            entry.Protocol,
            entry.Direction,
            entry.Content,
            entry.Status,
            entry.DurationMs.ToString(),
            entry.ErrorType);

        var row = _logGrid.Rows[rowIndex];
        row.Cells["Status"].Style.ForeColor = entry.Status == "Success" ? Success : Danger;
        row.Cells["Direction"].Style.ForeColor = entry.Direction == "Receive" ? Primary : TextMain;

        UpdateMetrics(entry.Status, entry.DurationMs.ToString(), entry.ErrorType);

        if (_logGrid.Rows.Count > 80)
        {
            _logGrid.Rows.RemoveAt(0);
        }

        if (_logGrid.IsHandleCreated && _logGrid.Rows.Count > 0)
        {
            try
            {
                _logGrid.FirstDisplayedScrollingRowIndex = _logGrid.Rows.Count - 1;
            }
            catch (InvalidOperationException)
            {
            }
        }

        if (persist)
        {
            CommunicationLogStore.Save(_logEntries);
        }
    }

    private static int ParseDurationMs(string durationMs)
    {
        return int.TryParse(durationMs, out var value) ? value : 0;
    }

    private static string FormatGridTime(string time)
    {
        return DateTime.TryParse(time, out var parsed) ? parsed.ToString("HH:mm:ss") : time;
    }

    private void UpdateMetrics(string status, string durationMs, string errorType)
    {
        if (status == "Success")
        {
            _successCount++;
        }
        else
        {
            _failedCount++;
        }

        UpdateMetricLabel(_successCountValue, _successCount.ToString(), Success);
        UpdateMetricLabel(_failedCountValue, _failedCount.ToString(), Danger);

        if (!string.IsNullOrWhiteSpace(durationMs) && durationMs != "0")
        {
            _lastDurationText = $"{durationMs} ms";
            UpdateMetricLabel(_lastDurationValue, $"{durationMs} ms", Primary);
        }

        UpdateStatusLine();
    }

    private void SetSimulatorState(string text, Color color)
    {
        _simulatorStateText = text;
        UpdateMetricLabel(_simulatorStateValue, text, color);
        UpdateStatusLine();
    }

    private void UpdateStatusLine()
    {
        if (_statusLineLabel is null)
        {
            return;
        }

        _statusLineLabel.Text = $"设备状态：{_simulatorStateText}    |    成功：{_successCount}    |    异常：{_failedCount}    |    最近耗时：{_lastDurationText}    |    日志条数：{_logIndex}";
    }

    private static void UpdateMetricLabel(Label? label, string text, Color color)
    {
        if (label is null)
        {
            return;
        }

        label.Text = text;
        label.ForeColor = color;
    }

    private static void SetStatus(Label label, string text, Color color)
    {
        label.Text = text;
        label.ForeColor = color;
    }

    private void ShowPlaceholderPage(string title)
    {
        var key = _navButtons.FirstOrDefault(pair => pair.Value.Text == title).Key;
        SetActiveNav(key);

        _contentPanel.Controls.Clear();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            ColumnCount = 1,
            RowCount = 2,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(72)));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var panel = CreateSurfacePanel();
        var label = new Label
        {
            AutoSize = false,
            Text = $"{title} 后面再做。现在先把 TCP 模拟这条主线跑通。",
            ForeColor = TextMuted,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
        };
        panel.Controls.Add(label);

        root.Controls.Add(BuildPageTitle(title, "这是占位页，避免一开始把功能做散。"), 0, 0);
        root.Controls.Add(panel, 0, 1);

        _contentPanel.Controls.Add(root);
    }

    private void SetActiveNav(string key)
    {
        foreach (var item in _navButtons)
        {
            var active = item.Key == key;
            item.Value.BackColor = active ? SidebarActive : SidebarBackground;
            item.Value.ForeColor = active ? Primary : SidebarText;
            item.Value.Font = new Font(Font.FontFamily, 9F, active ? FontStyle.Bold : FontStyle.Regular);
            item.Value.FlatAppearance.BorderColor = active ? Color.FromArgb(212, 224, 248) : Surface;
        }
    }

    private sealed record SimulatedResponse(string Content, string Status, string DurationMs, string ErrorType);

    private sealed class DurationTrendChart : Panel
    {
        private readonly IReadOnlyList<CommunicationLogEntry> _entries;

        public DurationTrendChart(IReadOnlyList<CommunicationLogEntry> entries)
        {
            _entries = entries;
            BackColor = Surface;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var plot = new Rectangle(42, 18, Math.Max(10, Width - 62), Math.Max(10, Height - 52));
            DrawAxes(e.Graphics, plot);

            if (_entries.Count == 0)
            {
                DrawEmptyText(e.Graphics, ClientRectangle, "暂无耗时数据");
                return;
            }

            var maxValue = Math.Max(10, _entries.Max(entry => entry.DurationMs));
            var points = new PointF[_entries.Count];
            for (var i = 0; i < _entries.Count; i++)
            {
                var x = _entries.Count == 1
                    ? plot.Left + plot.Width / 2f
                    : plot.Left + i * plot.Width / (float)(_entries.Count - 1);
                var y = plot.Bottom - _entries[i].DurationMs / (float)maxValue * plot.Height;
                points[i] = new PointF(x, y);
            }

            using var areaBrush = new SolidBrush(Color.FromArgb(28, Primary));
            var areaPoints = points
                .Concat(new[] { new PointF(points[^1].X, plot.Bottom), new PointF(points[0].X, plot.Bottom) })
                .ToArray();
            e.Graphics.FillPolygon(areaBrush, areaPoints);

            using var linePen = new Pen(Primary, 2.4F);
            if (points.Length > 1)
            {
                e.Graphics.DrawLines(linePen, points);
            }

            using var pointBrush = new SolidBrush(Surface);
            using var pointPen = new Pen(Primary, 2F);
            foreach (var point in points)
            {
                e.Graphics.FillEllipse(pointBrush, point.X - 4, point.Y - 4, 8, 8);
                e.Graphics.DrawEllipse(pointPen, point.X - 4, point.Y - 4, 8, 8);
            }

            DrawAxisText(e.Graphics, plot, $"最高 {maxValue} ms", _entries[0].Time, _entries[^1].Time);
        }
    }

    private sealed class StatusCountChart : Panel
    {
        private readonly int _successCount;
        private readonly int _failedCount;

        public StatusCountChart(int successCount, int failedCount)
        {
            _successCount = successCount;
            _failedCount = failedCount;
            BackColor = Surface;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var plot = new Rectangle(42, 18, Math.Max(10, Width - 62), Math.Max(10, Height - 52));
            DrawAxes(e.Graphics, plot);

            var maxValue = Math.Max(1, Math.Max(_successCount, _failedCount));
            DrawBar(e.Graphics, plot, 0, "成功", _successCount, maxValue, Success);
            DrawBar(e.Graphics, plot, 1, "失败", _failedCount, maxValue, Danger);
            DrawAxisText(e.Graphics, plot, $"最高 {maxValue} 条", "", "");
        }

        private static void DrawBar(Graphics graphics, Rectangle plot, int index, string label, int value, int maxValue, Color color)
        {
            var centerX = plot.Left + plot.Width * (index == 0 ? 0.32F : 0.68F);
            var barWidth = Math.Min(54, plot.Width * 0.18F);
            var height = value / (float)maxValue * plot.Height;
            var rectangle = new RectangleF(centerX - barWidth / 2F, plot.Bottom - height, barWidth, height);

            using var brush = new SolidBrush(color);
            graphics.FillRectangle(brush, rectangle);

            using var textBrush = new SolidBrush(TextMuted);
            using var valueBrush = new SolidBrush(TextMain);
            using var font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular);
            using var valueFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            graphics.DrawString(value.ToString(), valueFont, valueBrush, rectangle.Left + 4, rectangle.Top - 22);
            graphics.DrawString(label, font, textBrush, centerX - 16, plot.Bottom + 8);
        }
    }

    private static void DrawAxes(Graphics graphics, Rectangle plot)
    {
        using var axisPen = new Pen(SubtleBorder);
        using var darkPen = new Pen(Color.FromArgb(158, 166, 180));

        for (var i = 0; i <= 3; i++)
        {
            var y = plot.Top + i * plot.Height / 3F;
            graphics.DrawLine(axisPen, plot.Left, y, plot.Right, y);
        }

        graphics.DrawLine(darkPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
        graphics.DrawLine(darkPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
    }

    private static void DrawAxisText(Graphics graphics, Rectangle plot, string topText, string firstTime, string lastTime)
    {
        using var brush = new SolidBrush(TextMuted);
        using var font = new Font("Microsoft YaHei UI", 8F, FontStyle.Regular);
        graphics.DrawString(topText, font, brush, plot.Left, 0);

        if (!string.IsNullOrWhiteSpace(firstTime))
        {
            graphics.DrawString(ShortTime(firstTime), font, brush, plot.Left, plot.Bottom + 8);
        }

        if (!string.IsNullOrWhiteSpace(lastTime))
        {
            var text = ShortTime(lastTime);
            var size = graphics.MeasureString(text, font);
            graphics.DrawString(text, font, brush, plot.Right - size.Width, plot.Bottom + 8);
        }
    }

    private static void DrawEmptyText(Graphics graphics, Rectangle bounds, string text)
    {
        using var brush = new SolidBrush(TextMuted);
        using var font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
        var size = graphics.MeasureString(text, font);
        graphics.DrawString(text, font, brush, (bounds.Width - size.Width) / 2F, (bounds.Height - size.Height) / 2F);
    }

    private static string ShortTime(string value)
    {
        return value.Length <= 8 ? value : value[^8..];
    }

    private sealed class BorderedPanel : Panel
    {
        public Color BorderColor { get; set; } = SubtleBorder;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(BorderColor);
            var rectangle = ClientRectangle;
            rectangle.Width -= 1;
            rectangle.Height -= 1;
            e.Graphics.DrawRectangle(pen, rectangle);
        }
    }
}
