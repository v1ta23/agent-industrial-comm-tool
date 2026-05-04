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
  protocolTriage: ProtocolLogTriage[];
  generatedAt: string;
};

export type ProtocolLogTriage = {
  protocol: string;
  displayName: string;
  total: number;
  failed: number;
  latestFailure: CommunicationLogEntry | null;
  mainErrorType: string;
  averageDurationMs: number;
  suggestion: string;
};

const currentDirectory = path.dirname(fileURLToPath(import.meta.url));
const triageProtocols = [
  { protocol: "TCP", displayName: "TCP" },
  { protocol: "Serial", displayName: "串口" },
  { protocol: "ModbusTCP", displayName: "ModbusTCP" },
] as const;

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
  const protocolTriage = buildProtocolTriage(entries);

  if (entries.length === 0) {
    return {
      level: "normal",
      summary: "还没有通信日志",
      focus: "先生成通信数据",
      suggestions: protocolTriage.map(formatProtocolSuggestion),
      evidence: ["日志文件为空，Agent 暂时没有可分析的数据。"],
      protocolTriage,
      generatedAt: new Date().toISOString(),
    };
  }

  const summary = summarizeLogs(entries);
  const failedEntries = entries.filter((entry) => entry.status !== "Success");
  const slowEntries = entries.filter((entry) => entry.durationMs > Math.max(80, summary.averageDurationMs * 2));
  const failedProtocols = protocolTriage.filter((item) => item.failed > 0);
  const suggestions = protocolTriage.map(formatProtocolSuggestion);
  const evidence: string[] = [
    `总日志 ${summary.total} 条，成功 ${summary.success} 条，失败 ${summary.failed} 条。`,
    `平均响应耗时 ${summary.averageDurationMs} ms。`,
    ...protocolTriage.map(formatProtocolEvidence),
  ];

  let level: CommunicationLogAnalysis["level"] = "normal";
  let focus = "各协议暂未发现失败";
  let summaryText = "各协议日志看起来稳定";

  if (failedEntries.length > 0) {
    level = failedEntries.length >= 3 ? "critical" : "warning";
    focus = `优先排查 ${failedProtocols.map((item) => item.displayName).join("、")}`;
    summaryText = `按协议发现 ${failedEntries.length} 条失败通信`;
  }

  if (slowEntries.length > 0) {
    level = level === "normal" ? "warning" : level;
    evidence.push(`慢响应 ${slowEntries.length} 条，最高 ${Math.max(...slowEntries.map((entry) => entry.durationMs))} ms。`);
  }

  return {
    level,
    summary: summaryText,
    focus,
    suggestions,
    evidence,
    protocolTriage,
    generatedAt: new Date().toISOString(),
  };
}

function buildProtocolTriage(entries: CommunicationLogEntry[]): ProtocolLogTriage[] {
  return triageProtocols.map(({ protocol, displayName }) => {
    const protocolEntries = entries.filter((entry) => getProtocolGroup(entry.protocol) === protocol);
    const failedEntries = protocolEntries.filter((entry) => entry.status !== "Success");
    const durationEntries = protocolEntries.filter((entry) => entry.durationMs > 0);
    const latestFailure = failedEntries.at(-1) ?? null;
    const mainErrorType = getMainErrorType(failedEntries);
    const averageDurationMs = durationEntries.length === 0
      ? 0
      : Math.round(durationEntries.reduce((total, entry) => total + entry.durationMs, 0) / durationEntries.length);

    return {
      protocol,
      displayName,
      total: protocolEntries.length,
      failed: failedEntries.length,
      latestFailure,
      mainErrorType,
      averageDurationMs,
      suggestion: buildProtocolSuggestion(protocol, protocolEntries.length, failedEntries.length, mainErrorType, averageDurationMs),
    };
  });
}

function getProtocolGroup(protocol: string) {
  const normalized = protocol.trim().toLowerCase().replace(/\s+/g, "");

  if (normalized === "tcp" || normalized === "tcpsim" || normalized === "tcp-sim") {
    return "TCP";
  }

  if (normalized === "serial" || normalized === "串口") {
    return "Serial";
  }

  if (normalized === "modbustcp" || normalized === "modbus-tcp") {
    return "ModbusTCP";
  }

  return protocol || "Unknown";
}

function getMainErrorType(entries: CommunicationLogEntry[]) {
  const counts: Record<string, number> = {};

  for (const entry of entries) {
    const errorType = entry.errorType.trim() || "Unknown";
    counts[errorType] = (counts[errorType] ?? 0) + 1;
  }

  return Object.entries(counts).sort((left, right) => right[1] - left[1])[0]?.[0] ?? "None";
}

function buildProtocolSuggestion(
  protocol: string,
  total: number,
  failed: number,
  mainErrorType: string,
  averageDurationMs: number,
) {
  if (total === 0) {
    if (protocol === "TCP") {
      return "还没有 TCP 日志，先到 TCP 调试页发送一次指令。";
    }

    if (protocol === "Serial") {
      return "还没有串口日志，先到串口页打开模拟串口并发送一次。";
    }

    return "还没有 ModbusTCP 日志，先到 Modbus TCP 页读或写一次寄存器。";
  }

  if (failed === 0) {
    if (averageDurationMs > 100) {
      return "暂时没有失败，但平均耗时偏高，先对照时间点看设备负载和网络状态。";
    }

    return "暂时没有失败，继续保留日志观察。";
  }

  if (protocol === "TCP") {
    if (mainErrorType === "Disconnected") {
      return "先查连接是否断开，再查 IP、端口和模拟设备是否在线。";
    }

    if (mainErrorType === "Timeout") {
      return "先查设备有没有返回，再查网络延迟和超时时间设置。";
    }

    return "先按最新失败内容复现一次，再看连接状态和返回内容。";
  }

  if (protocol === "Serial") {
    if (mainErrorType === "CrcError") {
      return "先查波特率、校验位、停止位和协议格式是否一致。";
    }

    if (mainErrorType === "PortClosed") {
      return "先打开串口，再确认 COM 口号和 USB 转串口驱动。";
    }

    return "先确认串口已打开，再查线缆、参数和设备是否返回。";
  }

  if (mainErrorType === "ModbusException") {
    return "先查功能码、寄存器地址、数量和 UnitId 是否符合设备表。";
  }

  if (mainErrorType === "InvalidAddress" || mainErrorType === "InvalidValue") {
    return "先把寄存器地址、数量和值改成合法数字。";
  }

  return "先查 Modbus TCP 连接、UnitId、寄存器地址和设备异常码。";
}

function formatProtocolSuggestion(item: ProtocolLogTriage) {
  const latestFailure = item.latestFailure
    ? `${item.latestFailure.time || "无时间"}，${item.latestFailure.content || "无内容"}`
    : "无";
  const averageDuration = item.averageDurationMs === 0 ? "-- ms" : `${item.averageDurationMs} ms`;
  const mainError = item.mainErrorType === "None" ? "无" : item.mainErrorType;

  return `${item.displayName}：最近失败 ${latestFailure}；主错误 ${mainError}；平均耗时 ${averageDuration}；下一步 ${item.suggestion}`;
}

function formatProtocolEvidence(item: ProtocolLogTriage) {
  const averageDuration = item.averageDurationMs === 0 ? "-- ms" : `${item.averageDurationMs} ms`;
  const mainError = item.mainErrorType === "None" ? "无" : item.mainErrorType;

  return `${item.displayName}：总 ${item.total} 条，失败 ${item.failed} 条，主错误 ${mainError}，平均耗时 ${averageDuration}。`;
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
