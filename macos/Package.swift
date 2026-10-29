// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "VocalGrid",
    platforms: [.macOS(.v13)],
    products: [.executable(name: "VocalGrid", targets: ["VocalGrid"])],
    targets: [
        .target(name: "VocalGridCore"),
        .executableTarget(name: "VocalGrid", dependencies: ["VocalGridCore"]),
        .testTarget(name: "VocalGridCoreTests", dependencies: ["VocalGridCore"])
    ]
)
