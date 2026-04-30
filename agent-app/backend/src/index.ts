import cors from "cors";
import express from "express";
import type { ErrorRequestHandler } from "express";
import { getDefaultLogPath, loadCommunicationLogs, summarizeLogs } from "./logs.js";

const app = express();
const port = Number(process.env.PORT ?? 4317);

app.use(cors());

app.get("/api/health", (_request, response) => {
  response.json({
    status: "ok",
    service: "industrial-comm-agent-backend",
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

const errorHandler: ErrorRequestHandler = (error, _request, response, _next) => {
  console.error(error);
  response.status(500).json({
    error: "Failed to read communication logs",
    detail: error instanceof Error ? error.message : String(error),
  });
};

app.use(errorHandler);

app.listen(port, () => {
  console.log(`Agent backend listening on http://localhost:${port}`);
  console.log(`Reading logs from ${getDefaultLogPath()}`);
});
