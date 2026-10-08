import Foundation

enum UsageWindowKind: String, Sendable, Equatable, Codable { case fiveHour, weekly, monthly, unknown }

struct UsageWindow: Sendable, Equatable, Identifiable, Codable {
    let id: String
    let kind: UsageWindowKind
    let usedPercent: Double
    let remainingPercent: Int
    let durationMinutes: Int
    let resetsAt: Date?
    let receivedAt: Date
}

enum UsageSelector {
    static func kind(for durationMinutes: Int) -> UsageWindowKind {
        switch durationMinutes {
        case 240...360: return .fiveHour
        case 9_000...11_000: return .weekly
        case 38_000...50_000: return .monthly
        default: return .unknown
        }
    }

    static func remainingPercent(usedPercent: Double) -> Int? {
        guard usedPercent.isFinite, (0...100).contains(usedPercent) else { return nil }
        return Int((100 - usedPercent).rounded())
    }

    /// 状态栏主数字：优先 5 小时窗口，缺失时回退周额度窗口。
    /// 特例：周额度已耗尽（剩余 0%）时，无论 5 小时额度剩余多少，都改显示周额度窗口，
    /// 让状态栏直接显示 0%，避免给出“还能继续用”的误导。
    static func selectDisplayedWindow(_ windows: [UsageWindow]) -> UsageWindow? {
        if let weekly = windows.first(where: { $0.kind == .weekly }), weekly.remainingPercent == 0 {
            return weekly
        }
        return windows.first { $0.kind == .fiveHour } ?? windows.first { $0.kind == .weekly }
    }

    static func selectMenuWindows(_ windows: [UsageWindow]) -> [UsageWindow] {
        guard let fiveHour = windows.first(where: { $0.kind == .fiveHour }) else {
            return windows.first(where: { $0.kind == .weekly }).map { [$0] } ?? []
        }
        return [fiveHour] + (windows.first(where: { $0.kind == .weekly }).map { [$0] } ?? [])
    }
}

enum AttentionReason: Sendable, Equatable {
    case commandApproval, fileChangeApproval, permissionApproval, userInput, authentication
    case failed(message: String?), systemError(message: String?)

    var description: String {
        switch self {
        case .commandApproval: return "需要批准命令执行"
        case .fileChangeApproval: return "需要批准文件修改"
        case .permissionApproval: return "需要授予权限"
        case .userInput: return "等待用户输入"
        case .authentication: return "需要重新登录"
        case .failed(let message), .systemError(let message): return message ?? "任务发生错误"
        }
    }
}

enum AgentActivityState: Sendable, Equatable {
    case unknown
    case running(activeCount: Int)
    case completed(completedAt: Date)
    case needsAttention(reason: AttentionReason)
}

enum RunningTaskPhase: Sendable, Equatable { case queued, analyzing, executing, waitingForApproval, waitingForUserInput, reconnecting
    var description: String {
        switch self {
        case .queued: return "排队中"
        case .analyzing: return "正在分析"
        case .executing: return "正在执行"
        case .waitingForApproval: return "等待授权"
        case .waitingForUserInput: return "等待输入"
        case .reconnecting: return "正在重新连接"
        }
    }
}

struct RunningTaskSummary: Identifiable, Sendable, Equatable {
    let id: String
    let threadID: String?
    let displayTitle: String
    let phase: RunningTaskPhase
    let startedAt: Date?
    let updatedAt: Date
    let attentionReason: AttentionReason?
}

enum TaskAggregator {
    static func aggregate(tasks: [RunningTaskSummary], latestCompletion: Date?, sourceAvailable: Bool) -> AgentActivityState {
        guard sourceAvailable else { return .unknown }
        if let task = tasks.first(where: { $0.attentionReason != nil }), let reason = task.attentionReason { return .needsAttention(reason: reason) }
        let active = tasks.filter { $0.phase != .reconnecting }.count
        if active > 0 { return .running(activeCount: active) }
        if let latestCompletion { return .completed(completedAt: latestCompletion) }
        return .unknown
    }
}

enum UsageDisplayState: Sendable, Equatable { case loading, available(UsageWindow), unavailable(reason: String), stale }

enum ConnectionState: Sendable, Equatable { case disconnected, connecting, connected, failed(String) }

struct MenuBarDisplayState: Equatable {
    let percentageText: String
    let quotaKindText: String
    let accessibilityLabel: String
    let tooltip: String
}
