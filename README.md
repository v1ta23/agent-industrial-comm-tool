# Industrial Comm Tool

一个用于学习和验证工业通信流程的 WinForms 工具。

<img width="1209" height="967" alt="image" src="https://github.com/user-attachments/assets/82f16a41-f9d8-442a-ab6d-8b13bf590e55" />

<img width="1569" height="1127" alt="image" src="https://github.com/user-attachments/assets/2bc95048-96ae-4bf5-804b-37bedaa435cd" />

它现在主要做两件事：

- WinForms 端负责通信调试、模拟通信、配置保存和通信日志落盘。
- `agent-app` 负责读取日志、展示数据看板，并给出排查建议。

## 当前功能

- TCP 调试页面：连接、断开、发送命令、接收返回、记录通信日志。
- 数据看板：从本地 JSON 日志读取通信结果，展示成功率、失败数、平均耗时等信息。
- Agent 工作台：通过 WebView2 打开本地网页工作台。
- 日志持久化：通信记录保存到 `data/comm-logs.json`。
- 协议分诊：后端按 TCP / Serial / ModbusTCP 分开统计和给建议。
- 大模型分析：通过 LangChain 调用 OpenAI 兼容接口，但默认不会自动调用，只有手动触发时才会消耗模型额度。

## 技术组成

```text
industrial-comm-tool
├─ WinForms 主程序
│  ├─ Form1.cs
│  ├─ AgentWorkbenchForm.cs
│  ├─ AgentAppRuntime.cs
│  ├─ CommunicationLogStore.cs
│  └─ CommunicationConfigStore.cs
├─ agent-app
│  ├─ backend    Node.js + Express + TypeScript + LangChain
│  └─ frontend   React + Vite + TypeScript + ECharts
└─ data
   └─ comm-logs.json
```

## 环境要求

- Windows
- .NET 8 SDK
- Node.js + npm
- WebView2 Runtime

如果只运行 WinForms 主程序，先保证 .NET 8 SDK 可用。

如果要打开 Agent 工作台，还需要安装 `agent-app` 的 npm 依赖。

## 第一次安装

在项目根目录执行：

```powershell
cd D:\jetbrains\c#\industrial-comm-tool
dotnet restore .\industrial-comm-tool.sln
```

安装 Agent 工作台依赖：

```powershell
cd D:\jetbrains\c#\industrial-comm-tool\agent-app
npm install
```

## 运行 WinForms 主程序

```powershell
cd D:\jetbrains\c#\industrial-comm-tool
dotnet run --project .\industrial-comm-tool.csproj
```

启动后可以直接使用左侧菜单里的 TCP 调试、数据看板等页面。

## 运行 Agent 工作台

推荐方式是让 WinForms 自动检查并打开 Agent 工作台。入口在 WinForms 页面右上角的 `Agent工作台` 按钮。

当前工作台走单地址：

```text
http://127.0.0.1:4317/workbench
```

如果要手动启动：

```powershell
cd D:\jetbrains\c#\industrial-comm-tool\agent-app
npm run dev:workbench
```

这个命令会先构建前端，再启动后端。后端默认监听 `4317`，并把前端页面挂到 `/workbench`。

## 数据怎么流

```text
WinForms 通信操作
        ↓
data/comm-logs.json
        ↓
agent-app/backend
        ↓
agent-app/frontend 工作台
```

日志字段大概长这样：

```json
{
  "id": "1",
  "time": "2026-04-27 10:30:12",
  "protocol": "TCP",
  "direction": "Send",
  "content": "01 03 00 00 00 02",
  "status": "Success",
  "durationMs": 35,
  "errorType": ""
}
```

这几个字段里，`protocol` 用来区分 TCP / Serial / ModbusTCP，`status` 用来看成功失败，`durationMs` 用来看耗时，`errorType` 用来判断主要异常类型。

## 后端接口

默认后端地址：

```text
http://127.0.0.1:4317
```

常用接口：

- `GET /api/health`：检查后端是否启动。
- `GET /api/logs`：读取通信日志和统计结果。
- `GET /api/log-state`：读取日志文件指纹，用来做低成本刷新判断。
- `GET /api/workbench`：返回工作台需要的日志、统计、规则分析和模型分析状态。
- `GET /api/workbench?model=1`：手动触发模型分析。
- `GET /api/analysis`：只返回分析结果。
- `GET /api/llm-analysis`：直接调用大模型分析。

## 配置大模型分析

复制模板：

```powershell
cd D:\jetbrains\c#\industrial-comm-tool\agent-app\backend
copy .env.example .env
```

然后填写：

```text
LLM_API_KEY=你的 key
LLM_MODEL=你的模型名
LLM_BASE_URL=你的 OpenAI 兼容接口地址
```

改完 `.env` 后要重启后端，否则新配置不会生效。

注意：默认打开页面不会自动调用大模型。只有手动分析，或者请求里带 `model=1`，才会调用模型。

## 构建和检查

WinForms 构建：

```powershell
cd D:\jetbrains\c#\industrial-comm-tool
dotnet build .\industrial-comm-tool.sln
```

Agent 类型检查和构建：

```powershell
cd D:\jetbrains\c#\industrial-comm-tool\agent-app
npm run check
npm run build
```
