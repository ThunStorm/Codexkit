# 架构说明

## 目标

应用的稳定职责是读取并展示账户额度。任务状态领域模型保留在代码中，但没有事件源、菜单展示或颜色语义的生产接线。

## 数据流

```text
用户点击刷新 / 定时器 / 唤醒
  → AppState.refresh()
  → CodexProcessLocator
  → JSONRPCClient
  → codex app-server
  → account/read
  → account/rateLimits/read
  → RateLimitsReadResponse
  → UsageSelector
  → AppState.usage / quotaWindows
  → StatusItemView / NSMenu
```

UI 永不直接调用 RPC。所有不可用、过期和错误状态先归入 `AppState`，再生成 `MenuBarDisplayState`。

状态栏主数字由 `selectDisplayedWindow` 选择，优先 5 小时、缺失时回退周额度；例外是周额度窗口剩余 0% 时直接显示周额度窗口（即状态栏显示 0%），不再展示 5 小时剩余。菜单由 `selectMenuWindows` 选择：存在 5 小时窗口时依次展示 5 小时和周额度；任一窗口未返回则隐藏对应行。

## 生命周期

`AppDelegate` 采用 accessory activation policy，因此不显示 Dock 图标。它监测 `com.openai.codex` 的启动；若该应用已运行或随后启动，才创建 `AppState` 与菜单栏状态项。

启用“随 Codex / ChatGPT 启动”时，`SMAppService.mainApp` 把本应用注册为登录项。登录项负责等待 Codex/ChatGPT；macOS 不提供让 ChatGPT 直接启动第三方应用的公开机制。

## 任务状态预留

`AgentActivityState`、`RunningTaskSummary` 和 `TaskAggregator` 是未来数据源的领域接口。当前：

- `activity` 保持 `.unknown`；
- `runningTasks` 不进入菜单；
- `showTaskStatusDot` 默认 `false`；
- 开启设置后仅显示灰点。

不得在没有受支持事件源时把空闲、超时或没有日志误判为“已完成”。

## 安全边界

- 仅启动由本应用拥有的 `codex app-server` 子进程；
- 子进程移除常见敏感环境变量；
- 不读取 Keychain、认证文件、桌面窗口、系统通知或进程内存；
- 关闭应用时终止自己启动的 app-server；
- 不自动批准任何工具操作。
