# CodexMenuMeter for Windows 11

Windows 11 原生托盘计量器。官方 Codex 桌面应用运行时，右下角显示真实 App Server 返回的剩余额度，例如 `69%`；Codex 退出后图标自动隐藏。

## 使用

1. 先在终端运行 `codex login`，让独立 Codex CLI 完成登录。桌面应用的登录态不会共享给独立 App Server。
2. 运行 `dist\CodexMenuMeter.exe`。首次运行会写入当前用户的登录启动项，无需管理员权限。
3. Windows 可能把新图标收进托盘折叠区，可将它拖到任务栏右侧常显。

额度不可读时显示 `--%`，绝不填入演示数据。点击图标可查看窗口类型、重置时间、更新时间，以及刷新、启动项和退出操作。

## 构建和真实接口验收

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Live
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

`-Test` 只检查计算、协议解析和进程识别；`-Live` 必须连接本机官方 CLI/App Server 并读取真实额度，失败时返回非零退出码，不使用模拟数据。

## 当前限制

- 当前桌面 Codex 的 App Server 使用私有父子进程 stdio；独立 App Server 的 `thread/list` 看不到桌面任务。任务状态因此保持灰色并显示“未共享”，不推测运行或完成状态。
- `dist\CodexMenuMeter.exe` 未签名，首次运行可能出现 SmartScreen 提示。
- 卸载：在托盘菜单退出，关闭“随 Windows 登录启动”，再删除本目录。启动项位于 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`。

详见 [架构](docs/ARCHITECTURE.md)、[协议](docs/PROTOCOL.md)、[测试](docs/TESTING.md) 和 [任务状态结论](docs/TASK_STATUS.md)。
