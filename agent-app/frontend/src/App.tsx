import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
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

type WorkbenchResponse = {
  source: string;
  total: number;
  summary: CommunicationLogSummary;
  analysis: CommunicationLogAnalysis;
  items: CommunicationLogEntry[];
};

type ChartItem = {
  name: string;
  value: number;
};

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
  const [source, setSource] = useState("");
  const [status, setStatus] = useState("正在读取日志");
  const [lastRefresh, setLastRefresh] = useState("");

  async function refreshLogs() {
    setStatus("正在读取日志");

    try {
      const response = await fetch(`${getApiBaseUrl()}/api/workbench`);

      if (!response.ok) {
        throw new Error(`工作台接口 HTTP ${response.status}`);
      }

      const data = await response.json() as WorkbenchResponse;
      setLogs(data.items);
      setSummary(data.summary);
      setAnalysis(data.analysis);
      setSource(data.source);
      setStatus(`已读取 ${data.total} 条日志`);
      setLastRefresh(new Date().toLocaleString());
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "读取失败");
    }
  }

  useEffect(() => {
    void refreshLogs();
  }, []);

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
    () => toChartData(summary.errorTypeCounts, "暂无异常"),
    [summary.errorTypeCounts],
  );
  const protocolData = useMemo(
    () => toChartData(summary.protocolCounts, "暂无协议"),
    [summary.protocolCounts],
  );
  const advice = analysis.suggestions.length > 0
    ? analysis.suggestions
    : ["Agent 正在等待可分析的通信日志。"];
  const evidence = analysis.evidence.length > 0
    ? analysis.evidence
    : ["暂无分析依据。"];
  const levelText = translateAnalysisLevel(analysis.level);
  const generatedAt = formatGeneratedAt(analysis.generatedAt);

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
          {lastRefresh ? <span>{lastRefresh}</span> : null}
        </div>

        <button className="primary-action" type="button" onClick={() => void refreshLogs()} aria-label="重新分析">
          <span aria-hidden="true">↻</span>
          重新分析
        </button>
      </header>

      <aside className="side-rail" aria-label="Agent 工作台导航">
        <div className="node-card">
          <span className="eyebrow">System Node</span>
          <strong>772-Alpha</strong>
          <small>WinForms 日志桥接中</small>
        </div>

        <button className="rail-action" type="button" onClick={() => void refreshLogs()}>
          <span aria-hidden="true">↻</span>
          重新分析
        </button>

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
              {lastRefresh ? <time>{lastRefresh}</time> : null}
            </div>
          </div>

          <div className="overview-control">
            <div className={`status-chip ${analysis.level}`}>
              <span>{levelText}</span>
              <strong>{analysis.focus}</strong>
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
            <h2>{analysis.summary}</h2>
            <div className="analysis-foot">
              <span>{analysis.focus}</span>
              {generatedAt ? <time dateTime={analysis.generatedAt}>{generatedAt}</time> : null}
            </div>
          </div>

          <div className="agent-lists">
            <AnalysisList title="建议" items={advice} />
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
              {advice.map((item) => (
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

function buildDurationOption(items: CommunicationLogEntry[]): EChartsCoreOption {
  const chartItems = items.length > 0 ? items : [{ id: "empty", time: "暂无数据", durationMs: 0 } as CommunicationLogEntry];

  return {
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
        symbolSize: 7,
        data: chartItems.map((item) => item.durationMs),
        lineStyle: { color: chartPalette.primary, width: 3 },
        itemStyle: { color: chartPalette.primary },
        areaStyle: { color: chartPalette.primarySoft },
      },
    ],
  };
}

function buildStatusOption(items: ChartItem[]): EChartsCoreOption {
  return {
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
    tooltip: { trigger: "item", borderColor: chartPalette.grid },
    legend: { bottom: 0, icon: "circle", textStyle: { color: chartPalette.axis } },
    series: [
      {
        type: "pie",
        radius: ["46%", "70%"],
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
        data: items.map((item) => ({
          value: item.value,
          itemStyle: { color: chartPalette.copper, borderRadius: [4, 4, 0, 0] },
        })),
      },
    ],
  };
}

function toChartData(counts: Record<string, number>, emptyLabel: string) {
  const rows = Object.entries(counts).map(([name, value]) => ({ name, value }));
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
  return import.meta.env.VITE_API_BASE_URL ?? "http://127.0.0.1:4317";
}
