import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties, type ReactNode } from "react";
import { BarChart, LineChart, PieChart } from "echarts/charts";
import { GridComponent, LegendComponent, TooltipComponent } from "echarts/components";
import { init, use, type EChartsCoreOption } from "echarts/core";
import { CanvasRenderer } from "echarts/renderers";

use([BarChart, LineChart, PieChart, GridComponent, LegendComponent, TooltipComponent, CanvasRenderer]);

type CommunicationLogEntry = {
  id: string;
  time: string;
  protocol: string;
  direction: string;
  content: string;
  status: string;
  durationMs: number;
  errorType: string;
};

type CommunicationLogSummary = {
  total: number;
  success: number;
  failed: number;
  averageDurationMs: number;
  protocolCounts: Record<string, number>;
  errorTypeCounts: Record<string, number>;
};

type CommunicationLogAnalysis = {
  level: "normal" | "warning" | "critical";
  summary: string;
  focus: string;
  suggestions: string[];
  evidence: string[];
  generatedAt: string;
};

type ModelAnalysis = {
  status: "disabled" | "ok" | "error";
  provider: string;
  model: string;
  summary: string;
  suggestions: string[];
  rawText: string;
  generatedAt: string;
  error?: string;
};

type WorkbenchResponse = {
  source: string;
  total: number;
  summary: CommunicationLogSummary;
  analysis: CommunicationLogAnalysis;
  modelAnalysis?: ModelAnalysis;
  logState?: LogState;
  items: CommunicationLogEntry[];
};

type ChartItem = {
  name: string;
  value: number;
};

type LogState = {
  source: string;
  exists: boolean;
  size: number;
  fingerprint: string;
  checkedAt: string;
};

type RefreshReason = "initial" | "auto" | "manual";

const AUTO_LISTEN_INTERVAL_MS = 15000;
const AUTO_LISTEN_SECONDS = AUTO_LISTEN_INTERVAL_MS / 1000;

const emptySummary: CommunicationLogSummary = {
  total: 0,
  success: 0,
  failed: 0,
  averageDurationMs: 0,
  protocolCounts: {},
  errorTypeCounts: {},
};

const emptyAnalysis: CommunicationLogAnalysis = {
  level: "normal",
  summary: "正在等待 Agent 分析",
  focus: "读取日志后生成判断",
  suggestions: [],
  evidence: [],
  generatedAt: "",
};

const emptyModelAnalysis: ModelAnalysis = {
  status: "disabled",
  provider: "openai-compatible",
  model: "",
  summary: "大模型分析未启用",
  suggestions: ["设置 LLM_API_KEY 和 LLM_MODEL 后，LangChain 才会调用外部大模型。"],
  rawText: "",
  generatedAt: "",
};

const navigationItems = [
  { href: "#overview", icon: "01", label: "运行概览" },
  { href: "#agent", icon: "02", label: "Agent 判断" },
  { href: "#charts", icon: "03", label: "趋势图表" },
  { href: "#details", icon: "04", label: "通信日志" },
];

const chartPalette = {
  axis: "#596276",
  grid: "#dfe4ec",
  primary: "#003d9b",
  primarySoft: "rgba(0, 61, 155, 0.1)",
  success: "#147a55",
  danger: "#ba1a1a",
  warning: "#a35a00",
  neutral: "#6f7582",
  copper: "#7b2600",
};

export default function App() {
  const [logs, setLogs] = useState<CommunicationLogEntry[]>([]);
  const [summary, setSummary] = useState<CommunicationLogSummary>(emptySummary);
  const [analysis, setAnalysis] = useState<CommunicationLogAnalysis>(emptyAnalysis);
  const [modelAnalysis, setModelAnalysis] = useState<ModelAnalysis>(emptyModelAnalysis);
  const [source, setSource] = useState("");
  const [status, setStatus] = useState("正在读取日志");
  const [lastRefresh, setLastRefresh] = useState("");
  const [refreshHint, setRefreshHint] = useState("不自动刷新，避免重复消耗 token");
  const [activeRefreshReason, setActiveRefreshReason] = useState<RefreshReason | null>(null);
  const [autoListenEnabled, setAutoListenEnabled] = useState(false);
  const [hasModelAnalysisRun, setHasModelAnalysisRun] = useState(false);
  const [modelAnalysisFailed, setModelAnalysisFailed] = useState(false);
  const isRefreshingRef = useRef(false);
  const lastTotalRef = useRef<number | null>(null);
  const lastLogIdRef = useRef<string | null>(null);
  const lastLogFingerprintRef = useRef<string | null>(null);
  const isRefreshing = activeRefreshReason !== null;

  const refreshLogs = useCallback(async (reason: RefreshReason = "manual") => {
    if (isRefreshingRef.current) {
      if (reason === "manual") {
        setRefreshHint("正在刷新，等这次返回");
      }

      return;
    }

    isRefreshingRef.current = true;
    setActiveRefreshReason(reason);
    setStatus(reason === "manual" ? "正在重新分析" : reason === "auto" ? "正在监听日志" : "正在读取日志");

    try {
      const modelQuery = reason === "manual" ? "?model=1" : "";
      const response = await fetch(`${getApiBaseUrl()}/api/workbench${modelQuery}`, { cache: "no-store" });

      if (!response.ok) {
        throw new Error(`工作台接口 HTTP ${response.status}`);
      }

      const data = await response.json() as WorkbenchResponse;
      const previousTotal = lastTotalRef.current;
      const previousLogId = lastLogIdRef.current;
      const latestLogId = data.items.at(-1)?.id ?? null;
      const refreshState = buildRefreshState(data.total, previousTotal, latestLogId, previousLogId);
      const logFingerprint = data.logState?.fingerprint ?? buildLogSnapshotFingerprint(data.total, latestLogId);

      setLogs(data.items);
      setSummary(data.summary);
      setAnalysis(data.analysis);
      setModelAnalysis(data.modelAnalysis ?? emptyModelAnalysis);
      setSource(data.source);
      setStatus(refreshState.status);
      setRefreshHint(reason === "manual" ? "本次已手动调用大模型" : reason === "auto" ? buildAutoListenHint() : refreshState.hint);
      setLastRefresh(new Date().toLocaleString());
      lastTotalRef.current = data.total;
      lastLogIdRef.current = latestLogId;
      lastLogFingerprintRef.current = logFingerprint;

      if (reason === "manual") {
        setHasModelAnalysisRun(true);
        setModelAnalysisFailed(false);
      }
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "读取失败");
      setRefreshHint(reason === "manual" ? "手动重新分析失败" : reason === "auto" ? "自动监听读取失败，下次继续只读本地日志" : "读取失败，没有自动重试");

      if (reason === "manual") {
        setModelAnalysisFailed(true);
      }
    } finally {
      isRefreshingRef.current = false;
      setActiveRefreshReason(null);
    }
  }, []);

  const checkLogState = useCallback(async () => {
    if (isRefreshingRef.current) {
      return;
    }

    try {
      const response = await fetch(`${getApiBaseUrl()}/api/log-state`, { cache: "no-store" });

      if (!response.ok) {
        throw new Error(`日志状态接口 HTTP ${response.status}`);
      }

      const data = await response.json() as LogState;
      const previousFingerprint = lastLogFingerprintRef.current;

      if (previousFingerprint === null) {
        lastLogFingerprintRef.current = data.fingerprint;
        setRefreshHint(buildAutoListenHint("等待日志变化"));
        return;
      }

      if (data.fingerprint !== previousFingerprint) {
        setRefreshHint("发现日志变化，正在刷新本地看板");
        await refreshLogs("auto");
        return;
      }

      setRefreshHint(buildAutoListenHint("未发现新日志"));
    } catch (error) {
      setRefreshHint(error instanceof Error ? error.message : "日志状态检查失败");
    }
  }, [refreshLogs]);

  useEffect(() => {
    void refreshLogs("initial");
  }, [refreshLogs]);

  useEffect(() => {
    if (!autoListenEnabled) {
      return;
    }

    const checkLogStateWhenVisible = () => {
      if (document.visibilityState !== "visible") {
        setRefreshHint("页面不在前台，自动监听暂停");
        return;
      }

      void checkLogState();
    };

    setRefreshHint(buildAutoListenHint());
    void checkLogState();
    const timerId = window.setInterval(checkLogStateWhenVisible, AUTO_LISTEN_INTERVAL_MS);
    document.addEventListener("visibilitychange", checkLogStateWhenVisible);

    return () => {
      window.clearInterval(timerId);
      document.removeEventListener("visibilitychange", checkLogStateWhenVisible);
    };
  }, [autoListenEnabled, checkLogState]);

  const handleAutoListenChange = (enabled: boolean) => {
    setAutoListenEnabled(enabled);
    setRefreshHint(enabled ? buildAutoListenHint() : "自动监听已关闭，避免重复消耗 token");
  };

  const responseLogs = useMemo(
    () => logs.filter((item) => item.durationMs > 0),
    [logs],
  );
  const recentLogs = useMemo(() => logs.slice(-8).reverse(), [logs]);
  const successRate = summary.total === 0 ? 0 : Math.round((summary.success / summary.total) * 100);
  const statusData = useMemo<ChartItem[]>(
    () => [
      { name: "成功", value: summary.success },
      { name: "失败", value: summary.failed },
    ],
    [summary.failed, summary.success],
  );
  const errorTypeData = useMemo(
    () => toChartData(summary.errorTypeCounts, "暂无异常", translateErrorType),
    [summary.errorTypeCounts],
  );
  const protocolData = useMemo(
    () => toChartData(summary.protocolCounts, "暂无协议"),
    [summary.protocolCounts],
  );
  const ruleAdvice = analysis.suggestions.length > 0
    ? analysis.suggestions.map(translateAnalysisText)
    : ["Agent 正在等待可分析的通信日志。"];
  const modelAdvice = modelAnalysis.suggestions.length > 0
    ? modelAnalysis.suggestions.map(translateAnalysisText)
    : [modelAnalysis.summary || "大模型暂时没有返回建议。"];
  const primaryAdvice = modelAnalysis.status === "ok" ? modelAdvice : ruleAdvice;
  const evidence = analysis.evidence.length > 0
    ? analysis.evidence.map(translateAnalysisText)
    : ["暂无分析依据。"];
  const modelTitle = modelAnalysis.status === "ok"
    ? `大模型建议 / ${modelAnalysis.model}`
    : "大模型状态";
  const levelText = translateAnalysisLevel(analysis.level);
  const analysisButtonText = getAnalysisButtonText(activeRefreshReason, hasModelAnalysisRun, modelAnalysisFailed);
  const focusText = translateAnalysisText(analysis.focus);
  const summaryText = translateAnalysisText(analysis.summary);
  const generatedAt = formatGeneratedAt(analysis.generatedAt);
  const visualPackets: CommunicationLogEntry[] = recentLogs.length > 0
    ? recentLogs.slice(0, 6)
    : [{
      id: "waiting",
      time: "",
      protocol: "WAIT",
      direction: "System",
      content: "等待日志",
      status: "Idle",
      durationMs: 0,
      errorType: "",
    }];
  const radarStyle = { "--success-rate": `${successRate * 3.6}deg` } as CSSProperties;

  return (
    <main className="app-shell">
      <header className="shell-topbar">
        <a className="brand-lockup" href="#overview" aria-label="回到运行概览">
          <span className="brand-mark" aria-hidden="true">IC</span>
          <span>
            <strong>IndustrialControl</strong>
            <small>Agent Workbench</small>
          </span>
        </a>

        <div className="topbar-meta" aria-label="当前运行状态">
          <span className={`live-dot ${analysis.level}`} aria-hidden="true" />
          <span>{status}</span>
          <span>{refreshHint}</span>
          {lastRefresh ? <span>{lastRefresh}</span> : null}
        </div>

        <div className="topbar-actions">
          <AutoListenToggle enabled={autoListenEnabled} onChange={handleAutoListenChange} />
          <button className="primary-action" type="button" onClick={() => void refreshLogs("manual")} aria-label={analysisButtonText} disabled={isRefreshing}>
            <span aria-hidden="true">↻</span>
            {analysisButtonText}
          </button>
        </div>
      </header>

      <aside className="side-rail" aria-label="Agent 工作台导航">
        <div className="node-card">
          <span className="eyebrow">System Node</span>
          <strong>772-Alpha</strong>
          <small>WinForms 日志桥接中</small>
        </div>

        <div className="rail-controls">
          <AutoListenToggle enabled={autoListenEnabled} onChange={handleAutoListenChange} />
          <button className="rail-action" type="button" onClick={() => void refreshLogs("manual")} disabled={isRefreshing}>
            <span aria-hidden="true">↻</span>
            {analysisButtonText}
          </button>
        </div>

        <nav>
          {navigationItems.map((item, index) => (
            <a className={index === 0 ? "active" : ""} href={item.href} key={item.href}>
              <span aria-hidden="true">{item.icon}</span>
              {item.label}
            </a>
          ))}
        </nav>

        <div className="source-path">
          <span>日志来源</span>
          <strong>{source || "等待后端返回日志路径"}</strong>
        </div>
      </aside>

      <section className="workspace">
        <section id="overview" className={`overview-board ${analysis.level}`} aria-label="工作台首页">
          <div className="overview-copy">
            <p className="eyebrow">Communication Agent</p>
            <h1>通信 Agent 工作台</h1>
            <p>读取通信日志，标出失败、超时和慢响应。</p>
            <div className="overview-meta">
              <span className={`live-dot ${analysis.level}`} aria-hidden="true" />
              <span>{status}</span>
              <span>{refreshHint}</span>
              {lastRefresh ? <time>{lastRefresh}</time> : null}
            </div>
          </div>

          <div className="signal-visual" aria-label="通信状态可视化">
            <div className={`radar-core ${analysis.level}`} style={radarStyle}>
              <span className="radar-grid" aria-hidden="true" />
              <span className="radar-sweep" aria-hidden="true" />
              <strong>{successRate}%</strong>
              <small>链路稳定度</small>
            </div>

            <div className="signal-lanes" aria-label="最近通信流">
              {visualPackets.map((item, index) => (
                <span
                  className={`signal-packet ${item.status === "Success" ? "success" : item.status === "Idle" ? "idle" : "danger"}`}
                  key={item.id}
                  style={{ "--delay": `${index * 0.18}s` } as CSSProperties}
                >
                  <b>{item.protocol}</b>
                  <i>{item.durationMs > 0 ? `${item.durationMs} ms` : "等待"}</i>
                </span>
              ))}
            </div>
          </div>

          <div className="overview-control">
            <div className={`status-chip ${analysis.level}`}>
              <span>{levelText}</span>
              <strong>{focusText}</strong>
            </div>

            <section className="metrics" aria-label="日志概览">
              <Metric label="总日志" value={summary.total.toString()} marker="总" />
              <Metric label="成功率" value={`${successRate}%`} marker="稳" tone="success" />
              <Metric label="失败次数" value={summary.failed.toString()} marker="险" tone="danger" />
              <Metric label="平均耗时" value={`${summary.averageDurationMs} ms`} marker="时" />
            </section>
          </div>
        </section>

        <section id="agent" className={`agent-board ${analysis.level}`} aria-label="Agent 分析">
          <div className="agent-primary">
            <p className="eyebrow">Agent 判断</p>
            <h2>{summaryText}</h2>
            <div className="analysis-foot">
              <span>{focusText}</span>
              {generatedAt ? <time dateTime={analysis.generatedAt}>{generatedAt}</time> : null}
            </div>
          </div>

          <div className="agent-lists">
            <AnalysisList title={modelTitle} items={modelAdvice} />
            <AnalysisList title="规则建议" items={ruleAdvice} />
            <AnalysisList title="依据" items={evidence} />
          </div>
        </section>

        <section id="charts" className="chart-grid" aria-label="数据图表">
          <ChartPanel title="响应耗时" meta={`${responseLogs.length} 条有效响应`} span="main">
            <EChart option={buildDurationOption(responseLogs)} />
          </ChartPanel>

          <div className="chart-side-stack">
            <ChartPanel title="成功 / 失败" meta="按状态聚合">
              <EChart option={buildStatusOption(statusData)} />
            </ChartPanel>

            <ChartPanel title="协议分布" meta="按协议聚合">
              <EChart option={buildProtocolOption(protocolData)} />
            </ChartPanel>
          </div>

          <ChartPanel title="异常类型" meta="按错误类型聚合" span="full">
            <EChart option={buildPieOption(errorTypeData)} />
          </ChartPanel>
        </section>

        <section id="details" className="bottom-grid">
          <section className="log-panel" aria-label="最近通信日志">
            <div className="section-heading">
              <div>
                <p className="eyebrow">最近记录</p>
                <h2>通信日志</h2>
              </div>
              <span>最新 8 条</span>
            </div>

            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>时间</th>
                    <th>协议</th>
                    <th>方向</th>
                    <th>状态</th>
                    <th>耗时</th>
                    <th>内容</th>
                  </tr>
                </thead>
                <tbody>
                  {recentLogs.map((item) => (
                    <tr key={item.id}>
                      <td>{item.time}</td>
                      <td>{item.protocol}</td>
                      <td>{translateDirection(item.direction)}</td>
                      <td>
                        <span className={item.status === "Success" ? "pill success" : "pill danger"}>
                          {translateStatus(item.status)}
                        </span>
                      </td>
                      <td>{item.durationMs} ms</td>
                      <td className="content-cell">{item.content}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <section className="advice-panel" aria-label="排查建议">
            <div className="section-heading">
              <div>
                <p className="eyebrow">Next Check</p>
                <h2>先查这里</h2>
              </div>
            </div>
            <ol>
              {primaryAdvice.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ol>
          </section>
        </section>
      </section>
    </main>
  );
}

function Metric({
  label,
  marker,
  value,
  tone,
}: {
  label: string;
  marker: string;
  value: string;
  tone?: "success" | "danger";
}) {
  return (
    <div className="metric">
      <span className={`metric-mark ${tone ?? ""}`} aria-hidden="true">{marker}</span>
      <span>{label}</span>
      <strong className={tone}>{value}</strong>
    </div>
  );
}

function AnalysisList({ title, items }: { title: string; items: string[] }) {
  return (
    <div className="agent-list">
      <span>{title}</span>
      <ul>
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </div>
  );
}

function ChartPanel({
  title,
  meta,
  children,
  span,
}: {
  title: string;
  meta: string;
  children: ReactNode;
  span?: "main" | "full";
}) {
  return (
    <section className={span ? `chart-panel ${span}` : "chart-panel"}>
      <div className="chart-head">
        <h2>{title}</h2>
        <span>{meta}</span>
      </div>
      {children}
    </section>
  );
}

function EChart({ option }: { option: EChartsCoreOption }) {
  const chartRef = useRef<HTMLDivElement>(null);
  const chartInstanceRef = useRef<ReturnType<typeof init> | null>(null);

  useEffect(() => {
    if (!chartRef.current) {
      return;
    }

    const chart = init(chartRef.current);
    chartInstanceRef.current = chart;

    const resize = () => chart.resize();
    window.addEventListener("resize", resize);

    return () => {
      window.removeEventListener("resize", resize);
      chart.dispose();
      chartInstanceRef.current = null;
    };
  }, []);

  useEffect(() => {
    chartInstanceRef.current?.setOption(option, true);
  }, [option]);

  return <div className="chart" ref={chartRef} />;
}

function AutoListenToggle({ enabled, onChange }: { enabled: boolean; onChange: (enabled: boolean) => void }) {
  return (
    <label className={`auto-listen-toggle ${enabled ? "active" : ""}`}>
      <input
        checked={enabled}
        onChange={(event) => onChange(event.currentTarget.checked)}
        type="checkbox"
      />
      <span className="toggle-track" aria-hidden="true">
        <span className="toggle-thumb" />
      </span>
      <span>自动监听</span>
    </label>
  );
}

function buildAutoListenHint(detail?: string) {
  return detail
    ? `自动监听中，每 ${AUTO_LISTEN_SECONDS} 秒检查日志，${detail}`
    : `自动监听中，每 ${AUTO_LISTEN_SECONDS} 秒检查日志`;
}

function buildLogSnapshotFingerprint(total: number, latestLogId: string | null) {
  return `${total}:${latestLogId ?? ""}`;
}

function getAnalysisButtonText(
  activeRefreshReason: RefreshReason | null,
  hasModelAnalysisRun: boolean,
  modelAnalysisFailed: boolean,
) {
  if (activeRefreshReason === "manual") {
    return "分析中";
  }

  if (modelAnalysisFailed) {
    return "重试分析";
  }

  return hasModelAnalysisRun ? "重新分析" : "开始分析";
}

function buildRefreshState(
  total: number,
  previousTotal: number | null,
  latestLogId: string | null,
  previousLogId: string | null,
) {
  if (previousTotal === null) {
    return {
      status: `已读取 ${total} 条日志`,
      hint: "只读本地日志，不自动调用大模型",
    };
  }

  if (total > previousTotal) {
    return {
      status: `已读取 ${total} 条日志，新增 ${total - previousTotal} 条`,
      hint: "刚刚发现新日志",
    };
  }

  if (total < previousTotal) {
    return {
      status: `已读取 ${total} 条日志，日志文件已重置`,
      hint: "不自动刷新，避免重复消耗 token",
    };
  }

  if (latestLogId && latestLogId !== previousLogId) {
    return {
      status: `已读取 ${total} 条日志，最近日志已更新`,
      hint: "刚刚发现日志更新",
    };
  }

  return {
    status: `已读取 ${total} 条日志`,
    hint: "不自动刷新，避免重复消耗 token",
  };
}

function buildDurationOption(items: CommunicationLogEntry[]): EChartsCoreOption {
  const chartItems = items.length > 0 ? items : [{ id: "empty", time: "暂无数据", durationMs: 0 } as CommunicationLogEntry];

  return {
    animationDuration: 850,
    animationEasing: "cubicOut",
    tooltip: { trigger: "axis", borderColor: chartPalette.grid, textStyle: { color: "#1f2633" } },
    grid: { left: 48, right: 20, top: 30, bottom: 44 },
    xAxis: {
      type: "category",
      data: chartItems.map((item) => item.time || item.id),
      axisTick: { show: false },
      axisLine: { lineStyle: { color: chartPalette.grid } },
      axisLabel: { color: chartPalette.axis, hideOverlap: true },
    },
    yAxis: {
      type: "value",
      name: "ms",
      splitLine: { lineStyle: { color: chartPalette.grid } },
      axisLabel: { color: chartPalette.axis },
    },
    series: [
      {
        type: "line",
        smooth: true,
        symbolSize: 8,
        data: chartItems.map((item) => item.durationMs),
        lineStyle: { color: chartPalette.primary, width: 4, shadowBlur: 12, shadowColor: "rgba(0, 61, 155, 0.22)" },
        itemStyle: { color: chartPalette.primary, borderColor: "#ffffff", borderWidth: 2 },
        areaStyle: { color: chartPalette.primarySoft },
        emphasis: { focus: "series", scale: true },
      },
    ],
  };
}

function buildStatusOption(items: ChartItem[]): EChartsCoreOption {
  return {
    animationDuration: 760,
    animationEasing: "quarticOut",
    tooltip: { borderColor: chartPalette.grid },
    grid: { left: 44, right: 16, top: 28, bottom: 34 },
    xAxis: {
      type: "category",
      data: items.map((item) => item.name),
      axisTick: { show: false },
      axisLine: { lineStyle: { color: chartPalette.grid } },
      axisLabel: { color: chartPalette.axis },
    },
    yAxis: {
      type: "value",
      splitLine: { lineStyle: { color: chartPalette.grid } },
      axisLabel: { color: chartPalette.axis },
    },
    series: [
      {
        type: "bar",
        barWidth: 38,
        showBackground: true,
        backgroundStyle: { color: "#eef2f7", borderRadius: [4, 4, 0, 0] },
        data: [
          { value: items[0]?.value ?? 0, itemStyle: { color: chartPalette.success, borderRadius: [4, 4, 0, 0] } },
          { value: items[1]?.value ?? 0, itemStyle: { color: chartPalette.danger, borderRadius: [4, 4, 0, 0] } },
        ],
      },
    ],
  };
}

function buildPieOption(items: ChartItem[]): EChartsCoreOption {
  return {
    animationDuration: 900,
    animationEasing: "cubicOut",
    tooltip: { trigger: "item", borderColor: chartPalette.grid },
    legend: { bottom: 0, icon: "circle", textStyle: { color: chartPalette.axis } },
    series: [
      {
        type: "pie",
        roseType: "radius",
        radius: ["38%", "76%"],
        center: ["50%", "42%"],
        data: items,
        color: [chartPalette.danger, chartPalette.warning, chartPalette.neutral, chartPalette.primary],
        label: { color: "#2d3442" },
      },
    ],
  };
}

function buildProtocolOption(items: ChartItem[]): EChartsCoreOption {
  return {
    animationDuration: 760,
    animationEasing: "quarticOut",
    tooltip: { borderColor: chartPalette.grid },
    grid: { left: 44, right: 16, top: 28, bottom: 34 },
    xAxis: {
      type: "category",
      data: items.map((item) => item.name),
      axisTick: { show: false },
      axisLine: { lineStyle: { color: chartPalette.grid } },
      axisLabel: { color: chartPalette.axis },
    },
    yAxis: {
      type: "value",
      splitLine: { lineStyle: { color: chartPalette.grid } },
      axisLabel: { color: chartPalette.axis },
    },
    series: [
      {
        type: "bar",
        barWidth: 36,
        showBackground: true,
        backgroundStyle: { color: "#eef2f7", borderRadius: [4, 4, 0, 0] },
        data: items.map((item) => ({
          value: item.value,
          itemStyle: { color: chartPalette.copper, borderRadius: [4, 4, 0, 0] },
        })),
      },
    ],
  };
}

function toChartData(counts: Record<string, number>, emptyLabel: string, translateName?: (name: string) => string) {
  const rows = Object.entries(counts).map(([name, value]) => ({ name: translateName?.(name) ?? name, value }));
  return rows.length > 0 ? rows : [{ name: emptyLabel, value: 1 }];
}

function translateDirection(direction: string) {
  const map: Record<string, string> = {
    Send: "发送",
    Receive: "接收",
    System: "系统",
  };
  return map[direction] ?? direction;
}

function translateStatus(status: string) {
  return status === "Success" ? "成功" : "失败";
}

function translateErrorType(errorType: string) {
  const map: Record<string, string> = {
    Disconnected: "未连接",
    CrcError: "校验错误",
    ModbusException: "Modbus 异常",
    Timeout: "超时",
    Unknown: "未知异常",
  };
  return map[errorType] ?? errorType;
}

function translateAnalysisText(value: string) {
  return value
    .replaceAll("Disconnected", "未连接")
    .replaceAll("CrcError", "校验错误")
    .replaceAll("ModbusException", "Modbus 异常")
    .replaceAll("Timeout", "超时")
    .replaceAll("Unknown", "未知异常");
}

function translateAnalysisLevel(level: CommunicationLogAnalysis["level"]) {
  const map: Record<CommunicationLogAnalysis["level"], string> = {
    normal: "运行正常",
    warning: "需要关注",
    critical: "优先处理",
  };
  return map[level];
}

function formatGeneratedAt(value: string) {
  if (!value) {
    return "";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }

  return date.toLocaleString();
}

function getApiBaseUrl() {
  return import.meta.env.VITE_API_BASE_URL ?? "";
}
