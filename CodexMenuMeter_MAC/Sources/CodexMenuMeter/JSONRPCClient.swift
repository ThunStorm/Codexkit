import Foundation

enum JSONRPCError: Error, LocalizedError { case executableNotFound, launchFailed, protocolError(String), timeout
    var errorDescription: String? { switch self { case .executableNotFound: return "未找到 Codex CLI"; case .launchFailed: return "无法启动 Codex app-server"; case .protocolError(let message): return message; case .timeout: return "Codex app-server 请求超时" } }
}

actor JSONRPCClient {
    private let executableURL: URL
    private var process: Process?
    private var input: FileHandle?
    private var output: FileHandle?
    private var generation = 0
    private var continuations: [Int: CheckedContinuation<Data, Error>] = [:]
    private var timeouts: [Int: Task<Void, Never>] = [:]
    private var nextID = 1
    private var readBuffer = Data()
    private var didInitialize = false

    init(executableURL: URL) { self.executableURL = executableURL }

    func connect() throws {
        if let process { if process.isRunning { return }; close() }
        generation += 1
        let generation = generation
        let process = Process()
        process.executableURL = executableURL
        process.arguments = ["app-server", "--stdio"]
        var environment = ProcessInfo.processInfo.environment
        ["OPENAI_API_KEY", "CODEX_API_KEY", "AWS_SECRET_ACCESS_KEY", "AWS_SESSION_TOKEN"].forEach { environment.removeValue(forKey: $0) }
        process.environment = environment
        let stdin = Pipe(), stdout = Pipe()
        process.standardInput = stdin
        process.standardOutput = stdout
        // stderr is diagnostic output, not part of the RPC transport. An unread pipe can block the server.
        process.standardError = FileHandle.nullDevice
        stdout.fileHandleForReading.readabilityHandler = { [weak self] handle in
            let data = handle.availableData
            guard !data.isEmpty else {
                // EOF stays readable until the handler is removed; otherwise it creates tasks forever.
                handle.readabilityHandler = nil
                Task { await self?.connectionEnded(generation: generation) }
                return
            }
            Task { await self?.receive(data, generation: generation) }
        }
        do { try process.run() } catch {
            stdout.fileHandleForReading.readabilityHandler = nil
            try? stdin.fileHandleForWriting.close(); try? stdin.fileHandleForReading.close()
            try? stdout.fileHandleForWriting.close(); try? stdout.fileHandleForReading.close()
            throw JSONRPCError.launchFailed
        }
        try? stdin.fileHandleForReading.close()
        try? stdout.fileHandleForWriting.close()
        self.process = process
        self.input = stdin.fileHandleForWriting
        self.output = stdout.fileHandleForReading
    }

    func initialize() async throws {
        try connect()
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
        let data = try await waitForResponse(id: id, payload: payload, timeout: timeout)
        let response = try JSONDecoder().decode(ResponseEnvelope<Response>.self, from: data)
        if let error = response.error { throw JSONRPCError.protocolError(error.message) }
        guard let result = response.result else { throw JSONRPCError.protocolError("响应缺少结果") }
        return result
    }

    func requestWithoutParams<Response: Decodable>(method: String, timeout: TimeInterval = 12) async throws -> Response {
        try connect()
        let id = nextID; nextID += 1
        let payload = try JSONEncoder().encode(JSONRPCRequestWithoutParams(id: id, method: method))
        let data = try await waitForResponse(id: id, payload: payload, timeout: timeout)
        let response = try JSONDecoder().decode(ResponseEnvelope<Response>.self, from: data)
        if let error = response.error { throw JSONRPCError.protocolError(error.message) }
        guard let result = response.result else { throw JSONRPCError.protocolError("响应缺少结果") }
        return result
    }

    func close() {
        generation += 1
        output?.readabilityHandler = nil
        try? output?.close(); try? input?.close()
        if process?.isRunning == true { process?.terminate() }
        process = nil; input = nil; output = nil; didInitialize = false; readBuffer = Data()
        timeouts.values.forEach { $0.cancel() }; timeouts = [:]
        continuations.values.forEach { $0.resume(throwing: JSONRPCError.launchFailed) }; continuations = [:]
    }

    private func connectionEnded(generation: Int) { if generation == self.generation { close() } }

    private func waitForResponse(id: Int, payload: Data, timeout: TimeInterval) async throws -> Data {
        try await withCheckedThrowingContinuation { continuation in
            continuations[id] = continuation
            timeouts[id] = Task {
                do { try await Task.sleep(for: .seconds(timeout)) } catch { return }
                self.expire(id: id)
            }
            do { try send(payload) } catch { continuations.removeValue(forKey: id); timeouts.removeValue(forKey: id)?.cancel(); continuation.resume(throwing: error) }
        }
    }

    private func expire(id: Int) { timeouts.removeValue(forKey: id); continuations.removeValue(forKey: id)?.resume(throwing: JSONRPCError.timeout) }

    private func notifyInitialized() throws { try send(JSONEncoder().encode(JSONRPCInitializedNotification(method: "initialized"))) }
    private func send(_ data: Data) throws { guard let input else { throw JSONRPCError.launchFailed }; try input.write(contentsOf: data + Data([0x0A])) }

    private func receive(_ data: Data, generation: Int) {
        guard generation == self.generation else { return }
        readBuffer.append(data)
        while let newline = readBuffer.firstIndex(of: 0x0A) {
            let line = readBuffer.prefix(upTo: newline)
            readBuffer.removeSubrange(...newline)
            guard let envelope = try? JSONDecoder().decode(ResponseID.self, from: Data(line)), let id = envelope.id, let continuation = continuations.removeValue(forKey: id) else { continue }
            timeouts.removeValue(forKey: id)?.cancel()
            continuation.resume(returning: Data(line))
        }
        if readBuffer.count > 1_048_576 { close() }
    }
}

private struct JSONRPCRequest<Params: Encodable>: Encodable { let jsonrpc = "2.0"; let id: Int; let method: String; let params: Params }
private struct JSONRPCRequestWithoutParams: Encodable { let jsonrpc = "2.0"; let id: Int; let method: String }
private struct JSONRPCInitializedNotification: Encodable { let jsonrpc = "2.0"; let method: String }
private struct InitializeResponse: Decodable {}
private struct ResponseID: Decodable { let id: Int? }
private struct ResponseEnvelope<Result: Decodable>: Decodable { let result: Result?; let error: RPCError? }
private struct RPCError: Decodable { let message: String }
