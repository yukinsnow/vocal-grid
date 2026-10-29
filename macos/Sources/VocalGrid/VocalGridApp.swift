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
    @Published var input: URL?
    @Published var folder: URL
    @Published var bpm = "120"
    @Published var numerator = 4
    @Published var denominator = 4
    @Published var singer = "Mai"
    @Published var generation2 = false
    @Published var lyric = "啦"
    @Published var skipDrums = true
    @Published var busy = false
    @Published var status = "就绪 · 已有工程会自动备份"
    @Published var message = ""
    @Published var showMessage = false
    @Published var candidates: [URL] = []
    @Published var candidate: URL?
    @Published var showCandidates = false
    init() {
        let bundle = Bundle.main.bundleURL
        folder = bundle.pathExtension == "app" ? bundle.deletingLastPathComponent() : URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
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
        if panel.runModal() == .OK, let url = panel.url { folder = url; status = "保存目录已更新" }
    }
    func accept(_ urls: [URL], saving: Bool) {
        guard !busy else { return }
        do {
            if saving {
                guard urls.count == 1, (try urls[0].resourceValues(forKeys: [.isDirectoryKey])).isDirectory == true else {
                    throw ConversionError.invalid("请在保存区域拖入一个文件夹。")
                }
                folder = urls[0]; status = "保存目录已更新"; return
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
        guard let tempo = Double(bpm.replacingOccurrences(of: ",", with: ".")) else { alert("请输入有效的 BPM。"); return }
        var options = ConversionOptions()
        options.bpm = tempo; options.numerator = numerator; options.denominator = denominator
        options.singer = singer; options.generation2 = generation2; options.lyric = lyric; options.skipDrums = skipDrums
        let snapshot = options, output = folder.appendingPathComponent("vocal.svp")
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
        VStack(alignment: .leading, spacing: 16) {
            HStack(spacing: 14) {
                Image(nsImage: NSApplication.shared.applicationIconImage).resizable().frame(width: 54, height: 54)
                VStack(alignment: .leading, spacing: 4) {
                    Text("Vocal Grid").font(.system(size: 27, weight: .bold, design: .rounded))
                    Text("把 MIDI 整理成可直接合成的人声工程").foregroundStyle(.secondary)
                }
                Spacer()
                Text("32 分音符").fontWeight(.semibold).foregroundStyle(accent)
            }.padding(.bottom, 5)
            card {
                Text("01  导入 MIDI").font(.headline)
                Text("拖入 .mid / .midi，也可拖入包含 MIDI 的文件夹").font(.callout).foregroundStyle(.secondary)
                HStack {
                    Text(model.input?.lastPathComponent ?? "尚未选择 MIDI 文件")
                        .foregroundStyle(model.input == nil ? .secondary : .primary)
                        .lineLimit(1).truncationMode(.middle)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(10).background(.quaternary.opacity(0.35), in: RoundedRectangle(cornerRadius: 7))
                        .help(model.input?.path ?? "拖入 MIDI 文件或文件夹")
                    Button("选择文件", action: model.chooseInput).controlSize(.large)
                }
            }
            .overlay(RoundedRectangle(cornerRadius: 13).stroke(inputTargeted ? accent : .clear, lineWidth: 2))
            .onDrop(of: [UTType.fileURL.identifier], isTargeted: $inputTargeted) { model.receive($0, saving: false) }
            card {
                Text("02  节奏与歌声").font(.headline)
                HStack(alignment: .top, spacing: 24) {
                    field("BPM") { TextField("120", text: $model.bpm).textFieldStyle(.roundedBorder) }.frame(width: 145)
                    field("拍号") {
                        HStack {
                            Picker("分子", selection: $model.numerator) { ForEach(1...64, id: \.self) { Text("\($0)").tag($0) } }.labelsHidden()
                            Text("/").foregroundStyle(.secondary)
                            Picker("分母", selection: $model.denominator) { ForEach([1, 2, 4, 8, 16, 32, 64], id: \.self) { Text("\($0)").tag($0) } }.labelsHidden()
                        }
                    }
                    field("默认歌词") { TextField("啦", text: $model.lyric).textFieldStyle(.roundedBorder) }.frame(width: 145)
                }
                HStack(alignment: .top, spacing: 24) {
                    field("歌姬代号") { TextField("Mai", text: $model.singer).textFieldStyle(.roundedBorder).help("填写工程中的准确声库名称，不是激活码。") }
                    field("音源代际") {
                        Picker("音源代际", selection: $model.generation2) { Text("一代 AI 音源").tag(false); Text("二代 AI 音源").tag(true) }.labelsHidden()
                    }.frame(width: 190)
                }
                Text("默认 Mai 一代 · 手动填写准确声库名称 · 汉语普通话").font(.caption).foregroundStyle(.secondary)
            }.controlSize(.large)
            card {
                HStack { Text("03  保存到").font(.headline); Spacer(); Text("拖入文件夹 · vocal.svp").font(.caption).foregroundStyle(.secondary) }
                HStack {
                    Text(model.folder.path).lineLimit(1).truncationMode(.middle)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(10).background(.quaternary.opacity(0.35), in: RoundedRectangle(cornerRadius: 7))
                        .help(model.folder.path)
                    Button("选择目录", action: model.chooseFolder).controlSize(.large)
                }
            }
            .overlay(RoundedRectangle(cornerRadius: 13).stroke(outputTargeted ? accent : .clear, lineWidth: 2))
            .onDrop(of: [UTType.fileURL.identifier], isTargeted: $outputTargeted) { model.receive($0, saving: true) }
            Toggle("跳过打击乐通道 10", isOn: $model.skipDrums).toggleStyle(.checkbox)
            VStack(alignment: .leading, spacing: 5) {
                Text("仅分轨 · 立体声 · 无气声 · 24 位 / 48 kHz")
                Text("已开启：旁通增益和声像、旁通效果")
            }.font(.caption).foregroundStyle(.secondary)
            HStack {
                Text(model.status).font(.callout).foregroundStyle(.secondary).lineLimit(2)
                Spacer()
                if model.busy { ProgressView().controlSize(.small) }
                Button(action: model.convert) { Text(model.busy ? "正在生成…" : "生成 vocal.svp  →").padding(.horizontal, 20).padding(.vertical, 7) }
                    .buttonStyle(.borderedProminent).controlSize(.large).keyboardShortcut(.return, modifiers: .command)
            }.padding(.top, 4)
        }
        .padding(26).frame(width: 720).tint(accent)
        .background(Color(nsColor: .windowBackgroundColor))
        .disabled(model.busy)
        .alert("Vocal Grid", isPresented: $model.showMessage) { Button("好", role: .cancel) {} } message: { Text(model.message) }
        .sheet(isPresented: $model.showCandidates) {
            VStack(alignment: .leading, spacing: 16) {
                Text("选择要转换的 MIDI").font(.title2).bold()
                Text("每次转换一个文件；只读取文件夹当前层级。").foregroundStyle(.secondary)
                List(model.candidates, id: \.self, selection: $model.candidate) { url in
                    VStack(alignment: .leading, spacing: 3) { Text(url.lastPathComponent); Text(url.deletingLastPathComponent().path).font(.caption).foregroundStyle(.secondary) }.tag(url)
                }.frame(height: 250)
                HStack { Spacer(); Button("取消") { model.showCandidates = false }; Button("使用此文件") { model.input = model.candidate; model.status = "MIDI 已载入"; model.showCandidates = false }.disabled(model.candidate == nil).keyboardShortcut(.defaultAction) }
            }.padding(24).frame(width: 590)
        }
    }
    private func card<Content: View>(@ViewBuilder _ content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 13, content: content).padding(18).frame(maxWidth: .infinity, alignment: .leading)
            .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 13))
            .overlay(RoundedRectangle(cornerRadius: 13).stroke(.primary.opacity(0.06)))
    }
    private func field<Content: View>(_ title: String, @ViewBuilder _ content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 7) { Text(title).font(.callout).foregroundStyle(.secondary); content() }
    }
}
