import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

export type CommunicationLogEntry = {
  id: string;
  time: string;
  protocol: string;
  direction: string;
  content: string;
  status: string;
  durationMs: number;
  errorType: string;
};

export type CommunicationLogSummary = {
  total: number;
  success: number;
  failed: number;
  averageDurationMs: number;
  protocolCounts: Record<string, number>;
  errorTypeCounts: Record<string, number>;
};

export type CommunicationLogAnalysis = {
  level: "normal" | "warning" | "critical";
  summary: string;
  focus: string;
  suggestions: string[];
  evidence: string[];
  generatedAt: string;
};

const currentDirectory = path.dirname(fileURLToPath(import.meta.url));

export function getDefaultLogPath() {
  return process.env.COMM_LOG_PATH
    ?? path.resolve(currentDirectory, "..", "..", "..", "data", "comm-logs.json");
}

export async function loadCommunicationLogs(logPath = getDefaultLogPath()) {
  try {
    const raw = await readFile(logPath, "utf8");
    const parsed = JSON.parse(raw) as unknown;

    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed.map(normalizeLogEntry);
  } catch (error) {
    if (isMissingFileError(error)) {
      return [];
    }

    throw error;
  }
}

export function summarizeLogs(entries: CommunicationLogEntry[]): CommunicationLogSummary {
  const summary: CommunicationLogSummary = {
    total: entries.length,
    success: 0,
    failed: 0,
    averageDurationMs: 0,
    protocolCounts: {},
    errorTypeCounts: {},
  };

  let durationTotal = 0;
  let durationCount = 0;

  for (const entry of entries) {
    if (entry.status === "Success") {
      summary.success += 1;
    } else {
      summary.failed += 1;
    }

    summary.protocolCounts[entry.protocol] = (summary.protocolCounts[entry.protocol] ?? 0) + 1;

    if (entry.errorType) {
      summary.errorTypeCounts[entry.errorType] = (summary.errorTypeCounts[entry.errorType] ?? 0) + 1;
    }

    if (entry.durationMs > 0) {
      durationTotal += entry.durationMs;
      durationCount += 1;
    }
  }

  summary.averageDurationMs = durationCount === 0 ? 0 : Math.round(durationTotal / durationCount);
  return summary;
}

export function analyzeLogs(entries: CommunicationLogEntry[]): CommunicationLogAnalysis {
  if (entries.length === 0) {
    return {
      level: "normal",
      summary: "还没有通信日志",
      focus: "先生成通信数据",
      suggestions: ["回到 WinForms 的 TCP 调试页，连接模拟设备并发送一次指令。"],
      evidence: ["日志文件为空，Agent 暂时没有可分析的数据。"],
      generatedAt: new Date().toISOString(),
    };
  }

  const summary = summarizeLogs(entries);
  const failedEntries = entries.filter((entry) => entry.status !== "Success");
  const timeoutEntries = failedEntries.filter((entry) => entry.errorType === "Timeout");
  const disconnectedEntries = failedEntries.filter((entry) => entry.errorType === "Disconnected");
  const slowEntries = entries.filter((entry) => entry.durationMs > Math.max(80, summary.averageDurationMs * 2));
  const latestFailed = failedEntries.at(-1);
  const topError = Object.entries(summary.errorTypeCounts).sort((left, right) => right[1] - left[1])[0];
  const suggestions: string[] = [];
  const evidence: string[] = [
    `总日志 ${summary.total} 条，成功 ${summary.success} 条，失败 ${summary.failed} 条。`,
    `平均响应耗时 ${summary.averageDurationMs} ms。`,
  ];

  let level: CommunicationLogAnalysis["level"] = "normal";
  let focus = "通信状态正常";
  let summaryText = "当前日志看起来稳定";

  if (failedEntries.length > 0) {
    level = failedEntries.length >= 3 ? "critical" : "warning";
    focus = topError ? `优先排查 ${topError[0]}` : "优先排查失败通信";
    summaryText = `发现 ${failedEntries.length} 条失败通信`;
    evidence.push(`最新失败：${latestFailed?.content || "无内容"}`);
  }

  if (disconnectedEntries.length > 0) {
    suggestions.push("先检查连接状态、IP、端口和设备是否在线。");
  }

  if (timeoutEntries.length > 0) {
    suggestions.push("有超时记录，优先确认设备负载、网络延迟和超时时间设置。");
  }

  if (slowEntries.length > 0) {
    level = level === "normal" ? "warning" : level;
    suggestions.push("有响应明显变慢的记录，可以对照时间点检查设备或网络状态。");
    evidence.push(`慢响应 ${slowEntries.length} 条，最高 ${Math.max(...slowEntries.map((entry) => entry.durationMs))} ms。`);
  }

  if (suggestions.length === 0) {
    suggestions.push("继续保留日志采集，下一步可以接入串口或 Modbus TCP 数据。");
  }

  return {
    level,
    summary: summaryText,
    focus,
    suggestions,
    evidence,
    generatedAt: new Date().toISOString(),
  };
}

function normalizeLogEntry(entry: Partial<CommunicationLogEntry>, index: number): CommunicationLogEntry {
  return {
    id: String(entry.id ?? index + 1),
    time: String(entry.time ?? ""),
    protocol: String(entry.protocol ?? "Unknown"),
    direction: String(entry.direction ?? ""),
    content: String(entry.content ?? ""),
    status: String(entry.status ?? "Unknown"),
    durationMs: Number(entry.durationMs ?? 0),
    errorType: String(entry.errorType ?? ""),
  };
}

function isMissingFileError(error: unknown) {
  return error instanceof Error && "code" in error && error.code === "ENOENT";
}
