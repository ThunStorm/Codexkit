// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "CodexMenuMeter",
    platforms: [.macOS(.v14)],
    products: [.executable(name: "CodexMenuMeter", targets: ["CodexMenuMeter"])],
    targets: [
        .executableTarget(name: "CodexMenuMeter"),
        .testTarget(name: "CodexMenuMeterTests", dependencies: ["CodexMenuMeter"])
    ]
)
