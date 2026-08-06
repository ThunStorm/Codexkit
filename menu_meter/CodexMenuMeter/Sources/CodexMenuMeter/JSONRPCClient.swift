import Foundation

enum JSONRPCError: Error, LocalizedError { case executableNotFound, launchFailed, protocolError(String), timeout
    var errorDescription: String? { switch self { case .executableNotFound: return "未找到 Codex CLI"; case .launchFailed: return "无法启动 Codex app-server"; case .protocolError(let message): return message; case .timeout: return "Codex app-server 请求超时" } }
}

actor JSONRPCClient {
    private let executableURL: URL
    private var process: Process?
    private var input: FileHandle?
    private var continuations: [Int: CheckedContinuation<Data, Error>] = [:]
    private var nextID = 1
    private var readBuffer = Data()
    private var didInitialize = false

    init(executableURL: URL) { self.executableURL = executableURL }

    func connect() throws {
        guard process == nil else { return }
        let process = Process()
        process.executableURL = executableURL
        process.arguments = ["app-server", "--stdio"]
        var environment = ProcessInfo.processInfo.environment
        ["OPENAI_API_KEY", "CODEX_API_KEY", "AWS_SECRET_ACCESS_KEY", "AWS_SESSION_TOKEN"].forEach { environment.removeValue(forKey: $0) }
        process.environment = environment
        let stdin = Pipe(), stdout = Pipe()
        process.standardInput = stdin
        process.standardOutput = stdout
        process.standardError = Pipe()
        stdout.fileHandleForReading.readabilityHandler = { [weak self] handle in
            let data = handle.availableData
            guard !data.isEmpty else { return }
            Task { await self?.receive(data) }
        }
        do { try process.run() } catch { throw JSONRPCError.launchFailed }
        self.process = process
        self.input = stdin.fileHandleForWriting
    }

    func initialize() async throws {
        guard !didInitialize else { return }
        struct Params: Encodable { let clientInfo: ClientInfo; let capabilities: Capabilities
            struct ClientInfo: Encodable { let name: String; let version: String }
            struct Capabilities: Encodable { let experimentalApi: Bool }
        }
        _ = try await request(method: "initialize", params: Params(clientInfo: .init(name: "CodexMenuMeter", version: "0.1.0"), capabilities: .init(experimentalApi: false))) as InitializeResponse
        try notifyInitialized()
        didInitialize = true
    }

    func request<Response: Decodable, Params: Encodable>(method: String, params: Params, timeout: TimeInterval = 12) async throws -> Response {
        try connect()
        let id = nextID; nextID += 1
        let payload = try JSONEncoder().encode(JSONRPCRequest(id: id, method: method, params: params))
        let data = try await withThrowingTaskGroup(of: Data.self) { group in
            group.addTask { try await self.waitForResponse(id: id, payload: payload) }
            group.addTask { try await Task.sleep(for: .seconds(timeout)); throw JSONRPCError.timeout }
            guard let first = try await group.next() else { throw JSONRPCError.timeout }
            group.cancelAll(); return first
        }
        let response = try JSONDecoder().decode(ResponseEnvelope<Response>.self, from: data)
        if let error = response.error { throw JSONRPCError.protocolError(error.message) }
        guard let result = response.result else { throw JSONRPCError.protocolError("响应缺少结果") }
        return result
    }

    func requestWithoutParams<Response: Decodable>(method: String, timeout: TimeInterval = 12) async throws -> Response {
        try connect()
        let id = nextID; nextID += 1
        let payload = try JSONEncoder().encode(JSONRPCRequestWithoutParams(id: id, method: method))
        let data = try await withThrowingTaskGroup(of: Data.self) { group in
            group.addTask { try await self.waitForResponse(id: id, payload: payload) }
            group.addTask { try await Task.sleep(for: .seconds(timeout)); throw JSONRPCError.timeout }
            guard let first = try await group.next() else { throw JSONRPCError.timeout }
            group.cancelAll(); return first
        }
        let response = try JSONDecoder().decode(ResponseEnvelope<Response>.self, from: data)
        if let error = response.error { throw JSONRPCError.protocolError(error.message) }
        guard let result = response.result else { throw JSONRPCError.protocolError("响应缺少结果") }
        return result
    }

    func close() { process?.terminate(); process = nil; input = nil; didInitialize = false; readBuffer = Data(); continuations.values.forEach { $0.resume(throwing: JSONRPCError.launchFailed) }; continuations = [:] }

    private func waitForResponse(id: Int, payload: Data) async throws -> Data {
        try await withCheckedThrowingContinuation { continuation in
            continuations[id] = continuation
            do { try send(payload) } catch { continuations.removeValue(forKey: id); continuation.resume(throwing: error) }
        }
    }

    private func notifyInitialized() throws { try send(JSONEncoder().encode(JSONRPCInitializedNotification(method: "initialized"))) }
    private func send(_ data: Data) throws { guard let input else { throw JSONRPCError.launchFailed }; input.write(data); input.write(Data([0x0A])) }

    private func receive(_ data: Data) {
        readBuffer.append(data)
        while let newline = readBuffer.firstIndex(of: 0x0A) {
            let line = readBuffer.prefix(upTo: newline)
            readBuffer.removeSubrange(...newline)
            guard let envelope = try? JSONDecoder().decode(ResponseID.self, from: Data(line)), let id = envelope.id, let continuation = continuations.removeValue(forKey: id) else { continue }
            continuation.resume(returning: Data(line))
        }
    }
}

private struct JSONRPCRequest<Params: Encodable>: Encodable { let jsonrpc = "2.0"; let id: Int; let method: String; let params: Params }
private struct JSONRPCRequestWithoutParams: Encodable { let jsonrpc = "2.0"; let id: Int; let method: String }
private struct JSONRPCInitializedNotification: Encodable { let jsonrpc = "2.0"; let method: String }
private struct InitializeResponse: Decodable {}
private struct ResponseID: Decodable { let id: Int? }
private struct ResponseEnvelope<Result: Decodable>: Decodable { let result: Result?; let error: RPCError? }
private struct RPCError: Decodable { let message: String }
