import Combine
import Foundation

@MainActor
final class AppState: ObservableObject {
    @Published private(set) var usage: UsageDisplayState = .loading
    @Published private(set) var quotaWindows: [UsageWindow] = []
    @Published private(set) var activity: AgentActivityState = .unknown
    @Published private(set) var runningTasks: [RunningTaskSummary] = []
    @Published private(set) var connection: ConnectionState = .disconnected
    @Published private(set) var lastUpdatedAt: Date?
    @Published private(set) var quotaStatusMessage: String?
    @Published private(set) var statusSourceMessage = "桌面任务状态暂不可用"

    private let locator = CodexProcessLocator()
    private var rpcClient: JSONRPCClient?
    private var refreshTimer: Timer?
    private var isRefreshing = false
    private var isStopped = false
    private var retryDelay: TimeInterval = 10
    private var accountFingerprint: String?
    private static let cacheKey = "lastSuccessfulQuota"

    init() {
        if let data = UserDefaults.standard.data(forKey: Self.cacheKey),
           let cache = try? JSONDecoder().decode(QuotaCache.self, from: data),
           let selected = UsageSelector.selectDisplayedWindow(cache.windows) {
            quotaWindows = cache.windows; usage = .available(selected); lastUpdatedAt = cache.updatedAt
            accountFingerprint = cache.accountFingerprint
            quotaStatusMessage = "显示上次成功数据，正在核对账户并刷新"
        }
    }

    func stop() {
        isStopped = true
        refreshTimer?.invalidate()
        refreshTimer = nil
        Task { [rpcClient] in await rpcClient?.close() }
    }

    var displayedWindow: UsageWindow? { if case .available(let window) = usage { return window }; return nil }
    var menuWindows: [UsageWindow] { UsageSelector.selectMenuWindows(quotaWindows) }
    var showsTaskStatusDot: Bool { UserDefaults.standard.bool(forKey: "showTaskStatusDot") }

    var display: MenuBarDisplayState {
        let percentage = displayedWindow.map { window in
            let label = UserDefaults.standard.bool(forKey: "showQuotaPeriod") ? (window.kind == .fiveHour ? "5h " : "7d ") : ""
            return "\(label)\(window.remainingPercent)%"
        } ?? "--%"
        let quota = displayedWindow.map { $0.kind == .fiveHour ? "5 小时" : "一周" } ?? "额度暂不可用"
        let stateText: String
        switch activity { case .unknown: stateText = "任务状态未知"; case .running: stateText = "任务正在运行"; case .completed: stateText = "任务已完成"; case .needsAttention: stateText = "任务需要处理" }
        let reset = displayedWindow?.resetsAt.map { "，\(Self.resetFormatter.string(from: $0)) 重置" } ?? ""
        let taskStatus = showsTaskStatusDot ? "，\(stateText)" : ""
        let cached = quotaStatusMessage.map { "，\($0)" } ?? ""
        return MenuBarDisplayState(percentageText: percentage, quotaKindText: quota, accessibilityLabel: "Codex，\(quota)额度剩余 \(percentage)\(reset)\(cached)\(taskStatus)", tooltip: "\(quota)额度剩余 \(percentage)\(reset)\(cached)")
    }

    func start() { refresh() }

    func refresh() {
        guard !isRefreshing, !isStopped else { return }
        refreshTimer?.invalidate()
        isRefreshing = true
        connection = .connecting
        Task { [weak self] in
            guard let self else { return }
            var succeeded = false
            defer {
                self.isRefreshing = false
                if !self.isStopped { self.scheduleRefresh(after: succeeded ? 60 : self.retryDelay) }
                self.retryDelay = succeeded ? 10 : min(self.retryDelay * 2, 60)
            }
            do {
                guard let executable = self.locator.locate() else { throw JSONRPCError.executableNotFound }
                let client: JSONRPCClient
                if let existing = self.rpcClient { client = existing } else { client = JSONRPCClient(executableURL: executable); self.rpcClient = client }
                try await client.initialize()
                let account: AccountReadResponse = try await client.request(method: "account/read", params: AccountReadParams())
                // `requiresOpenaiAuth` describes whether OpenAI auth is needed for this transport;
                // it remains true for a valid ChatGPT account and is not a logged-out signal.
                guard let metadata = account.account else {
                    self.clearQuota(reason: "Codex 尚未登录")
                    throw JSONRPCError.protocolError("Codex 尚未登录")
                }
                guard metadata.type == "chatgpt" else {
                    self.clearQuota(reason: "当前账户不提供 ChatGPT 额度")
                    throw JSONRPCError.protocolError("当前账户不提供 ChatGPT 额度")
                }
                if let previous = self.accountFingerprint, previous != metadata.fingerprint { self.clearQuota(reason: "账户已变化，正在读取新额度") }
                self.accountFingerprint = metadata.fingerprint
                let response: RateLimitsReadResponse = try await client.requestWithoutParams(method: "account/rateLimits/read")
                guard !self.isStopped else { return }
                let receivedAt = Date()
                let windows = response.allWindows(receivedAt: receivedAt)
                guard let selected = UsageSelector.selectDisplayedWindow(windows) else {
                    throw JSONRPCError.protocolError("没有可显示的 5 小时或一周额度窗口")
                }
                self.quotaWindows = windows
                self.usage = .available(selected)
                self.connection = .connected
                self.lastUpdatedAt = receivedAt
                self.quotaStatusMessage = nil
                let cache = QuotaCache(accountFingerprint: metadata.fingerprint, windows: windows, updatedAt: receivedAt)
                if let data = try? JSONEncoder().encode(cache) { UserDefaults.standard.set(data, forKey: Self.cacheKey) }
                succeeded = true
            } catch {
                if let client = self.rpcClient { await client.close(); self.rpcClient = nil }
                self.connection = .failed(error.localizedDescription)
                if self.displayedWindow == nil { self.usage = .unavailable(reason: error.localizedDescription) }
                self.quotaStatusMessage = self.displayedWindow == nil ? error.localizedDescription : "刷新暂未成功，显示上次成功数据"
            }
        }
    }

    func markStaleIfNeeded(now: Date = .now) {
        if let lastUpdatedAt, now.timeIntervalSince(lastUpdatedAt) > 300 { quotaStatusMessage = "显示上次成功数据，等待刷新"; activity = .unknown; runningTasks = [] }
    }

    private func clearQuota(reason: String) {
        usage = .unavailable(reason: reason); quotaWindows = []; lastUpdatedAt = nil; accountFingerprint = nil
        UserDefaults.standard.removeObject(forKey: Self.cacheKey)
    }

    func setMockTasks(_ tasks: [RunningTaskSummary], latestCompletion: Date? = nil) {
        runningTasks = tasks
        activity = TaskAggregator.aggregate(tasks: tasks, latestCompletion: latestCompletion, sourceAvailable: false)
    }

    private func scheduleRefresh(after interval: TimeInterval) {
        refreshTimer = Timer.scheduledTimer(withTimeInterval: interval, repeats: false) { [weak self] _ in
            Task { @MainActor in self?.markStaleIfNeeded(); self?.refresh() }
        }
    }

    private static let resetFormatter: DateFormatter = { let formatter = DateFormatter(); formatter.dateStyle = .none; formatter.timeStyle = .short; return formatter }()
}
