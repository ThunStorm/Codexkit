import Foundation

struct CodexProcessLocator {
    func locate() -> URL? {
        let manager = FileManager.default
        let candidates = [
            UserDefaults.standard.string(forKey: "codexExecutablePath"),
            "/Applications/ChatGPT.app/Contents/Resources/codex-cli/bin/codex",
            "/Applications/ChatGPT.app/Contents/Resources/codex-cli/CodexCLI.app/Contents/MacOS/codex",
            "/Applications/ChatGPT.app/Contents/Resources/codex",
            "/opt/homebrew/bin/codex", "/usr/local/bin/codex"
        ].compactMap { $0 }.map(URL.init(fileURLWithPath:))
        return candidates.first { manager.isExecutableFile(atPath: $0.path) }
    }
}
