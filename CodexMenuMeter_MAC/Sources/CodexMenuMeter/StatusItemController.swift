import AppKit
import Combine

@MainActor
final class StatusItemController: NSObject, NSMenuDelegate {
    private let state: AppState
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    private let contentView = StatusItemView()
    private var cancellable: AnyCancellable?
    private var settingsCancellable: AnyCancellable?
    private let menu = NSMenu()

    init(state: AppState) {
        self.state = state
        super.init()
        guard let button = statusItem.button else { return }
        button.title = ""
        button.target = self
        button.action = #selector(showMenu)
        button.addSubview(contentView)
        NSLayoutConstraint.activate([
            contentView.leadingAnchor.constraint(equalTo: button.leadingAnchor), contentView.trailingAnchor.constraint(equalTo: button.trailingAnchor),
            contentView.topAnchor.constraint(equalTo: button.topAnchor), contentView.bottomAnchor.constraint(equalTo: button.bottomAnchor)
        ])
        button.setAccessibilityRole(.button)
        menu.delegate = self
        statusItem.menu = menu
        cancellable = state.objectWillChange.sink { [weak self] in DispatchQueue.main.async { self?.updateView() } }
        settingsCancellable = NotificationCenter.default.publisher(for: UserDefaults.didChangeNotification).sink { [weak self] _ in self?.updateView() }
        updateView()
    }

    @objc private func showMenu() { rebuildMenu(); statusItem.button?.performClick(nil) }
    func menuNeedsUpdate(_ menu: NSMenu) { rebuildMenu() }

    private func updateView() {
        let display = state.display
        contentView.update(percentage: display.percentageText, color: statusColor(for: state.activity), showsStatusDot: state.showsTaskStatusDot, accessibilityLabel: display.accessibilityLabel)
        statusItem.button?.toolTip = display.tooltip
    }

    private func rebuildMenu() {
        menu.removeAllItems()
        add("Codex 额度", enabled: false)
        menu.addItem(.separator())
        if state.menuWindows.isEmpty { add("额度：暂不可用", enabled: false) }
        for window in state.menuWindows {
            let label = window.kind == .fiveHour ? "5 小时" : "周额度"
            add("\(label)：剩余 \(window.remainingPercent)%", enabled: false)
            add("重置：\(formatReset(window.resetsAt))", enabled: false)
        }
        add("更新：\(formatUpdated(state.lastUpdatedAt))", enabled: false)
        menu.addItem(.separator())
        addAction("打开 Codex", #selector(openCodex))
        addAction("刷新", #selector(refresh))
        menu.addItem(.separator())
        addAction("设置…", #selector(openSettings))
        addAction("退出 CodexMenuMeter", #selector(quit))
    }

    private func add(_ title: String, enabled: Bool) { let item = NSMenuItem(title: title, action: nil, keyEquivalent: ""); item.isEnabled = enabled; menu.addItem(item) }
    private func addAction(_ title: String, _ action: Selector) { let item = NSMenuItem(title: title, action: action, keyEquivalent: ""); item.target = self; menu.addItem(item) }
    @objc private func refresh() { state.refresh() }
    @objc private func openCodex() {
        let workspace = NSWorkspace.shared
        let codex = workspace.urlForApplication(withBundleIdentifier: "com.openai.codex") ?? URL(fileURLWithPath: "/Applications/ChatGPT.app")
        workspace.openApplication(at: codex, configuration: .init()) { _, _ in }
    }
    @objc private func openSettings() { SettingsController.shared.show() }
    @objc private func quit() { NSApp.terminate(nil) }
    private func statusColor(for activity: AgentActivityState) -> NSColor { switch activity { case .unknown: return .systemGray; case .running: return .systemYellow; case .completed: return .systemGreen; case .needsAttention: return .systemRed } }
    private func formatReset(_ date: Date?) -> String { guard let date else { return "未知" }; return DateFormatter.localizedString(from: date, dateStyle: .medium, timeStyle: .short) }
    private func formatUpdated(_ date: Date?) -> String { guard let date else { return "尚未更新" }; return RelativeDateTimeFormatter().localizedString(for: date, relativeTo: .now) }
}
