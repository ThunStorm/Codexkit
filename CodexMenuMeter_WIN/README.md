# CodexMenuMeter for Windows 11

Windows 11 原生托盘计量器。官方 Codex 桌面应用运行时，右下角以尽可能大的粗体整数显示真实 App Server 返回的剩余额度，例如 `69`；Codex 退出后图标自动隐藏。

## 使用

1. 先在终端运行 `codex login`，让独立 Codex CLI 完成登录。桌面应用的登录态不会共享给独立 App Server。
2. 将 `dist\CodexMenuMeter.exe` 复制到长期保留目录后运行。首次运行会写入当前用户的登录启动项，无需管理员权限；不要从临时下载目录开启启动项。
3. Windows 可能把新图标收进托盘折叠区，可将它拖到任务栏右侧常显。

额度不可读时显示 `--`，绝不填入演示数据。点击图标可查看窗口类型、重置时间和更新时间，并可刷新、打开设置或退出。设置中可启用灰色任务状态预留点及 Windows 登录启动；启动项位于 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，保存设置会刷新当前 EXE 路径。

## 构建和真实接口验收

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Live
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

`-Test` 只检查计算、协议解析和进程识别；`-Live` 必须连接本机官方 CLI/App Server 并读取真实额度，失败时返回非零退出码，不使用模拟数据。

## 当前限制

- 当前桌面 Codex 的 App Server 使用私有父子进程 stdio；独立 App Server 的 `thread/list` 看不到桌面任务。菜单不显示任务状态；可选状态点默认关闭，开启后也只显示灰色预留点。
- 程序使用 Per-Monitor V2，从 16/20/24/32/40/48/64px 中选择最接近任务栏 DPI 的原生帧；数字使用 GDI+ 单色网格提示的 Segoe UI Bold，并以约 1.30 倍纵向比例增强、上移 2 个原生像素，不绘制 `%`、ClearType 彩边或模糊描边。
- `dist\CodexMenuMeter.exe` 未签名，首次运行可能出现 SmartScreen 提示。
- 卸载：在托盘菜单退出，关闭“随 Windows 登录启动”，再删除本目录。启动项位于 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`。

详见 [架构](docs/ARCHITECTURE.md)、[协议](docs/PROTOCOL.md)、[测试](docs/TESTING.md) 和 [任务状态结论](docs/TASK_STATUS.md)。
