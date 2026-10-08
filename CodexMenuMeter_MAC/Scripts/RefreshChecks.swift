import Darwin
import Foundation

// Standalone regression checks for machines without XCTest; no real account or network calls.
@main
struct RefreshChecks {
    @MainActor
    static func main() async throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let executable = root.appendingPathComponent("server")
        let mode = root.appendingPathComponent("mode")
        let script = #"""
        #!/usr/bin/python3
        import json, os, sys
        for line in sys.stdin:
            request = json.loads(line)
            if 'id' not in request: continue
            with open(os.path.join(os.path.dirname(__file__), 'mode')) as f: mode = f.read()
            if mode == 'exit': sys.exit(0)
            if mode == 'silent': continue
            if mode == 'oversize':
                sys.stdout.write('x' * 1100000); sys.stdout.flush(); continue
            if mode == 'stderr':
                sys.stderr.write('x' * 262144); sys.stderr.flush()
            method = request['method']
            if method == 'account/read':
                account = None if mode == 'logout' else {'type': 'apiKey' if mode == 'apiKey' else 'chatgpt', 'email': ('other' if mode == 'switch' else 'test') + '@example.invalid'}
                result = {'account': account, 'requiresOpenaiAuth': True}
            elif method == 'account/rateLimits/read':
                if mode in ('fail', 'switch'):
                    print(json.dumps({'id': request['id'], 'error': {'message': 'temporary failure'}}), flush=True); continue
                result = {'rateLimits': None if mode == 'empty' else {'primary': {'usedPercent': 100 if mode == 'zero' else 16, 'windowDurationMins': 300, 'resetsAt': 1}}}
            else: result = {}
            print(json.dumps({'id': request['id'], 'result': result}), flush=True)
        """#
        try Data(script.utf8).write(to: executable)
        try FileManager.default.setAttributes([.posixPermissions: 0o700], ofItemAtPath: executable.path)
        func setMode(_ value: String) throws { try Data(value.utf8).write(to: mode) }
        func descriptors() -> Int { (0..<1024).filter { fcntl(Int32($0), F_GETFD) != -1 }.count }

        try setMode("exit")
        let before = descriptors()
        let client = JSONRPCClient(executableURL: executable)
        for _ in 0..<100 {
            do {
                let _: AccountReadResponse = try await client.requestWithoutParams(method: "account/read", timeout: 2)
                fatalError("Exited server must fail")
            } catch JSONRPCError.launchFailed { }
            await client.close()
        }
        try await Task.sleep(for: .milliseconds(100))
        precondition(descriptors() <= before + 4, "Pipe handles accumulated after EOF")
        print("PASS: 100 EOF/reconnect cycles, no accumulating pipe handles")

        try setMode("silent")
        let started = Date()
        do {
            let _: AccountReadResponse = try await client.requestWithoutParams(method: "account/read", timeout: 0.2)
            fatalError("Silent server must time out")
        } catch JSONRPCError.timeout { }
        precondition(Date().timeIntervalSince(started) < 2)
        await client.close()
        try setMode("oversize")
        do {
            let _: AccountReadResponse = try await client.requestWithoutParams(method: "account/read", timeout: 2)
            fatalError("Unframed output must be bounded")
        } catch JSONRPCError.launchFailed { }
        await client.close()
        print("PASS: timeout and oversized partial response cleanup")

        let defaults = UserDefaults.standard
        let keys = ["codexExecutablePath", "lastSuccessfulQuota"]
        let saved = keys.map { defaults.object(forKey: $0) }
        defer { for (key, value) in zip(keys, saved) { defaults.set(value, forKey: key) } }
        defaults.set(executable.path, forKey: keys[0]); defaults.removeObject(forKey: keys[1])
        try setMode("stderr")
        let state = AppState()
        try await refresh(state)
        precondition(state.displayedWindow?.remainingPercent == 84, "stderr blocked quota read")
        let updated = state.lastUpdatedAt
        try setMode("fail"); try await refresh(state)
        precondition(state.displayedWindow?.remainingPercent == 84 && state.lastUpdatedAt == updated && state.quotaStatusMessage != nil)
        state.markStaleIfNeeded(now: Date().addingTimeInterval(86400))
        precondition(state.displayedWindow?.remainingPercent == 84)
        try setMode("empty"); try await refresh(state)
        precondition(state.displayedWindow?.remainingPercent == 84 && state.lastUpdatedAt == updated)
        state.stop()
        let restored = AppState()
        precondition(restored.displayedWindow?.remainingPercent == 84 && restored.lastUpdatedAt == updated)
        try setMode("switch"); try await refresh(restored)
        precondition(restored.displayedWindow == nil && defaults.data(forKey: keys[1]) == nil)
        try setMode("zero"); try await refresh(restored)
        precondition(restored.displayedWindow?.remainingPercent == 0 && restored.quotaStatusMessage == nil)
        try setMode("logout"); try await refresh(restored)
        precondition(restored.displayedWindow == nil && defaults.data(forKey: keys[1]) == nil)
        try setMode("stderr"); try await refresh(restored)
        try setMode("apiKey"); try await refresh(restored)
        precondition(restored.displayedWindow == nil && defaults.data(forKey: keys[1]) == nil)
        restored.stop()
        print("PASS: stderr flood, failure/empty/stale retention, restart cache, account switch/logout/API key invalidation, real zero")
    }

    @MainActor
    private static func refresh(_ state: AppState) async throws {
        state.refresh()
        for _ in 0..<300 {
            if state.connection != .connecting { return }
            try await Task.sleep(for: .milliseconds(10))
        }
        fatalError("Refresh did not finish")
    }
}
