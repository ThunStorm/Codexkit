# 测试与验证指南

## 自动测试

```sh
cd CodexMenuMeter_MAC
swift test
```

测试覆盖状态栏与菜单窗口选择、缺失窗口隐藏、剩余百分比、Unix 重置时间、稀疏更新合并、已登录账户识别和任务领域模型聚合。

某些只安装了 Command Line Tools 的机器可能出现 Swift 编译器与 SDK 不匹配，或缺少 XCTest。可显式使用兼容 SDK：

```sh
SDKROOT=/Library/Developer/CommandLineTools/SDKs/MacOSX15.4.sdk \
CLANG_MODULE_CACHE_PATH=/private/tmp/codexmenumeter-clang-cache \
SWIFTPM_MODULECACHE_OVERRIDE=/private/tmp/codexmenumeter-swiftpm-cache \
swift build -c release --jobs 1
```

## 真实额度探测

在不输出账户对象或令牌的前提下，验证以下属性：

- `account/read` 返回 `account != nil`；
- `account/rateLimits/read` 返回至少一个有效窗口；
- 选择器按时长而非 primary/secondary 位置选中窗口；
- 菜单栏值等于 `round(100 - usedPercent)`。
- 5 小时窗口存在时，菜单同时显示 5 小时和周额度；服务端缺失的窗口不生成占位行。

探测仅可使用官方 `codex app-server --stdio`，并应在完成后终止自己启动的进程。

## 手工 UI 检查

- 默认状态项只显示百分比；
- 打开“显示任务状态点”后显示灰点，关闭后没有多余间距；
- 菜单不出现运行任务、状态、标题或审批信息；
- 有 5 小时和周窗口时菜单按该顺序显示两组额度与重置时间，缺失窗口时对应组隐藏；
- 额度周期标签开关即时生效；
- 未登录、CLI 不存在、超时及过期时显示 `--%`；
- 暂时断开 app-server 后显示 `--%`，恢复后无需手动刷新即可重新显示额度；连续失败时按 10、20、40、60 秒退避；
- 浅色/深色、高对比度和 VoiceOver 下可读；
- 检查 Finder 图标、无 Dock 图标和设置窗口；
- 用 Command 拖动状态项到 Codex 图标旁。
- 更新或快速重启 Codex 后状态项继续显示；Codex 全部退出后状态项隐藏，再启动时恢复。

## 发布检查

1. `plutil -lint Resources/Info.plist`。
2. Release 构建成功。
3. `.icns` 可由 `iconutil --convert iconset` 反向解析。
4. `codesign --verify --deep --strict` 通过。
5. 运行安装包后核对实际 app-server 返回与菜单栏百分比。
