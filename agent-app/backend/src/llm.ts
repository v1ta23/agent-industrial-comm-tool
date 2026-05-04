import { config as loadEnv } from "dotenv";
import { HumanMessage, SystemMessage } from "@langchain/core/messages";
import { ChatOpenAI } from "@langchain/openai";
import path from "node:path";
import { fileURLToPath } from "node:url";
import type { CommunicationLogAnalysis, CommunicationLogEntry } from "./logs.js";

const currentDirectory = path.dirname(fileURLToPath(import.meta.url));
loadEnv({ path: path.resolve(currentDirectory, "..", ".env") });
const browserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

export type ModelAnalysis = {
  status: "disabled" | "ok" | "error";
  provider: string;
  model: string;
  summary: string;
  suggestions: string[];
  rawText: string;
  generatedAt: string;
  error?: string;
};

type ModelConfig = {
  apiKey: string;
  baseUrl?: string;
  model: string;
  provider: string;
};

type ParsedModelText = {
  summary?: string;
  suggestions?: string[];
};

export async function analyzeLogsWithModel(
  entries: CommunicationLogEntry[],
  ruleAnalysis: CommunicationLogAnalysis,
): Promise<ModelAnalysis> {
  const config = readModelConfig();

  if (!config) {
    return {
      status: "disabled",
      provider: "openai-compatible",
      model: "",
      summary: "大模型分析未启用",
      suggestions: ["设置 LLM_API_KEY 和 LLM_MODEL 后，LangChain 才会调用外部大模型。"],
      rawText: "",
      generatedAt: new Date().toISOString(),
    };
  }

  try {
    const model = new ChatOpenAI({
      apiKey: config.apiKey,
      configuration: {
        ...(config.baseUrl ? { baseURL: config.baseUrl } : {}),
        defaultHeaders: {
          "User-Agent": browserUserAgent,
        },
        timeout: 30_000,
      },
      maxRetries: 0,
      model: config.model,
      temperature: 0.2,
      useResponsesApi: false,
    });
    const result = await model.invoke([
      new SystemMessage("你是工业通信日志分析助手。只根据用户给出的日志事实判断，不要编造设备信息，不要生成控制设备的指令。"),
      new HumanMessage(buildPrompt(entries, ruleAnalysis)),
    ]);
    const rawText = messageContentToText(result.content);
    const parsed = parseModelText(rawText);
    const summary = parsed.summary || rawText || "大模型没有返回可读内容";
    const suggestions = parsed.suggestions?.length ? parsed.suggestions : [summary];

    return {
      status: "ok",
      provider: config.provider,
      model: config.model,
      summary,
      suggestions,
      rawText,
      generatedAt: new Date().toISOString(),
    };
  } catch (error) {
    return {
      status: "error",
      provider: config.provider,
      model: config.model,
      summary: "大模型分析失败",
      suggestions: ["本地规则分析仍然可用；先检查 LLM_API_KEY、LLM_MODEL 和 LLM_BASE_URL 是否正确。"],
      rawText: "",
      generatedAt: new Date().toISOString(),
      error: error instanceof Error ? error.message : String(error),
    };
  }
}

function readModelConfig(): ModelConfig | null {
  const apiKey = process.env.LLM_API_KEY ?? process.env.OPENAI_API_KEY;
  const model = process.env.LLM_MODEL ?? process.env.OPENAI_MODEL;
  const baseUrl = process.env.LLM_BASE_URL ?? process.env.OPENAI_BASE_URL;
  const provider = process.env.LLM_PROVIDER ?? "openai-compatible";

  if (!apiKey || !model) {
    return null;
  }

  return {
    apiKey,
    baseUrl,
    model,
    provider,
  };
}

function buildPrompt(entries: CommunicationLogEntry[], ruleAnalysis: CommunicationLogAnalysis) {
  const failedEntries = entries.filter((entry) => entry.status !== "Success").slice(-12);
  const slowEntries = entries
    .filter((entry) => entry.durationMs > 0)
    .sort((left, right) => right.durationMs - left.durationMs)
    .slice(0, 8);

  return `
请分析下面这批工业通信日志。

要求：
1. 只根据给出的事实判断，不要脑补不存在的设备、协议或现场环境。
2. 不要建议自动下发控制指令，只给人工排查建议。
3. 输出 JSON，格式必须是：
{
  "summary": "一句话总结",
  "suggestions": ["建议1", "建议2", "建议3"]
}

本地规则分析结果：
${JSON.stringify(ruleAnalysis, null, 2)}

最近失败日志：
${JSON.stringify(failedEntries, null, 2)}

耗时最高的日志：
${JSON.stringify(slowEntries, null, 2)}
`.trim();
}

function messageContentToText(content: unknown) {
  if (typeof content === "string") {
    return content.trim();
  }

  if (Array.isArray(content)) {
    return content
      .map((part) => {
        if (typeof part === "string") {
          return part;
        }

        if (part && typeof part === "object" && "text" in part) {
          return String((part as { text?: unknown }).text ?? "");
        }

        return "";
      })
      .join("")
      .trim();
  }

  return "";
}

function parseModelText(rawText: string): ParsedModelText {
  const jsonText = rawText.match(/\{[\s\S]*\}/)?.[0] ?? rawText;

  try {
    const parsed = JSON.parse(jsonText) as ParsedModelText;

    return {
      summary: typeof parsed.summary === "string" ? parsed.summary : undefined,
      suggestions: Array.isArray(parsed.suggestions)
        ? parsed.suggestions.filter((item): item is string => typeof item === "string" && item.trim().length > 0)
        : undefined,
    };
  } catch {
    return {};
  }
}
