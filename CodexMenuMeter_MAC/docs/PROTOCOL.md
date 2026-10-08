# Codex app-server 协议说明

## 已验证传输

本项目针对本机 Codex CLI `0.147.0-alpha.1.2` 验证的传输为 UTF-8 JSONL：每个 JSON-RPC 2.0 消息以换行结束。

```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"clientInfo":{"name":"CodexMenuMeter","version":"0.1.1"}}}
```

不使用 `Content-Length`。将 LSP/MCP 风格 framing 发送给该 app-server 会导致反序列化错误。

## 启动序列

1. 启动 `codex app-server --stdio`。
2. 请求 `initialize`。
3. 在成功响应后发送 `initialized` 通知。
4. 请求 `account/read`，仅确认 `account` 是否存在。
5. 请求 `account/rateLimits/read`。

`requiresOpenaiAuth` 是 transport/auth 能力标记；它在已登录 ChatGPT 账户上可以为 `true`，不是登出条件。

## 额度字段

`rateLimits.primary` 和 `rateLimits.secondary` 均可能为空，也都可能是任意周期。依 `windowDurationMins` 识别：

| 类型 | 容差 | 主显示优先级 |
| --- | --- | --- |
| 5 小时 | 240...360 分钟 | 1 |
| 一周 | 9000...11000 分钟 | 2 |
| 月度 | 38000...50000 分钟 | 不显示 |

有效窗口要求 `usedPercent` 是有限数且位于 `0...100`。`remainingPercent = round(100 - usedPercent)`。`resetsAt` 是 Unix 秒级时间戳，按用户本地时区格式化。

## 兼容性与错误处理

- 不依赖 `primary`/`secondary` 的语义顺序；
- 接收未知通知时忽略，不中断请求队列；
- stdout 可以分片，读取缓冲区必须按换行拼接；
- 成功后 60 秒刷新；失败后在 10、20、40、60 秒后重试，后续保持 60 秒；成功后恢复正常周期；
- 超时、网络错误、CLI 暂不可用或空额度响应时保留上次成功值及其更新时间，菜单与提示标明缓存状态；不因超过 5 分钟或重置时间已过而清空或自动计算新额度；
- 成功值持久化到本应用的 UserDefaults；重启时显示上次成功值并核对账户。账户仅保存类型及邮箱的 SHA-256 指纹，不保存邮箱或令牌；确认未登录、切换账户或改用 API key 账户时清空旧额度与缓存；
- 超时、子进程退出、CLI 或账户状态变化后，重建连接并重新执行完整启动序列；
- EOF 时立即取消 stdout 读取回调，关闭所有管道句柄；旧连接回调不得影响新连接。stderr 写入空设备，避免未读取的管道填满并阻塞请求；未结束的 stdout 消息缓冲上限为 1 MiB；
- 不记录账户对象、邮箱或任何认证响应原文。
