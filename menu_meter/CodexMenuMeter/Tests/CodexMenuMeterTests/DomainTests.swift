import XCTest
@testable import CodexMenuMeter

final class DomainTests: XCTestCase {
    private let now = Date(timeIntervalSince1970: 1_700_000_000)
    private func window(_ duration: Int, used: Double = 16) -> UsageWindow {
        UsageWindow(id: "\(duration)", kind: UsageSelector.kind(for: duration), usedPercent: used, remainingPercent: UsageSelector.remainingPercent(usedPercent: used)!, durationMinutes: duration, resetsAt: nil, receivedAt: now)
    }

    func testFiveHourWindowWinsOverWeekly() { XCTAssertEqual(UsageSelector.selectDisplayedWindow([window(10_080), window(300)])?.durationMinutes, 300) }
    func testWeeklyFallback() { XCTAssertEqual(UsageSelector.selectDisplayedWindow([window(10_080)])?.kind, .weekly) }
    func testUnknownWindowsAreNotSelected() { XCTAssertNil(UsageSelector.selectDisplayedWindow([window(60)])) }
    func testRemainingPercentageAndInvalidValues() { XCTAssertEqual(UsageSelector.remainingPercent(usedPercent: 16), 84); XCTAssertEqual(UsageSelector.remainingPercent(usedPercent: 0), 100); XCTAssertEqual(UsageSelector.remainingPercent(usedPercent: 100), 0); XCTAssertNil(UsageSelector.remainingPercent(usedPercent: 101)) }
    func testSparseUpdateKeepsExistingSecondaryWindow() {
        let initial = RateLimitSnapshot(primary: .init(usedPercent: 16, windowDurationMins: 300), secondary: .init(usedPercent: 30, windowDurationMins: 10_080))
        let merged = initial.merged(with: .init(primary: .init(usedPercent: 20, windowDurationMins: 300), secondary: nil))
        XCTAssertEqual(merged.primary?.usedPercent, 20); XCTAssertEqual(merged.secondary?.windowDurationMins, 10_080)
    }
    func testRateLimitDecodesOfficialNumericResetTime() throws {
        let data = Data(#"{"usedPercent":16,"windowDurationMins":300,"resetsAt":1700000000}"#.utf8)
        let decoded = try JSONDecoder().decode(RateLimitWindow.self, from: data)
        XCTAssertEqual(decoded.windowDurationMins, 300)
        XCTAssertEqual(decoded.resetsAt?.timeIntervalSince1970, 1_700_000_000)
    }
    func testRealAppServerWeeklyOnlyFixtureSelectsWeeklyRemainingPercentage() throws {
        let data = Data(#"{"rateLimits":{"primary":{"usedPercent":29,"windowDurationMins":10080,"resetsAt":1786167266},"secondary":null}}"#.utf8)
        let response = try JSONDecoder().decode(RateLimitsReadResponse.self, from: data)
        let selected = UsageSelector.selectDisplayedWindow(response.allWindows())
        XCTAssertEqual(selected?.kind, .weekly)
        XCTAssertEqual(selected?.remainingPercent, 71)
    }
    func testAccountReadFixtureAcceptsSignedInAccountWhenRequiresOpenAIAuthIsTrue() throws {
        let data = Data(#"{"account":{"type":"chatgpt"},"requiresOpenaiAuth":true}"#.utf8)
        let account = try JSONDecoder().decode(AccountReadResponse.self, from: data)
        XCTAssertNotNil(account.account)
        XCTAssertTrue(account.requiresOpenaiAuth)
    }
    func testAttentionTakesPriority() {
        let task = RunningTaskSummary(id: "1", threadID: nil, displayTitle: "测试", phase: .waitingForApproval, startedAt: now, updatedAt: now, attentionReason: .commandApproval)
        XCTAssertEqual(TaskAggregator.aggregate(tasks: [task], latestCompletion: nil, sourceAvailable: true), .needsAttention(reason: .commandApproval))
    }
    func testLostSourceIsUnknown() { XCTAssertEqual(TaskAggregator.aggregate(tasks: [], latestCompletion: now, sourceAvailable: false), .unknown) }
}
