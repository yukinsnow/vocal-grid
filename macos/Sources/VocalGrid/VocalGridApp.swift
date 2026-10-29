import SwiftUI
import AppKit
import UniformTypeIdentifiers
import VocalGridCore

@main
@MainActor
struct VocalGridApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var delegate
    var body: some Scene {
        WindowGroup("Vocal Grid") { ContentView() }
            .defaultSize(width: 850, height: 640)
            .windowResizability(.contentSize)
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}

@MainActor
final class ConverterModel: ObservableObject {
    struct Saved: Codable { var options: ConversionOptions; var singer: String; var followInput: Bool; var manualFolder: String? }
    @Published var inputs: [URL] = [] { didSet { if followInput, let input = inputs.first { folder = input.deletingLastPathComponent() } } }
    var input: URL? { inputs.first }
    @Published var folder = FileManager.default.homeDirectoryForCurrentUser { didSet { save() } }
    @Published var options = ConversionOptions() { didSet { save() } }
    @Published var singer = "Mai" { didSet { save() } }
    @Published var followInput = true { didSet { if followInput, let input = input { folder = input.deletingLastPathComponent() }; save() } }
    @Published var busy = false
    @Published var status = "就绪 · 设置自动保存在本机"
    @Published var message = ""
    @Published var showMessage = false
    @Published var candidates: [URL] = []
    @Published var candidate: URL?
    @Published var showCandidates = false
    private var ready = false
    private let defaults: UserDefaults
    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        if let data = defaults.data(forKey: "VocalGrid.preferences.v1") {
            do {
                let saved = try JSONDecoder().decode(Saved.self, from: data)
                options = saved.options; singer = saved.singer == "Mai | 1" ? "Mai" : saved.singer; followInput = saved.followInput
                if !followInput, let path = saved.manualFolder { folder = URL(fileURLWithPath: path, isDirectory: true) }
            } catch { status = "本机设置无法读取，已恢复默认值。" }
        }
        options.skipDrums = false
        ready = true
    }
    func save() {
        guard ready else { return }
        let saved = Saved(options: options, singer: singer, followInput: followInput, manualFolder: followInput ? nil : folder.path)
        if let data = try? JSONEncoder().encode(saved) { defaults.set(data, forKey: "VocalGrid.preferences.v1") }
    }
    func alert(_ text: String) { message = text; showMessage = true }
    func chooseInput() {
        let panel = NSOpenPanel()
        panel.title = "选择 MIDI 文件或包含 MIDI 的文件夹"
        panel.canChooseDirectories = true; panel.canChooseFiles = true; panel.allowsMultipleSelection = true
        panel.allowedContentTypes = [UTType(filenameExtension: "mid") ?? .data, UTType(filenameExtension: "midi") ?? .data, .folder]
        if panel.runModal() == .OK { accept(panel.urls, saving: false) }
    }
    func readVoice() {
        let panel = NSOpenPanel(); panel.allowedContentTypes = [UTType(filenameExtension: "svp") ?? .data]
        if panel.runModal() == .OK, let url = panel.url {
            do { singer = try Converter.readSinger(from: url) } catch { alert(error.localizedDescription) }
        }
    }
    func chooseFolder() {
        let panel = NSOpenPanel()
        panel.title = "选择 vocal.svp 的保存目录"
        panel.canChooseFiles = false; panel.canChooseDirectories = true; panel.allowsMultipleSelection = false
        panel.directoryURL = folder
        if panel.runModal() == .OK, let url = panel.url { followInput = false; folder = url; status = "保存目录已更新" }
    }
    func accept(_ urls: [URL], saving: Bool) {
        guard !busy else { return }
        do {
            if saving {
                guard urls.count == 1, (try urls[0].resourceValues(forKeys: [.isDirectoryKey])).isDirectory == true else {
                    throw ConversionError.invalid("请在保存区域拖入一个文件夹。")
                }
                followInput = false; folder = urls[0]; status = "保存目录已更新"; return
            }
            let files = try Converter.midiCandidates(urls)
            guard !files.isEmpty else { throw ConversionError.invalid("没有找到 MIDI 文件。文件夹只读取当前层级。") }
            inputs += files.filter { !inputs.contains($0) }; status = "已导入 \(inputs.count) 个 MIDI · 同起点合并"
        } catch { alert(error.localizedDescription) }
    }
    func receive(_ providers: [NSItemProvider], saving: Bool) -> Bool {
        guard !busy else { return false }
        let matches = providers.filter { $0.hasItemConformingToTypeIdentifier(UTType.fileURL.identifier) }
        guard !matches.isEmpty else { return false }
        let group = DispatchGroup(), lock = NSLock()
        var urls: [URL] = []
        for provider in matches {
            group.enter()
            provider.loadItem(forTypeIdentifier: UTType.fileURL.identifier, options: nil) { item, _ in
                defer { group.leave() }
                let url: URL?
                if let value = item as? URL { url = value }
                else if let data = item as? Data { url = URL(dataRepresentation: data, relativeTo: nil) }
                else if let text = item as? String { url = URL(string: text) }
                else { url = nil }
                if let url = url, url.isFileURL { lock.lock(); urls.append(url); lock.unlock() }
            }
        }
        group.notify(queue: .main) { [weak self] in
            if urls.isEmpty { self?.alert("无法读取拖入的文件，请使用选择按钮。") }
            else { self?.accept(urls, saving: saving) }
        }
        return true
    }
    func convert() {
        guard !busy else { return }
        guard !inputs.isEmpty else { alert("请先选择或拖入 MIDI 文件。"); return }
        let sources = inputs
        var snapshot = options
        snapshot.singer = singer; snapshot.skipDrums = false
        let output = folder.appendingPathComponent("vocal.svp")
        save()
        busy = true; status = "正在量化音符、整理声部…"
        Task {
            do {
                let report = try await Task.detached(priority: .userInitiated) {
                    try Converter.convert(inputs: sources, output: output, options: snapshot)
                }.value
                status = "已生成 vocal.svp · \(report.outputTracks) 个声部 / \(report.notes) 个音符"
                alert("完成：\(report.sourceTracks) 个 MIDI 音符轨 → \(report.outputTracks) 个声部轨，共 \(report.notes) 个音符。\n\n\(report.warnings.joined(separator: "\n"))\n保存：\(report.output.path)\n\n请在 SV2 中打开；需要已安装并激活对应歌姬。")
            } catch { status = "未完成 · 请检查输入"; alert(error.localizedDescription) }
            busy = false
        }
    }
}

@MainActor
struct ContentView: View {
    @StateObject private var model = ConverterModel()
    @State private var inputTargeted = false
    @State private var outputTargeted = false
    private let accent = Color(red: 0.047, green: 0.494, blue: 0.439)
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Image(nsImage: NSApplication.shared.applicationIconImage).resizable().frame(width: 36, height: 36)
                Text("Vocal Grid").font(.system(size: 23, weight: .bold, design: .rounded))
                Spacer(); Text("多 MIDI → 一个 SVP · 本机记忆").foregroundStyle(.secondary)
            }
            card {
                HStack { Text("导入 MIDI").bold(); Text("多选或拖入文件 / 文件夹，同起点合并").font(.caption).foregroundStyle(.secondary); Spacer() }
                HStack {
                    Text(model.inputs.isEmpty ? "尚未导入 MIDI" : "\(model.inputs.count) 个文件：" + model.inputs.map(\.lastPathComponent).joined(separator: "、")).lineLimit(1).truncationMode(.middle).help(model.inputs.map(\.path).joined(separator: "\n"))
                    Spacer(); Button("添加 MIDI", action: model.chooseInput); Button("清空") { model.inputs = [] }
                }
            }.overlay(RoundedRectangle(cornerRadius: 10).stroke(inputTargeted ? accent : .clear, lineWidth: 2))
             .onDrop(of: [UTType.fileURL.identifier], isTargeted: $inputTargeted) { model.receive($0, saving: false) }
            HStack(alignment: .top, spacing: 12) {
                card {
                    Text("节奏与歌声").bold()
                    HStack {
                        field("BPM") { TextField("120", value: $model.options.bpm, format: .number) }.frame(width: 80)
                        field("拍号") { HStack(spacing: 3) { TextField("4", value: $model.options.numerator, format: .number); Text("/"); Picker("分母", selection: $model.options.denominator) { ForEach([1,2,4,8,16,32,64], id: \.self) { Text("\($0)").tag($0) } }.labelsHidden() } }.frame(width: 150)
                        field("歌词") { TextField("啦", text: $model.options.lyric) }
                    }
                    field("声库标记 database.name / 完整 database JSON") {
                        HStack { TextField("Mai", text: $model.singer); Button("从 SVP 读取", action: model.readVoice) }
                    }
                    Text("Mai / Mai 2；星尘一代：MEDIUM5·Stardust").font(.caption2).foregroundStyle(.secondary)
                    HStack { Toggle("量化音符", isOn: $model.options.alignNotes); Spacer(); Picker("1/", selection: $model.options.gridDivision) { ForEach([4,8,16,32,64], id: \.self) { Text("\($0)").tag($0) } }.frame(width: 110).disabled(!model.options.alignNotes) }
                    Text("SV2 工具栏网格由 SV2 本机偏好控制").font(.caption2).foregroundStyle(.secondary)
                    HStack {
                        Toggle("调式音阶", isOn: $model.options.scaleEnabled)
                        Picker("主音", selection: $model.options.scaleRoot) { ForEach(["C","C#","D","D#","E","F","F#","G","G#","A","A#","B"], id: \.self) { Text($0).tag($0) } }.labelsHidden().frame(width: 70).disabled(!model.options.scaleEnabled)
                        Picker("调式", selection: $model.options.scaleType) { Text("大调").tag("Major"); Text("自然小调").tag("Minor") }.labelsHidden().disabled(!model.options.scaleEnabled)
                    }
                }.frame(width: 400)
                card {
                    Text("音频导出").bold()
                    HStack {
                        field("输出内容") { HStack { Toggle("分轨", isOn: $model.options.exportStems); Toggle("混音", isOn: $model.options.exportMixDown) } }
                        field("声道") { Picker("声道", selection: $model.options.channels) { Text("单声道").tag(1); Text("立体声").tag(2) }.labelsHidden() }
                    }
                    HStack {
                        field("位深") { Picker("位深", selection: $model.options.bitDepth) { ForEach([16,24,32], id: \.self) { Text("\($0) 位").tag($0) } }.labelsHidden() }
                        field("采样率") { Picker("采样率", selection: $model.options.sampleRate) { ForEach([44100,48000,88200,96000], id: \.self) { Text("\($0) Hz").tag($0) } }.labelsHidden() }
                    }
                    Picker("气声输出", selection: $model.options.aspiration) { Text("无").tag("noAspiration"); Text("增加文件输出气声").tag("asFiles") }
                    Toggle("旁通增益和声像", isOn: $model.options.bypassGainPan)
                    Toggle("旁通效果", isOn: $model.options.bypassEffects)
                }
            }
            card {
                HStack { Text("保存到").bold(); Spacer(); Toggle("跟随首个 MIDI 目录", isOn: $model.followInput) }
                HStack { Text(model.folder.path).lineLimit(1).truncationMode(.middle).help(model.folder.path); Spacer(); Button("选择目录", action: model.chooseFolder) }
            }.overlay(RoundedRectangle(cornerRadius: 10).stroke(outputTargeted ? accent : .clear, lineWidth: 2))
             .onDrop(of: [UTType.fileURL.identifier], isTargeted: $outputTargeted) { model.receive($0, saving: true) }
            HStack {
                VStack(alignment: .leading, spacing: 3) { Text(model.status).foregroundStyle(.secondary).lineLimit(1); Link("GitHub @Yukinsnow", destination: URL(string: "https://github.com/yukinsnow/vocal-grid")!).font(.caption) }
                Spacer(); if model.busy { ProgressView().controlSize(.small) }
                Button("合并生成 vocal.svp →", action: model.convert).buttonStyle(.borderedProminent).keyboardShortcut(.return, modifiers: .command).disabled(model.busy)
            }
        }.padding(18).frame(width: 850, height: 640).tint(accent).controlSize(.regular).textFieldStyle(.roundedBorder).toggleStyle(.checkbox).background(Color(nsColor: .windowBackgroundColor))
         .alert("Vocal Grid", isPresented: $model.showMessage) { Button("好", role: .cancel) {} } message: { Text(model.message) }
    }
    private func card<Content: View>(@ViewBuilder _ content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 10, content: content).padding(12).frame(maxWidth: .infinity, alignment: .leading).background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 10))
    }
    private func field<Content: View>(_ title: String, @ViewBuilder _ content: () -> Content) -> some View { VStack(alignment: .leading, spacing: 4) { Text(title).font(.caption).foregroundStyle(.secondary); content() } }
}
