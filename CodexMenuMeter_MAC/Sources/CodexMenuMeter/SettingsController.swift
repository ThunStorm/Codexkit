import AppKit
import ServiceManagement

@MainActor
final class SettingsController: NSObject {
    static let shared = SettingsController()
    private var window: NSWindow?

    func show() {
        if let window { window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true); return }
        let path = NSTextField(string: UserDefaults.standard.string(forKey: "codexExecutablePath") ?? "")
        path.identifier = NSUserInterfaceItemIdentifier("cliPath")
        path.placeholderString = "自动发现 Codex CLI（可选）"
        let label = NSButton(checkboxWithTitle: "菜单栏显示额度周期标签", target: nil, action: nil)
        label.identifier = NSUserInterfaceItemIdentifier("periodLabel")
        label.state = UserDefaults.standard.bool(forKey: "showQuotaPeriod") ? .on : .off
        let taskStatusDot = NSButton(checkboxWithTitle: "显示任务状态点（预留功能）", target: nil, action: nil)
        taskStatusDot.identifier = NSUserInterfaceItemIdentifier("taskStatusDot")
        taskStatusDot.state = UserDefaults.standard.bool(forKey: "showTaskStatusDot") ? .on : .off
        let login = NSButton(checkboxWithTitle: "登录时启动，并跟随 Codex / ChatGPT", target: self, action: #selector(toggleLaunchAtLogin(_:)))
        login.state = SMAppService.mainApp.status == .enabled ? .on : .off
        let save = NSButton(title: "保存", target: self, action: #selector(save(_:)))
        save.identifier = NSUserInterfaceItemIdentifier("save")
        let help = NSTextField(wrappingLabelWithString: "登录时启动由 macOS 登录项管理；应用只在 Codex / ChatGPT 运行时显示并读取额度，Codex 退出后自动停止。任务状态数据源尚未实现。")
        help.textColor = .secondaryLabelColor
        let stack = NSStackView(views: [NSTextField(labelWithString: "Codex CLI 路径"), path, label, taskStatusDot, login, help, save])
        stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 12
        stack.edgeInsets = NSEdgeInsets(top: 20, left: 20, bottom: 20, right: 20)
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 420, height: 280), styleMask: [.titled, .closable], backing: .buffered, defer: false)
        window.title = "CodexMenuMeter 设置"; window.contentView = stack; window.center(); window.isReleasedWhenClosed = false
        self.window = window
        window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true)
    }

    @objc private func save(_ sender: NSButton) {
        guard let stack = sender.superview as? NSStackView,
              let path = stack.arrangedSubviews.compactMap({ $0 as? NSTextField }).first(where: { $0.identifier?.rawValue == "cliPath" }),
              let label = stack.arrangedSubviews.compactMap({ $0 as? NSButton }).first(where: { $0.identifier?.rawValue == "periodLabel" }),
              let taskStatusDot = stack.arrangedSubviews.compactMap({ $0 as? NSButton }).first(where: { $0.identifier?.rawValue == "taskStatusDot" }) else { return }
        let trimmed = path.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
        UserDefaults.standard.set(trimmed.isEmpty ? nil : trimmed, forKey: "codexExecutablePath")
        UserDefaults.standard.set(label.state == .on, forKey: "showQuotaPeriod")
        UserDefaults.standard.set(taskStatusDot.state == .on, forKey: "showTaskStatusDot")
        window?.close()
    }

    @objc private func toggleLaunchAtLogin(_ sender: NSButton) {
        do { if sender.state == .on { try SMAppService.mainApp.register() } else { try SMAppService.mainApp.unregister() } }
        catch { sender.state = SMAppService.mainApp.status == .enabled ? .on : .off; NSAlert(error: error).runModal() }
    }
}
