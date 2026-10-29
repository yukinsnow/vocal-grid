import Foundation
import CoreFoundation

public struct ConversionOptions: Sendable, Codable {
    public var bpm = 120.0
    public var numerator = 4
    public var denominator = 4
    public var singer = "Mai"
    public var generation2 = false
    public var lyric = "啦"
    public var skipDrums = false
    public var alignNotes = true
    public var gridDivision = 32
    public var scaleEnabled = true
    public var scaleRoot = "C"
    public var scaleType = "Major"
    public var exportStems = true
    public var exportMixDown = false
    public var channels = 2
    public var aspiration = "noAspiration"
    public var bitDepth = 24
    public var sampleRate = 48000
    public var bypassGainPan = true
    public var bypassEffects = true
    public init() {}
}

public struct ConversionReport: Sendable {
    public let sourceTracks: Int
    public let outputTracks: Int
    public let notes: Int
    public let warnings: [String]
    public let output: URL
}

public enum ConversionError: LocalizedError {
    case invalid(String)
    public var errorDescription: String? {
        switch self { case .invalid(let message): return message }
    }
}

private struct Note {
    var start: Int64
    var end: Int64
    let pitch: Int
    let channel: Int
    let order: Int
    var lyric: String?
}

private struct Reader {
    let data: [UInt8]
    var position = 0
    init(_ data: Data) { self.data = Array(data) }
    var atEnd: Bool { position >= data.count }
    mutating func byte() throws -> Int {
        guard position < data.count else { throw ConversionError.invalid("MIDI 数据意外结束。") }
        defer { position += 1 }
        return Int(data[position])
    }
    mutating func bytes(_ count: Int) throws -> Data {
        guard count >= 0, count <= data.count - position else { throw ConversionError.invalid("MIDI 块长度无效。") }
        defer { position += count }
        return Data(data[position..<(position + count)])
    }
    mutating func uint16() throws -> Int { let a = try byte(); return try a * 256 + byte() }
    mutating func uint32() throws -> Int {
        var value = 0
        for _ in 0..<4 { value = try value * 256 + byte() }
        return value
    }
    mutating func variable() throws -> Int64 {
        var value: Int64 = 0
        for _ in 0..<4 {
            let b = try byte(); value = value * 128 + Int64(b & 127)
            if b < 128 { return value }
        }
        throw ConversionError.invalid("无效的 MIDI 可变长度整数。")
    }
    mutating func tag() throws -> String { String(decoding: try bytes(4), as: UTF8.self) }
}

public enum Converter {
    public static let grid: Int64 = 88_200_000
    private static func uuid() -> String { UUID().uuidString.lowercased() }
    private static func decode(_ data: Data) -> String {
        let gb = String.Encoding(rawValue: CFStringConvertEncodingToNSStringEncoding(CFStringEncoding(CFStringEncodings.GB_18030_2000.rawValue)))
        return (String(data: data, encoding: .utf8) ?? String(data: data, encoding: gb) ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
    }
    private static func parse(_ data: Data, skipDrums: Bool) throws -> (Int, [[Note]], [String]) {
        var reader = Reader(data)
        guard try reader.tag() == "MThd" else { throw ConversionError.invalid("请选择标准 MIDI 文件（.mid / .midi）。") }
        let header = try reader.uint32()
        guard header >= 6 else { throw ConversionError.invalid("MIDI 文件头不完整。") }
        let format = try reader.uint16(), count = try reader.uint16(), ppq = try reader.uint16()
        _ = try reader.bytes(header - 6)
        guard format <= 1 else { throw ConversionError.invalid("暂不支持格式 2 MIDI，请转换为格式 0 或 1。") }
        guard ppq > 0, ppq & 0x8000 == 0 else { throw ConversionError.invalid("不支持 SMPTE 时间制，请转换为 PPQ 时间制。") }
        var tracks: [[Note]] = [], warnings: [String] = []
        for _ in 0..<count {
            guard try reader.tag() == "MTrk" else { throw ConversionError.invalid("找不到 MIDI 轨道块。") }
            let size = try reader.uint32()
            var track = Reader(try reader.bytes(size))
            var tick: Int64 = 0, running = 0, order = 0
            var notes: [Note] = [], active: [Int: [Note]] = [:], lyrics: [Int64: [String]] = [:]
            while !track.atEnd {
                tick += try track.variable()
                var status = try track.byte()
                var first: Int? = nil
                if status < 128 {
                    guard running != 0 else { throw ConversionError.invalid("无效的 MIDI running status。") }
                    first = status; status = running
                } else if status < 240 { running = status }
                if status == 255 {
                    let type = try track.byte(), length = try track.variable()
                    let payload = try track.bytes(Int(length))
                    if type == 5 { lyrics[tick, default: []].append(decode(payload)) }
                    if type == 47 { break }
                    continue
                }
                if status == 240 || status == 247 {
                    running = 0; let length = try track.variable(); _ = try track.bytes(Int(length)); continue
                }
                guard status < 240 else { throw ConversionError.invalid("不支持的 MIDI 系统事件。") }
                let kind = status >> 4, channel = status & 15
                let a: Int
                if let first = first { a = first } else { a = try track.byte() }
                let b: Int
                if kind == 12 || kind == 13 { b = 0 } else { b = try track.byte() }
                guard a < 128, b < 128 else { throw ConversionError.invalid("无效的 MIDI 事件数据。") }
                if skipDrums && channel == 9 { continue }
                let key = channel * 128 + a
                if kind == 9 && b > 0 {
                    active[key, default: []].append(Note(start: tick, end: tick, pitch: a, channel: channel, order: order))
                    order += 1
                } else if kind == 8 || (kind == 9 && b == 0) {
                    if var queue = active[key], !queue.isEmpty {
                        var note = queue.removeFirst(); active[key] = queue; note.end = tick
                        if note.end > note.start { notes.append(note) }
                    }
                }
            }
            for queue in active.values {
                for var note in queue {
                    note.end = max(note.start + Int64(max(1, ppq / 8)), tick); notes.append(note)
                    warnings.append("有未配对的 Note On，已在轨道末尾补齐。")
                }
            }
            notes.sort { $0.start == $1.start ? $0.order < $1.order : $0.start < $1.start }
            for index in notes.indices {
                if var queue = lyrics[notes[index].start], !queue.isEmpty {
                    notes[index].lyric = queue.removeFirst(); lyrics[notes[index].start] = queue
                }
            }
            tracks.append(notes)
        }
        return (ppq, tracks, warnings)
    }
        private static func snap(_ tick: Int64, ppq: Int, division: Int) -> Int64 {
        let steps = Int64(division / 4)
        return ((tick * steps * 2 + Int64(ppq)) / (2 * Int64(ppq))) * (705_600_000 / steps)
    }
    private static func exact(_ tick: Int64, ppq: Int) -> Int64 {
        Int64((Double(tick) * 705_600_000 / Double(ppq)).rounded(.toNearestOrAwayFromZero))
    }
    public static func parseSinger(_ value: String) throws -> (String, Bool) {
        let parts = value.components(separatedBy: "|").map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
        guard parts.count <= 2, !parts[0].isEmpty else { throw ConversionError.invalid("歌姬格式：Mai | 1；二代格式：工程内声库名 | 2。") }
        if parts.count == 2 && !["1", "2"].contains(parts[1]) { throw ConversionError.invalid("歌姬代际只允许 1 或 2。") }
        return (parts[0].lowercased() == "mai" ? "Mai" : parts[0], parts.count == 2 && parts[1] == "2")
    }
    private static func track(_ notes: [Note], number: Int, options: ConversionOptions) -> [String: Any] {
        let group = uuid(), name = "Melody\(number)"
        var parameters: [String: Any] = [:]
        for key in ["pitchDelta", "vibratoEnv", "loudness", "tension", "breathiness", "voicing", "gender", "toneShift", "mouthOpening"] {
            parameters[key] = ["mode": "cubic", "points": []] as [String: Any]
        }
        let converted: [[String: Any]] = notes.map { note in
            let lyric = (note.lyric?.isEmpty == false) ? note.lyric! : options.lyric
            return ["uuid": String(uuid().replacingOccurrences(of: "-", with: "").prefix(16)), "musicalType": "singing",
                    "onset": note.start, "duration": note.end - note.start, "pitch": note.pitch, "detune": 0,
                    "lyrics": lyric, "phonemes": "", "accent": "",
                    "attributes": ["evenSyllableDuration": true, "muted": false, "languageOverride": "mandarin", "phonesetOverride": "xsampa"]]
        }
        var result: [String: Any] = ["name": name, "dispColor": "ff7db235", "dispOrder": number - 1, "renderEnabled": options.exportStems,
                "mixer": ["gainDecibel": 0.0, "pan": 0.0, "mute": false, "solo": false, "display": true, "fxPresetName": "",
                          "fxParams": ["room": ["enabled": false], "postRoomEq": ["enabled": false], "compressor": ["enabled": false], "reverb": ["enabled": false]]],
                "mainGroup": ["name": name, "uuid": group, "parameters": parameters, "vocalModes": [:], "pitchControls": [], "notes": converted, "musicalScale": ["type": options.scaleType, "root": options.scaleRoot]],
                "mainRef": ["uuid": uuid(), "groupID": group, "blickAbsoluteBegin": 0, "blickAbsoluteEnd": -1,
                            "blickOffset": 0, "pitchOffset": 0, "mute": false, "isInstrumental": false,
                            "database": ["name": options.singer, "language": "japanese", "phoneset": "romaji", "languageOverride": "mandarin", "phonesetOverride": "xsampa",
                                         "backendType": options.generation2 ? "SVR3" : "SVR2AI", "version": options.generation2 ? "201" : (options.singer == "Mai" ? "104" : "100")],
                            "dictionary": "", "voice": ["vocalModeInherited": true, "vocalModePreset": "", "vocalModeParams": [:]], "voicePresetName": ""],
                "groups": []]
        if !options.scaleEnabled, var group = result["mainGroup"] as? [String: Any] { group.removeValue(forKey: "musicalScale"); result["mainGroup"] = group }
        return result
    }
    public static func convert(input: URL, output: URL, options original: ConversionOptions) throws -> ConversionReport {
        var options = original
        options.singer = options.singer.trimmingCharacters(in: .whitespacesAndNewlines)
        options.lyric = options.lyric.trimmingCharacters(in: .whitespacesAndNewlines)
        if options.singer.contains("|") { (options.singer, options.generation2) = try parseSinger(options.singer) }
        guard [4,8,16,32,64].contains(options.gridDivision) else { throw ConversionError.invalid("网格只能为 4、8、16、32 或 64。") }
        guard options.exportStems || options.exportMixDown else { throw ConversionError.invalid("至少启用分轨或混音输出。") }
        guard [1,2].contains(options.channels), [16,24,32].contains(options.bitDepth), [44100,48000,88200,96000].contains(options.sampleRate), ["noAspiration","asFiles"].contains(options.aspiration) else { throw ConversionError.invalid("导出格式无效。") }
        if options.scaleEnabled { guard ["C","C#","D","D#","E","F","F#","G","G#","A","A#","B"].contains(options.scaleRoot), ["Major","Minor"].contains(options.scaleType) else { throw ConversionError.invalid("调式音阶无效。") } }
        guard options.bpm.isFinite, (5...999).contains(options.bpm) else { throw ConversionError.invalid("BPM 范围为 5–999。") }
        guard (1...64).contains(options.numerator), [1, 2, 4, 8, 16, 32, 64].contains(options.denominator) else { throw ConversionError.invalid("拍号无效。") }
        guard !options.singer.isEmpty else { throw ConversionError.invalid("请输入歌姬代号，例如 Mai。") }
        guard !options.lyric.isEmpty else { throw ConversionError.invalid("请输入默认歌词，例如：啦。") }
        let (ppq, sources, warnings) = try parse(Data(contentsOf: input), skipDrums: options.skipDrums)
        var tracks: [[String: Any]] = [], total = 0, sourceCount = 0
        for raw in sources where !raw.isEmpty {
            sourceCount += 1
            let channels = Dictionary(grouping: raw, by: { $0.channel })
            for key in channels.keys.sorted() {
                var lanes: [[Note]] = []
                for var note in channels[key]!.sorted(by: { $0.start == $1.start ? $0.order < $1.order : $0.start < $1.start }) {
                                        note.start = options.alignNotes ? snap(note.start, ppq: ppq, division: options.gridDivision) : exact(note.start, ppq: ppq)
                    let minimum: Int64 = options.alignNotes ? 705_600_000 / Int64(options.gridDivision / 4) : 1
                    note.end = max(note.start + minimum, options.alignNotes ? snap(note.end, ppq: ppq, division: options.gridDivision) : exact(note.end, ppq: ppq))
                    if let lane = lanes.firstIndex(where: { $0.last!.end <= note.start }) { lanes[lane].append(note) }
                    else { lanes.append([note]) }
                    total += 1
                }
                for lane in lanes { tracks.append(track(lane, number: tracks.count + 1, options: options)) }
            }
        }
        guard !tracks.isEmpty else { throw ConversionError.invalid("没有可转换的音符。") }
        let project: [String: Any] = ["version": 196, "uuid": uuid(),
            "time": ["meter": [["index": 0, "numerator": options.numerator, "denominator": options.denominator]], "tempo": [["position": 0, "bpm": options.bpm]], "startTimeSeconds": 0.0],
            "library": [], "tracks": tracks,
            "renderConfig": ["destination": output.deletingLastPathComponent().path, "filename": "vocal", "numChannels": options.channels,
                             "aspirationFormat": options.aspiration, "bitDepth": options.bitDepth, "sampleRate": options.sampleRate, "exportMixDown": options.exportMixDown, "exportPitch": false,
                             "bypassPan": options.bypassGainPan, "bypassGain": options.bypassGainPan, "bypassEffects": options.bypassEffects], "projectMixer": ["linkRoomSettings": true]]
        let data = try JSONSerialization.data(withJSONObject: project, options: [.sortedKeys])
        let fm = FileManager.default
        try fm.createDirectory(at: output.deletingLastPathComponent(), withIntermediateDirectories: true)
        if fm.fileExists(atPath: output.path) {
            let backup = output.appendingPathExtension("\(Int64(Date().timeIntervalSince1970 * 1000))-\(uuid().prefix(8)).bak")
            try fm.copyItem(at: output, to: backup)
        }
        try data.write(to: output, options: .atomic)
        return ConversionReport(sourceTracks: sourceCount, outputTracks: tracks.count, notes: total, warnings: Array(Set(warnings)).sorted(), output: output)
    }
    public static func midiCandidates(_ urls: [URL]) throws -> [URL] {
        let fm = FileManager.default
        var candidates = Set<URL>()
        for url in urls {
            var directory: ObjCBool = false
            guard fm.fileExists(atPath: url.path, isDirectory: &directory) else { continue }
            let entries = directory.boolValue ? try fm.contentsOfDirectory(at: url, includingPropertiesForKeys: [.isRegularFileKey], options: [.skipsHiddenFiles]) : [url]
            for entry in entries where ["mid", "midi"].contains(entry.pathExtension.lowercased()) {
                if (try entry.resourceValues(forKeys: [.isRegularFileKey])).isRegularFile == true { candidates.insert(entry.standardizedFileURL) }
            }
        }
        return candidates.sorted { $0.path.localizedStandardCompare($1.path) == .orderedAscending }
    }
}
