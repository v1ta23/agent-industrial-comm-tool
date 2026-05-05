import cors from "cors";
import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import express from "express";
import path from "node:path";
import { fileURLToPath } from "node:url";
import type { ErrorRequestHandler } from "express";
import { analyzeLogsWithModel, type ModelAnalysis } from "./llm.js";
import { analyzeLogs, getDefaultLogPath, loadCommunicationLogs, summarizeLogs } from "./logs.js";

const app = express();
const port = Number(process.env.PORT ?? 4317);
const currentDirectory = path.dirname(fileURLToPath(import.meta.url));
const frontendDistPath = path.resolve(currentDirectory, "..", "..", "frontend", "dist");
const workbenchIndexPath = path.join(frontendDistPath, "index.html");

app.use(cors());

app.get(["/workbench", "/workbench/"], serveWorkbenchIndex);
app.use("/workbench", express.static(frontendDistPath, { dotfiles: "allow", fallthrough: true, index: false }));
app.get(/^\/workbench\/(?!assets(?:\/|$)).*$/, serveWorkbenchIndex);

function serveWorkbenchIndex(_request: express.Request, response: express.Response, next: express.NextFunction) {
  response.sendFile(workbenchIndexPath, { dotfiles: "allow" }, (error) => {
    if (error) {
      next(error);
    }
  });
}

app.get("/api/health", (_request, response) => {
  response.json({
    status: "ok",
    service: "industrial-comm-agent-backend",
    workbench: "/workbench",
  });
});

app.get("/api/logs", async (_request, response, next) => {
  try {
    const items = await loadCommunicationLogs();

    response.json({
      source: getDefaultLogPath(),
      total: items.length,
      summary: summarizeLogs(items),
      items,
    });
  } catch (error) {
    next(error);
  }
});

app.get("/api/log-state", async (_request, response, next) => {
  try {
    response.json(await getLogFileState());
  } catch (error) {
    next(error);
  }
});

app.get("/api/workbench", async (request, response, next) => {
  try {
    const items = await loadCommunicationLogs();
    const analysis = analyzeLogs(items);
    const includeModel = shouldRunModelAnalysis(request.query.model);

    response.json({
      source: getDefaultLogPath(),
      total: items.length,
      summary: summarizeLogs(items),
      analysis,
      modelAnalysis: includeModel ? await analyzeLogsWithModel(items, analysis) : getSkippedModelAnalysis(),
      logState: await getLogFileState(),
      items,
    });
  } catch (error) {
    next(error);
  }
});

app.get("/api/analysis", async (request, response, next) => {
  try {
    const items = await loadCommunicationLogs();
    const analysis = analyzeLogs(items);
    const includeModel = shouldRunModelAnalysis(request.query.model);

    response.json({
      source: getDefaultLogPath(),
      total: items.length,
      analysis,
      modelAnalysis: includeModel ? await analyzeLogsWithModel(items, analysis) : getSkippedModelAnalysis(),
    });
  } catch (error) {
    next(error);
  }
});

app.get("/api/llm-analysis", async (_request, response, next) => {
  try {
    const items = await loadCommunicationLogs();
    const analysis = analyzeLogs(items);

    response.json({
      source: getDefaultLogPath(),
      total: items.length,
      modelAnalysis: await analyzeLogsWithModel(items, analysis),
    });
  } catch (error) {
    next(error);
  }
});

const errorHandler: ErrorRequestHandler = (error, _request, response, _next) => {
  console.error(error);
  response.status(500).json({
    error: "Failed to read communication logs",
    detail: error instanceof Error ? error.message : String(error),
  });
};

app.use(errorHandler);

function shouldRunModelAnalysis(value: unknown) {
  const rawValue = Array.isArray(value) ? value[0] : value;
  return rawValue === "1" || rawValue === "true";
}

function getSkippedModelAnalysis(): ModelAnalysis {
  return {
    status: "disabled",
    provider: "openai-compatible",
    model: "",
    summary: "大模型分析未自动调用",
    suggestions: ["为了避免重复消耗 token，只有手动重新分析时才会调用大模型。"],
    rawText: "",
    generatedAt: new Date().toISOString(),
  };
}

async function getLogFileState() {
  const source = getDefaultLogPath();

  try {
    const raw = await readFile(source, "utf8");
    return buildLogFileState(source, true, raw);
  } catch (error) {
    if (getErrorCode(error) === "ENOENT") {
      return buildLogFileState(source, false, "");
    }

    throw error;
  }
}

function buildLogFileState(source: string, exists: boolean, raw: string) {
  return {
    source,
    exists,
    size: raw.length,
    fingerprint: createHash("sha256").update(raw).digest("hex"),
    checkedAt: new Date().toISOString(),
  };
}

function getErrorCode(error: unknown) {
  return error && typeof error === "object" && "code" in error
    ? String((error as { code?: unknown }).code)
    : "";
}

app.listen(port, () => {
  console.log(`Agent backend listening on http://localhost:${port}`);
  console.log(`Agent workbench served at http://localhost:${port}/workbench`);
  console.log(`Reading logs from ${getDefaultLogPath()}`);
});
