# CodexMenuMeter Windows 11 托盘计量器设计

## 目标

在 Windows 11 上提供一个轻量、原生的 Codex 伴随程序。程序随当前用户登录静默启动，但只在官方 Codex Windows 桌面应用运行时显示右下角托盘图标。图标直接显示当前额度，例如 `69%`；点击后显示额度详情和可靠获取到的任务状态。

所有源码、测试、构建产物和文档均位于 `CodexMenuMeter_WIN`。

## 产品边界

- 只识别安装路径属于 `OpenAI.Codex` Windows 应用包的桌面进程，不把终端中的 `codex` CLI 或本程序启动的 `codex app-server` 子进程视为桌面应用。
- 只使用官方 `codex app-server` 接口，不读取认证文件、SQLite、会话 JSONL、提示词、命令、文件路径、diff、输出或进程内存。
- 5 小时额度窗口优先；缺失时回退到一周窗口。
- 额度不可用、未登录、数据无效或超过 5 分钟未成功更新时显示 `--%`，不沿用旧值。
- 任务状态必须先通过真实桌面任务验证。若独立 App Server 无法看到桌面应用的实时线程状态，则关闭任务状态功能并显示灰色，不做日志或超时推断。
- 不包含安装器、自动更新、代码签名、多账户或任务历史。

## 技术方案

采用 C# WinForms 和 Windows 自带的 .NET Framework 工具链，生成单文件 `CodexMenuMeter.exe`。使用 `NotifyIcon` 实现系统托盘，不引入 NuGet、Electron 或额外 SDK。

主要组件：

- `CodexProcessMonitor`：每 2 秒识别官方 Codex 桌面进程。
- `AppServerClient`：管理一个 `codex app-server` 子进程、JSONL 收发、请求 ID、超时和重连。
- `QuotaSelector`：解析额度窗口，按持续时间识别 5 小时与一周额度，计算剩余百分比。
- `TaskTracker`：轮询线程摘要，生成任务列表和红、黄、绿、灰汇总状态。
- `TrayApplicationContext`：控制托盘图标、菜单、定时器、启动项和退出清理。

数据流：

```text
Windows 登录
  -> CodexMenuMeter 静默启动
  -> 检测官方 Codex 桌面进程
  -> 启动 codex app-server
  -> initialize / initialized
  -> account/read
  -> account/rateLimits/read
  -> thread/list 可行性验证与轮询
  -> 动态托盘图标、提示和菜单
```

官方 App Server 文档说明了 JSON-RPC/JSONL 传输、`account/rateLimits/read`、`thread/list` 及线程运行状态。任务状态仍须在本机验证，因为 `thread/status/changed` 通知只对同一 App Server 内已加载的线程可靠。参考：<https://learn.chatgpt.com/docs/app-server>。

## 托盘界面

动态图标提供多尺寸位图。大号绘制 `0` 至 `100`，右上角绘制较小的 `%`；未知额度绘制 `--%`。图标使用高对比度前景色，背景色表达任务状态：

- 红色：等待授权、等待用户输入或系统错误。
- 黄色：至少一个任务正在运行。
- 绿色：此前观察到任务运行，现在已结束且当前无活动任务；绿色不表示成功。
- 灰色：从未观察到任务、状态源不可用或状态数据过期。

绿色保持到下一次任务开始或 Codex 退出。悬停文字示例：`Codex 5小时额度剩余 69% · 2 个任务运行中`。

点击菜单显示：

```text
Codex 额度
额度：5 小时剩余 69%
重置：2026/8/8 22:30
更新：刚刚
--------------------
任务：2 个运行中
修复登录问题 — 运行中 · 3 分钟
整理构建脚本 — 等待授权 · 1 分钟
--------------------
打开 Codex
刷新
随 Windows 登录启动 [x]
退出 CodexMenuMeter
```

最多展示 5 个当前任务。任务项只使用官方任务名称；名称不存在时显示“未命名任务”。每项可显示运行中、等待授权、等待输入、系统错误、运行时长和最近更新时间。任务结束后从列表移除，菜单顶部以绿色显示“当前无运行任务”。

程序无法强制 Windows 将托盘图标固定在任务栏常显区域。README 说明首次运行后如何从隐藏图标区域拖到右下角。

## 协议与状态规则

App Server 使用换行分隔的 JSON 消息。客户端发送一次 `initialize`，收到结果后发送 `initialized`，再发起其他请求。解析器必须处理 stdout 分片、未知通知和乱序响应，并且只按请求 ID 完成对应请求。

额度窗口按 `windowDurationMins` 分类，不依赖 `primary` 或 `secondary` 的位置：

- 5 小时：`240...360` 分钟。
- 一周：`9000...11000` 分钟。
- 其他周期不在托盘主显示中使用。

`usedPercent` 必须是 `0...100` 的有限数值；`remainingPercent = round(100 - usedPercent)`。`account/read` 只有在 `account` 为空时视为未登录，不能把 `requiresOpenaiAuth = true` 当作退出登录。

任务状态探测先调用 `thread/list`，仅读取任务摘要、名称、运行状态和更新时间。探测成功的最低标准是：在 Codex 桌面应用中启动测试任务后，独立 App Server 能观察到对应线程由非活动状态进入 `active`，结束后退出 `active`。若未满足此标准，发布版本不启用彩色任务状态和任务列表。

状态聚合优先级为红色、黄色、绿色、灰色。任何等待授权、等待输入或系统错误优先于运行状态。只有本次进程生命周期内确实观察到 `active -> 无 active` 转换才进入绿色。

## 生命周期与错误处理

- 程序首次运行时为当前用户写入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`；菜单开关可删除或恢复该值。
- Codex 未运行时不创建托盘图标、不启动 App Server。
- Codex 启动后创建托盘图标并立即刷新；Codex 退出后隐藏图标、终止子进程并清空内存状态。
- 额度每 60 秒刷新，任务每 2 秒刷新，菜单中的“刷新”同时触发两者。
- App Server 退出、请求超时或协议错误后关闭旧连接；下一次定时刷新重建连接。
- 额度失败立即进入不可用状态；成功数据超过 5 分钟后也进入不可用状态。
- 任务读取失败立即清空任务列表并变灰。
- 退出程序时必须删除动态图标并终止自己创建的 App Server，避免残留托盘图标或子进程。

## 测试与验收

自动检查覆盖：

- 5 小时窗口优先和周窗口回退。
- `usedPercent` 的 0、31、100、越界和非有限数值。
- Unix 秒级重置时间。
- `requiresOpenaiAuth = true` 且账户存在时仍视为已登录。
- JSONL 分片、未知通知、请求 ID 匹配和超时。
- 红色优先于黄色，运行转空闲为绿色，失联为灰色。
- 最多 5 个任务、无名称回退和结束任务移除。
- 只识别官方 Codex 桌面进程路径，排除 CLI 和子进程。

手工验收覆盖：

- `0%`、`9%`、`69%`、`100%` 和 `--%` 在 Windows 100%、125%、150% 缩放下可辨认。
- Codex 启动时出现、退出时隐藏，终端单独运行 CLI 时不出现。
- 实际账户额度与托盘显示一致，5 小时窗口缺失时正确回退。
- 任务状态探测满足最低标准后才启用彩色状态和任务列表。
- 菜单在浅色、深色和高对比度主题下可读。
- 开机启动开关生效，退出后无残留 App Server。

构建脚本先编译并运行测试，再生成 Windows GUI 子系统的 `CodexMenuMeter.exe`。完成声明必须以本机最新测试、构建结果和手工探测结果为依据。

## 文档调整

- 重写 `README.md` 为 Windows 11 安装、运行、固定托盘图标、构建和卸载说明。
- 将 `codex_macos_menu_bar_usage_status_plan.md` 更名为 Windows 需求文档。
- 重写 `docs/ARCHITECTURE.md`、`docs/PROTOCOL.md`、`docs/TESTING.md`、`docs/TASK_STATUS.md` 和 `docs/feasibility.md`。
- 删除与新设计重复且已经过时的 `plan_preview.md`。
- 设计、实施计划、源码、测试、构建脚本和二进制产物全部保留在 `CodexMenuMeter_WIN`。
