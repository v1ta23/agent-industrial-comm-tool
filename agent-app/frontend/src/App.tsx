import { useEffect, useMemo, useRef, useState } from "react";
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

type LogsResponse = {
  source: string;
  total: number;
  summary: CommunicationLogSummary;
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

export default function App() {
  const [logs, setLogs] = useState<CommunicationLogEntry[]>([]);
  const [summary, setSummary] = useState<CommunicationLogSummary>(emptySummary);
  const [source, setSource] = useState("");
  const [status, setStatus] = useState("正在读取日志");
  const [lastRefresh, setLastRefresh] = useState("");

  async function refreshLogs() {
    setStatus("正在读取日志");

    try {
      const response = await fetch(`${getApiBaseUrl()}/api/logs`);
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const data = await response.json() as LogsResponse;
      setLogs(data.items);
      setSummary(data.summary);
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
  const advice = useMemo(() => buildAdvice(summary, logs), [summary, logs]);

  return (
    <main className="app-shell">
      <aside className="side-rail">
        <div>
          <p className="eyebrow">Agent App</p>
          <h1>通信数据看板</h1>
        </div>
        <nav>
          <a href="#overview">概览</a>
          <a href="#charts">图表</a>
          <a href="#details">日志</a>
          <a href="#advice">建议</a>
        </nav>
        <div className="source-path">
          <span>日志来源</span>
          <strong>{source || "等待后端返回日志路径"}</strong>
        </div>
      </aside>

      <section className="workspace">
        <header className="topbar">
          <div>
            <p className="eyebrow">WinForms 日志读取</p>
            <h2>先看数据，不控制设备</h2>
            <p>看板从 JSON 日志读数据，适合先判断通信是否稳定。</p>
          </div>
          <button type="button" onClick={() => void refreshLogs()}>
            刷新数据
          </button>
        </header>

        <section id="overview" className="metrics" aria-label="日志概览">
          <Metric label="总日志" value={summary.total.toString()} />
          <Metric label="成功率" value={`${successRate}%`} tone="success" />
          <Metric label="失败次数" value={summary.failed.toString()} tone="danger" />
          <Metric label="平均耗时" value={`${summary.averageDurationMs} ms`} />
        </section>

        <p className="status-line">
          {status}
          {lastRefresh ? `，最后刷新：${lastRefresh}` : ""}
        </p>

        <section id="charts" className="chart-grid" aria-label="数据图表">
          <ChartPanel title="响应耗时" span="wide">
            <EChart option={buildDurationOption(responseLogs)} />
          </ChartPanel>

          <ChartPanel title="成功 / 失败">
            <EChart option={buildStatusOption(statusData)} />
          </ChartPanel>

          <ChartPanel title="异常类型">
            <EChart option={buildPieOption(errorTypeData)} />
          </ChartPanel>

          <ChartPanel title="协议分布">
            <EChart option={buildProtocolOption(protocolData)} />
          </ChartPanel>
        </section>

        <section id="details" className="bottom-grid">
          <section className="log-panel" aria-label="最近通信日志">
            <div className="section-heading">
              <div>
                <p className="eyebrow">最近记录</p>
                <h3>通信日志</h3>
              </div>
              <span>最新 8 条</span>
            </div>
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
          </section>

          <section id="advice" className="advice-panel" aria-label="排查建议">
            <div className="section-heading">
              <div>
                <p className="eyebrow">Agent 建议</p>
                <h3>先查这里</h3>
              </div>
            </div>
            <ul>
              {advice.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </section>
        </section>
      </section>
    </main>
  );
}

function Metric({ label, value, tone }: { label: string; value: string; tone?: "success" | "danger" }) {
  return (
    <div className="metric">
      <span>{label}</span>
      <strong className={tone}>{value}</strong>
    </div>
  );
}

function ChartPanel({ title, children, span }: { title: string; children: React.ReactNode; span?: "wide" }) {
  return (
    <section className={span === "wide" ? "chart-panel wide" : "chart-panel"}>
      <h3>{title}</h3>
      {children}
    </section>
  );
}

function EChart({ option }: { option: EChartsCoreOption }) {
  const chartRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!chartRef.current) {
      return;
    }

    const chart = init(chartRef.current);
    chart.setOption(option);

    const resize = () => chart.resize();
    window.addEventListener("resize", resize);

    return () => {
      window.removeEventListener("resize", resize);
      chart.dispose();
    };
  }, [option]);

  return <div className="chart" ref={chartRef} />;
}

function buildDurationOption(items: CommunicationLogEntry[]): EChartsCoreOption {
  return {
    tooltip: { trigger: "axis" },
    grid: { left: 44, right: 18, top: 28, bottom: 42 },
    xAxis: {
      type: "category",
      data: items.map((item) => item.time || item.id),
      axisTick: { show: false },
      axisLabel: { color: "#526072", hideOverlap: true },
    },
    yAxis: { type: "value", name: "ms", axisLabel: { color: "#526072" } },
    series: [
      {
        type: "line",
        smooth: true,
        symbolSize: 7,
        data: items.map((item) => item.durationMs),
        lineStyle: { color: "#2563eb", width: 3 },
        itemStyle: { color: "#2563eb" },
        areaStyle: { color: "rgba(37, 99, 235, 0.12)" },
      },
    ],
  };
}

function buildStatusOption(items: ChartItem[]): EChartsCoreOption {
  return {
    tooltip: {},
    grid: { left: 42, right: 16, top: 28, bottom: 32 },
    xAxis: {
      type: "category",
      data: items.map((item) => item.name),
      axisTick: { show: false },
      axisLabel: { color: "#526072" },
    },
    yAxis: { type: "value", axisLabel: { color: "#526072" } },
    series: [
      {
        type: "bar",
        barWidth: 42,
        data: [
          { value: items[0]?.value ?? 0, itemStyle: { color: "#16a34a" } },
          { value: items[1]?.value ?? 0, itemStyle: { color: "#dc2626" } },
        ],
      },
    ],
  };
}

function buildPieOption(items: ChartItem[]): EChartsCoreOption {
  return {
    tooltip: { trigger: "item" },
    legend: { bottom: 0, icon: "circle", textStyle: { color: "#526072" } },
    series: [
      {
        type: "pie",
        radius: ["42%", "68%"],
        center: ["50%", "42%"],
        data: items,
        color: ["#dc2626", "#f59e0b", "#64748b", "#2563eb"],
        label: { color: "#334155" },
      },
    ],
  };
}

function buildProtocolOption(items: ChartItem[]): EChartsCoreOption {
  return {
    tooltip: {},
    grid: { left: 42, right: 16, top: 28, bottom: 32 },
    xAxis: {
      type: "category",
      data: items.map((item) => item.name),
      axisTick: { show: false },
      axisLabel: { color: "#526072" },
    },
    yAxis: { type: "value", axisLabel: { color: "#526072" } },
    series: [
      {
        type: "bar",
        barWidth: 38,
        data: items.map((item) => ({ value: item.value, itemStyle: { color: "#0f766e" } })),
      },
    ],
  };
}

function toChartData(counts: Record<string, number>, emptyLabel: string) {
  const rows = Object.entries(counts).map(([name, value]) => ({ name, value }));
  return rows.length > 0 ? rows : [{ name: emptyLabel, value: 1 }];
}

function buildAdvice(summary: CommunicationLogSummary, logs: CommunicationLogEntry[]) {
  if (summary.total === 0) {
    return ["还没有通信日志，先在 WinForms 里跑一次模拟发送。"];
  }

  const advice: string[] = [];
  const topError = Object.entries(summary.errorTypeCounts).sort((a, b) => b[1] - a[1])[0];
  const latestFailed = [...logs].reverse().find((item) => item.status !== "Success");

  if (summary.failed > 0) {
    advice.push(`当前有 ${summary.failed} 条失败记录，先看最新失败：${latestFailed?.content ?? "无内容"}`);
  }

  if (topError) {
    advice.push(`出现最多的问题是 ${topError[0]}，优先检查连接状态、IP、端口和设备是否在线。`);
  }

  if (summary.averageDurationMs > 50) {
    advice.push("平均响应耗时偏高，可以先确认设备负载和网络延迟。");
  }

  if (advice.length === 0) {
    advice.push("当前日志看起来稳定，可以继续增加串口或 Modbus TCP 数据源。");
  }

  return advice;
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

function getApiBaseUrl() {
  return import.meta.env.VITE_API_BASE_URL ?? "http://127.0.0.1:4317";
}
