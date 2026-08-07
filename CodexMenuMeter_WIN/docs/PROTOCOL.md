# App Server 协议

客户端通过标准输入输出发送逐行 JSON-RPC，顺序为：

1. `initialize`，随后发送 `initialized` 通知。
2. `account/read`，确认独立 CLI 已登录。
3. `account/rateLimits/read`，读取 `rateLimits.primary/secondary` 或 `rateLimitsByLimitId`。

额度窗口按 `windowDurationMins` 识别：240–360 分钟为 5 小时，9000–11000 分钟为一周。显示值为四舍五入后的 `100 - usedPercent`；越界、NaN、未知窗口或错误响应均不显示数字。

子进程不继承 `OPENAI_API_KEY`、`CODEX_API_KEY`、`AWS_SECRET_ACCESS_KEY` 和 `AWS_SESSION_TOKEN`。请求超时为 12 秒；stdout 未知通知被忽略，stderr 不进入界面。

协议依据：[OpenAI Codex App Server 文档](https://learn.chatgpt.com/docs/app-server)。
