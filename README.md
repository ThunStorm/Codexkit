# CodexMenuMeter

CodexMenuMeter 是一个原生桌面额度计量器：仅在官方 Codex 桌面应用运行时显示，读取独立官方 `codex app-server` 返回的真实额度，不读取本地会话、提示词、命令或凭据。

| 平台 | 源码目录 | 当前通知栏样式 | 成品 |
| --- | --- | --- | --- |
| Windows 11 | `CodexMenuMeter_WIN` | Segoe UI Bold、纵向增强并略上移、无 `%` | `CodexMenuMeter-Windows.exe` |
| macOS 14+ | `CodexMenuMeter_MAC` | 菜单栏额度文字 | `CodexMenuMeter-macOS.app` |

详细部署与系统配置见 [部署指南](docs/DEPLOYMENT.md)。

## 快速开始

### Windows 11

1. 在 PowerShell 执行 `codex login`。Codex 桌面版的登录态不会自动共享给独立 CLI。
2. 将 `CodexMenuMeter-Windows.exe` 放入一个长期保留的位置，例如 `%LOCALAPPDATA%\CodexMenuMeter\`，再运行它一次。
3. 首次运行默认写入当前用户的登录启动项；需要调整时，右键托盘图标并进入“设置…”。
4. Windows 可能把新图标放入隐藏图标区，可把它拖到任务栏常显区。

### macOS

在 macOS 14+ 机器上安装 Xcode/Swift 6 与已登录的官方 `codex` CLI，然后按部署指南构建 `.app`。Windows 不能交叉编译或签名可运行的 macOS App。

## 验证

Windows：

```powershell
cd .\CodexMenuMeter_WIN
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Live
```

`-Live` 只接受真实 App Server 数据；未执行 `codex login` 时会失败，并且应用显示 `--`，不会使用演示值。

macOS：

```sh
cd CodexMenuMeter_MAC
swift test
./Scripts/build-app.sh
```

## 隐私与已知限制

- 当前 Codex 桌面版没有向独立 App Server 共享桌面任务状态；Windows 和 macOS 的任务状态点均仅为默认关闭的灰色预留功能。
- Windows 使用每任务栏 DPI 的原生像素渲染；macOS 使用原生菜单栏状态项。
- 两个平台成品均未签名，首次运行可能出现系统安全提示。
