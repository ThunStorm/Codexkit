# 测试

在 `CodexMenuMeter_WIN` 目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Clean
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Live
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

`-Test` 覆盖百分比计算、额度文字、窗口选择、JSON 响应解析、状态点默认值、七档原生尺寸选择、无 `%` 的全尺寸数字布局与实际像素输出、颜色优先级、任务行上限、官方桌面进程识别和 CLI 路径选择。

`-Live` 不注入 fixture：它发现本机官方桌面应用和 CLI，启动真实 App Server，读取真实额度并输出窗口分钟数。未登录、接口错误或无额度都会失败，不能作为成功验收。任务数量只记录独立 App Server 的真实返回；它不等同于桌面任务。

手工检查：Codex 未运行时无图标；启动后两秒内出现；退出后隐藏；菜单刷新和启动项开关可用。

## 2026-08-08 本机结果

- `-Test`：`PASS 57 tests`。
- `-Live`：失败，真实响应为 `Codex 尚未登录`。该结果证明没有用 fixture 冒充额度；完成 `codex login` 后需再次运行，只有输出 `LIVE quota: <数字>% remaining` 才算真实额度验收通过。
- 活动桌面任务存在时，独立 App Server 返回 0 个桌面任务，因此任务状态功能未启用。
- Windows UI：检查上移后的纵向增强 Segoe UI Bold 大数字无 `%` 且无裁切；开启预留点后只增加灰点；右键菜单没有任务状态行；菜单和设置字体在当前 DPI 下边缘清晰。
- Per-Monitor V2 视觉探针：生产渲染器的 16px `39` 使用 55 个可见单色像素，较 Regular 的 33 个更粗；无 ClearType 彩边或描边。当前自动化会话无法连接用户 Explorer 的托盘窗口，因此最终任务栏位置仍以用户桌面目视为准。
