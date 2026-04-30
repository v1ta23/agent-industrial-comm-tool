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
