# 架构

应用是一个无第三方依赖的 C# 5 / .NET Framework 4.8 WinForms 可执行文件。

1. 每 2 秒检查 `ChatGPT.exe`，并要求路径包含 `WindowsApps\OpenAI.Codex_`。
2. 发现官方桌面应用后显示 `NotifyIcon`，并启动用户本地官方 `codex.exe app-server --listen stdio://`。
3. 每 60 秒读取额度；5 分钟没有有效数据即清空为 `--%`。
4. 桌面应用退出时隐藏图标并终止子 App Server。

`Domain.cs` 负责额度窗口和状态规则，`AppServerClient.cs` 负责 JSONL 请求，`TrayApplicationContext.cs` 负责进程、托盘和启动项。单实例互斥量防止重复图标。

启动项只写当前用户注册表。程序移动后再次启动会刷新已启用启动项的路径。
