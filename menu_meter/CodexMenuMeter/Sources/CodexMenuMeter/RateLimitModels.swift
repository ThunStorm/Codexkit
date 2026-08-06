import Foundation

struct AccountReadParams: Encodable, Sendable { let refreshToken = false }
struct AccountReadResponse: Decodable, Sendable {
    let account: AccountMetadata?
    let requiresOpenaiAuth: Bool
}
struct AccountMetadata: Decodable, Sendable { let type: String }

struct RateLimitSnapshot: Codable, Sendable, Equatable {
    var primary: RateLimitWindow?
    var secondary: RateLimitWindow?

    func allWindows(receivedAt: Date = .now) -> [UsageWindow] {
        let direct = [primary, secondary].compactMap { $0 }
        return direct.compactMap { raw in
            guard let remaining = UsageSelector.remainingPercent(usedPercent: raw.usedPercent), let duration = raw.windowDurationMins, duration > 0 else { return nil }
            return UsageWindow(id: raw.id ?? "window-\(duration)", kind: UsageSelector.kind(for: duration), usedPercent: raw.usedPercent, remainingPercent: remaining, durationMinutes: duration, resetsAt: raw.resetsAt, receivedAt: receivedAt)
        }
    }

    func merged(with update: RateLimitSnapshot) -> RateLimitSnapshot {
        RateLimitSnapshot(primary: update.primary ?? primary, secondary: update.secondary ?? secondary)
    }
}

struct RateLimitWindow: Codable, Sendable, Equatable {
    var id: String?
    let usedPercent: Double
    let windowDurationMins: Int?
    let resetsAt: Date?

    init(id: String? = nil, usedPercent: Double, windowDurationMins: Int?, resetsAt: Date? = nil) { self.id = id; self.usedPercent = usedPercent; self.windowDurationMins = windowDurationMins; self.resetsAt = resetsAt }

    enum CodingKeys: String, CodingKey { case id = "limitId", usedPercent, windowDurationMins, resetsAt }
    init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        id = try values.decodeIfPresent(String.self, forKey: .id)
        usedPercent = try values.decode(Double.self, forKey: .usedPercent)
        windowDurationMins = try values.decodeIfPresent(Int.self, forKey: .windowDurationMins)
        if let seconds = try? values.decode(Double.self, forKey: .resetsAt) { resetsAt = Date(timeIntervalSince1970: seconds) }
        else if let text = try? values.decode(String.self, forKey: .resetsAt) { resetsAt = ISO8601DateFormatter().date(from: text) ?? TimeInterval(text).map(Date.init(timeIntervalSince1970:)) }
        else { resetsAt = nil }
    }
}

struct RateLimitsReadResponse: Codable, Sendable {
    let rateLimits: RateLimitSnapshot?
    let rateLimitsByLimitID: [String: RateLimitSnapshot]?
    enum CodingKeys: String, CodingKey { case rateLimits, rateLimitsByLimitID = "rateLimitsByLimitId" }
    func allWindows(receivedAt: Date = .now) -> [UsageWindow] {
        let historical = rateLimits?.allWindows(receivedAt: receivedAt) ?? []
        let buckets = rateLimitsByLimitID?.values.flatMap { $0.allWindows(receivedAt: receivedAt) } ?? []
        return historical.isEmpty ? buckets : historical
    }
}
