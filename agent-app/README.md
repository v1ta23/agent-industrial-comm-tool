# TypeScript Agent App

这是通信工具的 Agent 工作台。它只读取日志、展示数据和给排查建议，不会控制设备，也不会向 WinForms 发送指令。

## 数据怎么流过来

WinForms 每次模拟通信后，把结构化日志写进：

```text
D:\jetbrains\c#\industrial-comm-tool\data\comm-logs.json
```

Agent 后端读取这个 JSON 文件，再通过接口给前端：

```text
WinForms -> data/comm-logs.json -> backend /api/logs -> frontend ECharts
```

说白了：WinForms 负责产生日志，Agent 后端负责读取和分析，前端负责把图表和建议展示出来。

## 当前页面

- 概览指标：总日志、成功率、失败次数、平均耗时
- 响应耗时折线图：读取 `durationMs`
- 成功 / 失败统计图：读取 `status`
- 异常类型图：读取 `errorType`
- 协议分布图：读取 `protocol`
- 最近通信日志：显示最新 8 条记录
- Agent 判断：后端 `/api/analysis` 根据失败次数、异常类型和平均耗时给排查方向

## 运行方式

第一次运行先安装依赖：

```powershell
cd D:\jetbrains\c#\industrial-comm-tool\agent-app
npm install
```

开两个 PowerShell 窗口：

```powershell
cd D:\jetbrains\c#\industrial-comm-tool\agent-app
npm run dev:backend
```

```powershell
cd D:\jetbrains\c#\industrial-comm-tool\agent-app
npm run dev:frontend
```

默认地址：

- 后端：`http://localhost:4317`
- 前端：`http://localhost:5173`

## 折中使用方式

启动 WinForms 后，在左侧菜单点：

```text
数据看板
```

WinForms 里保留轻量看板，直接读取同一个 `data/comm-logs.json`。

如果要看更完整的 Agent 工作台，点 WinForms 页面右上角的：

```text
Agent工作台
```

它会打开 `http://localhost:5173`。这部分用网页前端做，适合放 ECharts、分析卡片和后续 Agent 对话。

## 目录结构

```text
agent-app
├── backend
│   ├── src
│   │   ├── index.ts
│   │   └── logs.ts
│   ├── package.json
│   └── tsconfig.json
├── frontend
│   ├── src
│   │   ├── App.tsx
│   │   ├── main.tsx
│   │   └── styles.css
│   ├── index.html
│   ├── package.json
│   └── vite.config.ts
├── package.json
└── README.md
```

## 接口

- `GET /api/health`：检查后端是否启动
- `GET /api/logs`：读取 `data/comm-logs.json`，返回日志数组和统计数据
- `GET /api/analysis`：读取日志并返回 Agent 分析建议
