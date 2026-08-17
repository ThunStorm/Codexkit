import AppKit

@main
struct CodexMenuMeterApp {
    static func main() {
        let app = NSApplication.shared
        app.setActivationPolicy(.accessory)
        if let iconURL = Bundle.main.url(forResource: "AppIcon-1024", withExtension: "png"), let icon = NSImage(contentsOf: iconURL) { app.applicationIconImage = icon }
        let delegate = AppDelegate()
        app.delegate = delegate
        app.run()
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var state: AppState?
    private var statusItemController: StatusItemController?
    private var wakeObserver: NSObjectProtocol?
    private var codexLaunchObserver: NSObjectProtocol?
    private var codexTerminateObserver: NSObjectProtocol?

    func applicationDidFinishLaunching(_ notification: Notification) {
        codexLaunchObserver = NSWorkspace.shared.notificationCenter.addObserver(forName: NSWorkspace.didLaunchApplicationNotification, object: nil, queue: .main) { [weak self] notification in
            guard let application = notification.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication,
                  application.bundleIdentifier == "com.openai.codex" else { return }
            Task { @MainActor in self?.startMeter() }
        }
        codexTerminateObserver = NSWorkspace.shared.notificationCenter.addObserver(forName: NSWorkspace.didTerminateApplicationNotification, object: nil, queue: .main) { [weak self] notification in
            guard let application = notification.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication,
                  application.bundleIdentifier == "com.openai.codex" else { return }
            Task { @MainActor in self?.stopMeter() }
        }
        if NSWorkspace.shared.runningApplications.contains(where: { $0.bundleIdentifier == "com.openai.codex" }) { startMeter() }
    }

    private func startMeter() {
        guard state == nil else { return }
        let state = AppState()
        self.state = state
        statusItemController = StatusItemController(state: state)
        wakeObserver = NSWorkspace.shared.notificationCenter.addObserver(forName: NSWorkspace.didWakeNotification, object: nil, queue: .main) { _ in
            DispatchQueue.main.asyncAfter(deadline: .now() + 1) { state.refresh() }
        }
        state.start()
    }

    private func stopMeter() {
        guard state != nil else { return }
        if let wakeObserver { NSWorkspace.shared.notificationCenter.removeObserver(wakeObserver) }
        wakeObserver = nil
        state?.stop()
        state = nil
        statusItemController = nil
    }

    func applicationWillTerminate(_ notification: Notification) {
        if let wakeObserver { NSWorkspace.shared.notificationCenter.removeObserver(wakeObserver) }
        if let codexLaunchObserver { NSWorkspace.shared.notificationCenter.removeObserver(codexLaunchObserver) }
        if let codexTerminateObserver { NSWorkspace.shared.notificationCenter.removeObserver(codexTerminateObserver) }
        state?.stop()
    }
}
