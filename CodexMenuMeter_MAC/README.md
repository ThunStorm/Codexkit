# CodexMenuMeter

一个原生 macOS 菜单栏伴随应用，显示 Codex 5 小时额度（缺失时回退到一周额度）。任务状态点是默认关闭的预留设置；当前版本不读取、推断或展示桌面任务状态。

## 构建与运行

需要 macOS 14+、Xcode/Swift 6，以及已安装并登录的官方 `codex` CLI。项目会依次发现用户设置的 CLI 路径、`/Applications/ChatGPT.app/Contents/Resources/codex`、Homebrew 和 `/usr/local/bin` 路径。

```sh
cd CodexMenuMeter_MAC
swift test
swift run CodexMenuMeter
```

需要 `.app` 包时运行 `Scripts/build-app.sh`，产物位于 `.build/CodexMenuMeter.app`；它包含 `LSUIElement`，不会显示 Dock 图标。

运行后按住 Command 将状态项拖到 Codex 图标旁。点击菜单中的“设置…”可指定 CLI 路径或注册登录时启动；该开关显示在“系统设置 → 通用 → 登录项”。卸载时退出应用、关闭登录启动并删除 `/Applications/CodexMenuMeter.app`；本应用不修改 Codex 配置或登录凭据。

## 任务状态预留

设置中的“显示任务状态点”用于未来兼容。打开后显示灰色状态点，但菜单仍只显示额度信息；它不代表任务正在运行或已完成。详见 [任务状态路线图](docs/TASK_STATUS.md)。
