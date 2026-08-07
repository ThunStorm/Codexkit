# 架构

应用是一个无第三方依赖的 C# 5 / .NET Framework 4.8 WinForms 可执行文件。

1. 每 2 秒检查 `ChatGPT.exe`，并要求路径包含 `WindowsApps\OpenAI.Codex_`。
2. 发现官方桌面应用后显示 `NotifyIcon`，并启动用户本地官方 `codex.exe app-server --listen stdio://`。
3. 每 60 秒读取额度；5 分钟没有有效数据即清空为 `--`。
4. 桌面应用退出时隐藏图标并终止子 App Server。

`Domain.cs` 负责额度窗口和状态规则，`AppServerClient.cs` 负责 JSONL 请求，`TrayApplicationContext.cs` 负责进程、托盘和启动项。单实例互斥量防止重复图标。

`app.manifest` 在控件创建前启用 Per-Monitor V2。图标渲染器读取任务栏 DPI，从 16/20/24/32/40/48/64px 中选择最近尺寸，并以 GDI+ `SingleBitPerPixelGridFit` 直接栅格化常规窄体数字，没有 `%`、ClearType 彩边、二次缩放或字形描边；可选灰点由设置控制。菜单和设置窗口使用系统字体及原生 DPI 缩放。

启动项只写当前用户注册表。程序移动后再次启动会刷新已启用启动项的路径。
