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
            .defaultSize(width: 720, height: 780)
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
    @Published var input: URL? { didSet { if followInput, let input = input { folder = input.deletingLastPathComponent() } } }
    @Published var folder = FileManager.default.homeDirectoryForCurrentUser { didSet { save() } }
    @Published var options = ConversionOptions() { didSet { save() } }
    @Published var singer = "Mai | 1" { didSet { save() } }
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
                options = saved.options; singer = saved.singer; followInput = saved.followInput
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
            if files.count == 1 { input = files[0]; status = "MIDI 已载入 · 可调整节奏与歌声" }
            else { candidates = files; candidate = files[0]; showCandidates = true }
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
        guard let input = input else { alert("请先选择或拖入 MIDI 文件。"); return }
        var snapshot = options
        snapshot.singer = singer; snapshot.skipDrums = false
        let output = folder.appendingPathComponent("vocal.svp")
        save()
        busy = true; status = "正在量化音符、整理声部…"
        Task {
            do {
                let report = try await Task.detached(priority: .userInitiated) {
                    try Converter.convert(input: input, output: output, options: snapshot)
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
        VStack(spacing: 0) {
            ScrollView {
                VStack(alignment: .leading, spacing: 16) {
                    HStack(spacing: 14) {
                        Image(nsImage: NSApplication.shared.applicationIconImage).resizable().frame(width: 54, height: 54)
                        VStack(alignment: .leading, spacing: 4) { Text("Vocal Grid").font(.system(size: 27, weight: .bold, design: .rounded)); Text("MIDI → Synthesizer V · 自选参数 · 本机记忆").foregroundStyle(.secondary) }
                    }
                    card {
                        Text("01  导入 MIDI").font(.headline)
                        Text("拖入 MIDI 或文件夹，多个文件会显示选择列表").font(.callout).foregroundStyle(.secondary)
                        HStack {
                            Text(model.input?.lastPathComponent ?? "尚未选择 MIDI").lineLimit(1).truncationMode(.middle).frame(maxWidth: .infinity, alignment: .leading).help(model.input?.path ?? "")
                            Button("选择文件", action: model.chooseInput)
                        }
                    }.overlay(RoundedRectangle(cornerRadius: 13).stroke(inputTargeted ? accent : .clear, lineWidth: 2))
                     .onDrop(of: [UTType.fileURL.identifier], isTargeted: $inputTargeted) { model.receive($0, saving: false) }
                    card {
                        Text("02  节奏、歌声与音阶").font(.headline)
                        HStack(alignment: .top, spacing: 18) {
                            field("BPM") { TextField("120", value: $model.options.bpm, format: .number).textFieldStyle(.roundedBorder) }.frame(width: 130)
                            field("拍号") { HStack { Picker("分子", selection: $model.options.numerator) { ForEach(1...64, id: \.self) { Text("\($0)").tag($0) } }.labelsHidden(); Text("/"); Picker("分母", selection: $model.options.denominator) { ForEach([1,2,4,8,16,32,64], id: \.self) { Text("\($0)").tag($0) } }.labelsHidden() } }
                            field("默认歌词") { TextField("啦", text: $model.options.lyric).textFieldStyle(.roundedBorder) }.frame(width: 130)
                        }
                        field("歌姬：工程内声库名称 | 代际（1 或 2）") { TextField("Mai | 1", text: $model.singer).textFieldStyle(.roundedBorder) }
                        Text("例：Mai | 1、Mai 2 | 2。星尘请填本机工程中的准确声库名 | 代际。").font(.caption).foregroundStyle(.secondary)
                        HStack {
                            Toggle("对齐音符", isOn: $model.options.alignNotes)
                            Spacer()
                            Picker("网格 1/", selection: $model.options.gridDivision) { ForEach([4,8,16,32,64], id: \.self) { Text("\($0)").tag($0) } }.frame(width: 200).disabled(!model.options.alignNotes)
                        }
                        HStack {
                            Toggle("设置调式音阶", isOn: $model.options.scaleEnabled)
                            Spacer()
                            Picker("主音", selection: $model.options.scaleRoot) { ForEach(["C","C#","D","D#","E","F","F#","G","G#","A","A#","B"], id: \.self) { Text($0).tag($0) } }.labelsHidden().frame(width: 90).disabled(!model.options.scaleEnabled)
                            Picker("调式", selection: $model.options.scaleType) { Text("大调 Major").tag("Major"); Text("小调 Minor").tag("Minor") }.labelsHidden().frame(width: 160).disabled(!model.options.scaleEnabled)
                        }
                    }.toggleStyle(.checkbox)
                    card {
                        Text("03  音频导出设置").font(.headline)
                        HStack(alignment: .top) {
                            field("输出内容") { HStack { Toggle("分轨", isOn: $model.options.exportStems); Toggle("混音", isOn: $model.options.exportMixDown) } }
                            field("声道") { Picker("声道", selection: $model.options.channels) { Text("单声道").tag(1); Text("立体声").tag(2) }.labelsHidden() }
                            field("气声输出") { Picker("气声输出", selection: $model.options.aspiration) { Text("无").tag("noAspiration"); Text("增加文件输出气声").tag("asFiles") }.labelsHidden() }
                        }
                        HStack {
                            field("位深") { Picker("位深", selection: $model.options.bitDepth) { ForEach([16,24,32], id: \.self) { Text("\($0) 位").tag($0) } }.labelsHidden() }
                            field("采样率") { Picker("采样率", selection: $model.options.sampleRate) { ForEach([44100,48000,88200,96000], id: \.self) { Text("\($0) Hz").tag($0) } }.labelsHidden() }
                        }
                        HStack { Toggle("旁通增益和声像", isOn: $model.options.bypassGainPan); Spacer(); Toggle("旁通效果", isOn: $model.options.bypassEffects) }
                    }.toggleStyle(.checkbox)
                    card {
                        HStack { Text("04  保存到").font(.headline); Spacer(); Toggle("跟随 MIDI 输入目录", isOn: $model.followInput).toggleStyle(.checkbox) }
                        HStack {
                            Text(model.folder.path).lineLimit(1).truncationMode(.middle).frame(maxWidth: .infinity, alignment: .leading).help(model.folder.path)
                            Button("选择目录", action: model.chooseFolder)
                        }
                        Text("vocal.svp · 已有工程自动备份 · 可拖入目标文件夹").font(.caption).foregroundStyle(.secondary)
                    }.overlay(RoundedRectangle(cornerRadius: 13).stroke(outputTargeted ? accent : .clear, lineWidth: 2))
                     .onDrop(of: [UTType.fileURL.identifier], isTargeted: $outputTargeted) { model.receive($0, saving: true) }
                }.padding(24).disabled(model.busy)
            }
            Divider()
            HStack {
                VStack(alignment: .leading, spacing: 5) { Text(model.status).font(.callout).foregroundStyle(.secondary).lineLimit(2); Link("GitHub @Yukinsnow", destination: URL(string: "https://github.com/yukinsnow/vocal-grid")!).font(.caption) }
                Spacer()
                if model.busy { ProgressView().controlSize(.small) }
                Button(action: model.convert) { Text(model.busy ? "正在生成…" : "生成 vocal.svp  →").padding(.horizontal, 15).padding(.vertical, 6) }.buttonStyle(.borderedProminent).keyboardShortcut(.return, modifiers: .command).disabled(model.busy)
            }.padding(20)
        }.frame(width: 720, height: 790).tint(accent).controlSize(.large).background(Color(nsColor: .windowBackgroundColor))
        .alert("Vocal Grid", isPresented: $model.showMessage) { Button("好", role: .cancel) {} } message: { Text(model.message) }
        .sheet(isPresented: $model.showCandidates) {
            VStack(alignment: .leading, spacing: 16) {
                Text("选择要转换的 MIDI").font(.title2).bold()
                List(model.candidates, id: \.self, selection: $model.candidate) { url in VStack(alignment: .leading) { Text(url.lastPathComponent); Text(url.deletingLastPathComponent().path).font(.caption).foregroundStyle(.secondary) }.tag(url) }.frame(height: 250)
                HStack { Spacer(); Button("取消") { model.showCandidates = false }; Button("使用此文件") { model.input = model.candidate; model.showCandidates = false }.disabled(model.candidate == nil).keyboardShortcut(.defaultAction) }
            }.padding(24).frame(width: 590)
        }
    }
    private func card<Content: View>(@ViewBuilder _ content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 13, content: content).padding(18).frame(maxWidth: .infinity, alignment: .leading).background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 13)).overlay(RoundedRectangle(cornerRadius: 13).stroke(.primary.opacity(0.06)))
    }
    private func field<Content: View>(_ title: String, @ViewBuilder _ content: () -> Content) -> some View { VStack(alignment: .leading, spacing: 7) { Text(title).font(.callout).foregroundStyle(.secondary); content() } }
}