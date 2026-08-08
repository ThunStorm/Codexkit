# CodexMenuMeter 部署与系统配置

## Windows 11

### 安装

1. 将 `CodexMenuMeter-Windows.exe` 复制到长期保留目录，例如 `%LOCALAPPDATA%\CodexMenuMeter\CodexMenuMeter.exe`。
2. 在终端执行 `codex login` 并完成登录。
3. 运行该 EXE。仅当 Microsoft Store 安装的官方 Codex 桌面应用运行时，托盘图标才会出现。

不要把 EXE 放在临时下载目录后开启启动项：启动项会保存该 EXE 的路径。

### 开机启动与设置

- 首次运行默认在 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 写入 `CodexMenuMeter`，无需管理员权限。
- 右键托盘图标，选择“设置…”，可关闭“随 Windows 登录启动”。下次保存设置会刷新当前 EXE 路径。
- “显示任务状态点（预留功能）”默认关闭；开启后只显示灰点，不代表任务正在运行、完成或失败。

### 卸载

1. 右键托盘图标，选择退出。
2. 在设置中关闭“随 Windows 登录启动”。
3. 删除安装目录。

### 故障排查

- 显示 `--`：执行 `codex login` 后在菜单中刷新。桌面版登录态不会共享给独立 CLI。
- 没有图标：确认官方 Codex 桌面应用正在运行；CLI 进程本身不会触发图标。
- 图标不可见：在任务栏隐藏图标区中找到它并拖到常显区域。
- SmartScreen：当前 Windows 成品未签名，只在信任来源后运行。

## macOS 14+

### 构建与安装

在 macOS 机器执行：

```sh
cd CodexMenuMeter_MAC
swift test
./Scripts/build-app.sh
cp -R .build/CodexMenuMeter.app /Applications/
open /Applications/CodexMenuMeter.app
```

要求：macOS 14+、Xcode/Swift 6 和已登录的官方 `codex` CLI。构建脚本产生 `.build/CodexMenuMeter.app`，应用采用 `LSUIElement`，因此不显示 Dock 图标。

### 登录启动与卸载

- 在菜单栏应用的“设置…”中启用登录启动；系统会显示在“系统设置 → 通用 → 登录项”。
- 卸载时先从菜单退出应用、在设置中关闭登录启动，再删除 `/Applications/CodexMenuMeter.app`。
- 未签名的应用第一次启动可能需要在“系统设置 → 隐私与安全性”中确认打开。

## 发布成品

Windows 机器只能构建 Windows EXE；macOS `.app` 必须在 macOS 上构建和签名。将两项成品分别命名为：

- `CodexMenuMeter-Windows.exe`
- `CodexMenuMeter-macOS.app`

然后放到同一个发布目录或 GitHub Release 中。
