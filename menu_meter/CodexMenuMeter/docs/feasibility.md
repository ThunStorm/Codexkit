# Phase 0 可行性记录

- 测试环境：macOS 26.5.2（构建 25F84），Codex CLI `0.147.0-alpha.1.2`。
- 官方 `codex app-server generate-json-schema --experimental` 明确包含 `account/rateLimits/read`、`account/rateLimits/updated`、`turn/started`、`turn/completed`、`thread/status/changed` 和审批/阻塞输入事件。
- 本机没有运行中的受管 app-server control socket：`~/.codex/app-server-control/app-server-control.sock` 不存在；无法据此订阅 Codex 桌面应用的既有会话。
- 因此本实现对任务状态采用 **Level D**：任务状态始终灰色并在菜单中说明“桌面任务状态暂不可用”。不会从 UI、通知、日志或静默超时推断任务状态。
- 额度读取使用独立、官方 `codex app-server --stdio` 客户端，并先以 `account/read` 仅确认账户对象存在（不将 `requiresOpenaiAuth` 误判为已登出），再读取 `account/rateLimits/read`；它不读取或导出令牌，也不修改 Codex.app。若连接或登录不可用，菜单栏显示 `--%`。

后续只有在 Codex 提供稳定、受支持的桌面会话订阅能力后，才可实现 Level A 任务事件源。
