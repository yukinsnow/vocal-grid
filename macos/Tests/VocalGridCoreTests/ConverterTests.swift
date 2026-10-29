import XCTest
@testable import VocalGridCore

final class ConverterTests: XCTestCase {
    private func midi(_ tracks: [[UInt8]], ppq: Int = 480, format: Int = 1) -> Data {
        var bytes: [UInt8] = [77, 84, 104, 100, 0, 0, 0, 6, 0, UInt8(format), 0, UInt8(tracks.count), UInt8(ppq >> 8), UInt8(ppq & 255)]
        for track in tracks {
            bytes += [77, 84, 114, 107, UInt8((track.count >> 24) & 255), UInt8((track.count >> 16) & 255), UInt8((track.count >> 8) & 255), UInt8(track.count & 255)]
            bytes += track
        }
        return Data(bytes)
    }
    private func workspace() throws -> URL {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: root) }
        return root
    }
    private func load(_ url: URL) throws -> [String: Any] { try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: Any]) }
    func testPolyphonyGridAndRenderSettings() throws {
        let root = try workspace(), source = root.appendingPathComponent("poly.mid"), output = root.appendingPathComponent("vocal.svp")
        try midi([[0, 255, 81, 3, 7, 161, 32, 0, 255, 47, 0], [0, 144, 60, 100, 0, 144, 64, 100, 120, 128, 60, 0, 120, 128, 64, 0, 0, 255, 47, 0], [0, 145, 67, 100, 131, 96, 129, 67, 0, 0, 255, 47, 0]]).write(to: source)
        var options = ConversionOptions(); options.bpm = 135; options.numerator = 7; options.denominator = 8
        let report = try Converter.convert(input: source, output: output, options: options)
        XCTAssertEqual(report.notes, 3); XCTAssertEqual(report.outputTracks, 3); XCTAssertEqual(report.sourceTracks, 2)
        let project = try load(output), tracks = try XCTUnwrap(project["tracks"] as? [[String: Any]])
        for (index, track) in tracks.enumerated() {
            XCTAssertEqual(track["name"] as? String, "Melody\(index + 1)")
            let reference = try XCTUnwrap(track["mainRef"] as? [String: Any]), database = try XCTUnwrap(reference["database"] as? [String: Any])
            XCTAssertEqual(database["name"] as? String, "Mai"); XCTAssertEqual(database["languageOverride"] as? String, "mandarin")
            let group = try XCTUnwrap(track["mainGroup"] as? [String: Any]), notes = try XCTUnwrap(group["notes"] as? [[String: Any]])
            for note in notes {
                let start = try XCTUnwrap(note["onset"] as? NSNumber).int64Value, duration = try XCTUnwrap(note["duration"] as? NSNumber).int64Value
                XCTAssertEqual(start % Converter.grid, 0); XCTAssertEqual(duration % Converter.grid, 0); XCTAssertGreaterThan(duration, 0)
            }
        }
        let render = try XCTUnwrap(project["renderConfig"] as? [String: Any])
        XCTAssertEqual(render["numChannels"] as? Int, 2); XCTAssertEqual(render["bitDepth"] as? Int, 24); XCTAssertEqual(render["sampleRate"] as? Int, 48000)
        XCTAssertEqual(render["exportMixDown"] as? Bool, false); XCTAssertEqual(render["aspirationFormat"] as? String, "noAspiration")
        for key in ["bypassGain", "bypassPan", "bypassEffects"] { XCTAssertEqual(render[key] as? Bool, true) }
        let time = try XCTUnwrap(project["time"] as? [String: Any])
        XCTAssertEqual((time["tempo"] as? [[String: Any]])?.first?["bpm"] as? Double, 135)
        XCTAssertEqual((time["meter"] as? [[String: Any]])?.first?["numerator"] as? Int, 7)
        XCTAssertEqual((time["meter"] as? [[String: Any]])?.first?["denominator"] as? Int, 8)
        _ = try Converter.convert(input: source, output: output, options: options)
        XCTAssertTrue(try FileManager.default.contentsOfDirectory(atPath: root.path).contains { $0.hasSuffix(".bak") })
    }
    func testLyricsRunningStatusDrumsAndCustomVoice() throws {
        let root = try workspace(), source = root.appendingPathComponent("lyrics.mid"), output = root.appendingPathComponent("vocal.svp")
        try midi([[0, 255, 5, 3, 229, 149, 166, 0, 144, 60, 100, 1, 60, 0, 0, 153, 35, 100, 120, 35, 0, 0, 255, 47, 0]], ppq: 100, format: 0).write(to: source)
        var options = ConversionOptions(); options.singer = "Custom Singer 2"; options.generation2 = true
        let report = try Converter.convert(input: source, output: output, options: options)
        XCTAssertEqual(report.notes, 1)
        let project = try load(output), tracks = try XCTUnwrap(project["tracks"] as? [[String: Any]]), track = try XCTUnwrap(tracks.first)
        let ref = try XCTUnwrap(track["mainRef"] as? [String: Any]), db = try XCTUnwrap(ref["database"] as? [String: Any])
        XCTAssertEqual(db["name"] as? String, "Custom Singer 2"); XCTAssertEqual(db["backendType"] as? String, "SVR3")
        let group = try XCTUnwrap(track["mainGroup"] as? [String: Any]), note = try XCTUnwrap((group["notes"] as? [[String: Any]])?.first)
        XCTAssertEqual(note["lyrics"] as? String, "啦"); XCTAssertEqual((note["duration"] as? NSNumber)?.int64Value, Converter.grid)
    }
    func testFolderDiscoveryAndInvalidInput() throws {
        let root = try workspace(), source = root.appendingPathComponent("one.MID"), output = root.appendingPathComponent("vocal.svp")
        try midi([[0, 255, 47, 0]], format: 2).write(to: source)
        try Data().write(to: root.appendingPathComponent("ignore.txt"))
        try FileManager.default.createDirectory(at: root.appendingPathComponent("fake.mid"), withIntermediateDirectories: true)
        XCTAssertEqual(try Converter.midiCandidates([root]), [source])
        XCTAssertThrowsError(try Converter.convert(input: source, output: output, options: ConversionOptions()))
        try Data([77, 84, 104, 100, 0]).write(to: source)
        XCTAssertThrowsError(try Converter.convert(input: source, output: output, options: ConversionOptions()))
        XCTAssertFalse(FileManager.default.fileExists(atPath: output.path))
    }
}
