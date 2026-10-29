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
        XCTAssertEqual(report.notes, 2)
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
    func testMergeDifferentPPQAndRawSinger() throws {
        let root = try workspace(), a = root.appendingPathComponent("a.mid"), b = root.appendingPathComponent("b.mid"), output = root.appendingPathComponent("merged.svp")
        try midi([[1,144,60,100,1,128,60,0,0,255,47,0]], ppq: 100, format: 0).write(to: a)
        try midi([[2,144,64,100,2,128,64,0,0,255,47,0]], ppq: 200, format: 0).write(to: b)
        var options = ConversionOptions(); options.alignNotes = false; options.scaleType = "Minor"
        options.singer = "{\"name\":\"Example 2\",\"backendType\":\"custom-engine\",\"version\":\"209\"}"
        let report = try Converter.convert(inputs: [a,b], output: output, options: options)
        XCTAssertEqual(report.sourceTracks, 2); XCTAssertEqual(report.outputTracks, 2); XCTAssertEqual(report.notes, 2)
        let project = try load(output), tracks = try XCTUnwrap(project["tracks"] as? [[String: Any]])
        for (index, track) in tracks.enumerated() {
            XCTAssertEqual(track["name"] as? String, "Melody\(index + 1)")
            let group = try XCTUnwrap(track["mainGroup"] as? [String: Any]), note = try XCTUnwrap((group["notes"] as? [[String: Any]])?.first)
            XCTAssertEqual((note["onset"] as? NSNumber)?.int64Value, 7_056_000)
            XCTAssertEqual((group["musicalScale"] as? [String: String])?["type"], "NaturalMinor")
            let ref = try XCTUnwrap(track["mainRef"] as? [String: Any]), db = try XCTUnwrap(ref["database"] as? [String: Any])
            XCTAssertEqual(db["backendType"] as? String, "custom-engine"); XCTAssertEqual(db["version"] as? String, "209")
        }
        XCTAssertTrue(try Converter.readSinger(from: output).contains("209"))
    }
    func testConfigurableGridScaleExportAndPreferences() throws {
        let root = try workspace(), source = root.appendingPathComponent("exact.mid"), output = root.appendingPathComponent("vocal.svp")
        try midi([[1,144,60,100,1,128,60,0,0,255,47,0]], ppq: 100, format: 0).write(to: source)
        for division in [4,8,16,32,64] {
            var options = ConversionOptions()
            options.gridDivision = division; options.scaleRoot = "D"; options.scaleType = "Minor"
            options.exportStems = false; options.exportMixDown = true; options.channels = 1
            options.aspiration = "asFiles"; options.bitDepth = 16; options.sampleRate = 96000
            options.bypassEffects = false; options.bypassGainPan = false; options.singer = "Mai 2 | 2"
            let restored = try JSONDecoder().decode(ConversionOptions.self, from: JSONEncoder().encode(options))
            XCTAssertEqual(restored.gridDivision, division); XCTAssertEqual(restored.sampleRate, 96000)
            _ = try Converter.convert(input: source, output: output, options: restored)
            let project = try load(output), track = try XCTUnwrap((project["tracks"] as? [[String: Any]])?.first)
            XCTAssertEqual(track["renderEnabled"] as? Bool, false)
            let group = try XCTUnwrap(track["mainGroup"] as? [String: Any]), scale = try XCTUnwrap(group["musicalScale"] as? [String: String])
            XCTAssertEqual(scale["root"], "D"); XCTAssertEqual(scale["type"], "NaturalMinor")
            let note = try XCTUnwrap((group["notes"] as? [[String: Any]])?.first), step = Int64(705_600_000 / (division / 4))
            XCTAssertEqual((note["duration"] as? NSNumber)?.int64Value, step)
            let render = try XCTUnwrap(project["renderConfig"] as? [String: Any])
            XCTAssertEqual(render["sampleRate"] as? Int, 96000); XCTAssertEqual(render["numChannels"] as? Int, 1)
            XCTAssertEqual(render["aspirationFormat"] as? String, "asFiles"); XCTAssertEqual(render["bypassEffects"] as? Bool, false)
            let ref = try XCTUnwrap(track["mainRef"] as? [String: Any]), db = try XCTUnwrap(ref["database"] as? [String: Any])
            XCTAssertEqual(db["name"] as? String, "Mai 2"); XCTAssertEqual(db["backendType"] as? String, "SVR3")
        }
        var options = ConversionOptions(); options.alignNotes = false; options.scaleEnabled = false
        _ = try Converter.convert(input: source, output: output, options: options)
        let project = try load(output), track = try XCTUnwrap((project["tracks"] as? [[String: Any]])?.first)
        let group = try XCTUnwrap(track["mainGroup"] as? [String: Any]), note = try XCTUnwrap((group["notes"] as? [[String: Any]])?.first)
        XCTAssertNil(group["musicalScale"])
        XCTAssertEqual((note["onset"] as? NSNumber)?.int64Value, 7_056_000)
        XCTAssertEqual((note["duration"] as? NSNumber)?.int64Value, 7_056_000)
        XCTAssertEqual(try Converter.parseSinger("MAI | 1").0, "Mai")
        XCTAssertThrowsError(try Converter.parseSinger("Mai | 3"))
    }
}

