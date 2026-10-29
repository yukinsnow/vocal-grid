# Vocal Grid

简体中文 · [English](README.md)

<img src="assets/VocalGrid.png" width="88" alt="Vocal Grid：MIDI 音符转换为人声波形图标">

轻量的原生 MIDI → Synthesizer V Studio 2 工程转换工具。导入 MIDI，输入 BPM、拍号，即可生成完成量化并预设干声分轨导出选项的 `vocal.svp`。

## 平台

| 平台 | 原生技术 | 运行要求 |
| --- | --- | --- |
| Windows | C# / WinForms | Windows，带 .NET Framework 4.x |
| macOS | SwiftUI / AppKit | macOS 13+，支持 Apple Silicon 和 Intel |

不依赖 Electron、浏览器运行时或第三方 MIDI 库。程序不包含歌姬声库或合成引擎，实际音频由 Synthesizer V 打开工程后生成。

## 功能

- 音符起点、终点对齐最近的 32 分音符网格，最短时值为一个网格。
- 使用用户输入的固定 BPM、拍号，覆盖 MIDI 原有速度和拍号变化，保留音符的音乐节拍位置。
- 输出轨命名为 `Melody1`、`Melody2`……；不同 MIDI 通道分开，复音自动拆成独立单声部，保留和弦中的音符。
- 默认 **Mai 一代**；也可手动输入其他歌姬在工程中的准确声库名称，并选择一代或二代 AI 音源。代号不是激活码或商品 UUID。
- 全部音符设置汉语普通话；保留 MIDI 中的歌词事件，没有歌词时默认填写“啦”，可自行修改。
- 支持拖入 MIDI 文件或包含 MIDI 的目录；发现多个 MIDI 时弹出列表选择一个，只读取当前层级。
- 支持把目标文件夹拖入保存区域，输出文件名固定为 `vocal.svp`。
- 已有工程自动备份后再替换，不修改原 MIDI。

## 干声导出预设

| 选项 | 设置 |
| --- | --- |
| 导出内容 | 仅分轨，全部生成轨启用，无混音输出 |
| 旁通增益和声像 | 开启 |
| 旁通效果 | 开启 |
| 声道 | 立体声 |
| 气声输出 | 无（`noAspiration`） |
| 位深 / 采样率 | 24 位 / 48000 Hz |
| 音频文件名前缀 | `vocal` |
| 音频保存目录 | 与生成的工程相同 |

两个旁通开关均开启。“气声输出：无”指编辑器中的导出选项；转换工具本身不额外处理音频。

## 下载私有云构建产物

进入仓库 **Actions → Build native apps → 成功的构建记录 → Artifacts**。需要登录 GitHub，并具有本私有仓库的访问权限。产物保留 14 天；工作流不会发布公开 Release，也不会修改仓库可见性。

- `VocalGrid-macOS-universal`：包含装有 `Vocal Grid.app` 的 ZIP 和 SHA-256 校验文件。先解压 GitHub 下载的产物包，再解压里面的应用 ZIP。
- `VocalGrid-Windows`：包含 `MidiToVocal.exe`。

macOS 应用使用本地临时签名，尚未进行 Developer ID 签名及 Apple 公证。云构建会检查编译、核心测试、应用包结构、签名完整性和双架构；不会验证 Synthesizer V 声库激活状态或实际音频输出。

## 使用

1. 打开应用，选择或拖入 MIDI。
2. 输入 BPM（四分音符每分钟）、拍号，可修改默认歌词。
3. 保持 `Mai` + 一代，或输入其他歌姬的准确工程名称和音源代际。
4. 选择保存目录，生成 `vocal.svp`。
5. 用 Synthesizer V Studio 2 打开，等待合成后导出分轨。

对应歌姬需要在本机安装、激活，并支持汉语普通话。工具不在线核验手填代号，也无法从纯 MIDI 推断整首歌词。

Windows 默认保存到 EXE 所在目录，macOS 默认保存到 `.app` 所在目录。程序位于受保护或只读位置时，请选择有写入权限的文件夹。旧工程保留为带时间戳的 `.bak`。

## 本机构建

### Windows

在仓库根目录的 PowerShell 中运行：

```powershell
./windows/build.ps1
./tests/windows/verify.ps1
```

生成 `dist/windows/MidiToVocal.exe`。使用 Windows 自带的 .NET Framework C# 编译器，无需额外 SDK。

### macOS

安装 Xcode 15+，或带 Swift 5.9+ 的 Command Line Tools：

```bash
bash macos/build-macos.sh
# 同时构建 Apple Silicon 与 Intel：
bash macos/build-macos.sh --universal
```

生成 `macos/dist/Vocal Grid.app`。脚本先运行测试，再编译、生成图标并组装应用。也可用 Xcode 打开 `macos/Package.swift`。

## 云构建

修改应用源码、测试、图标或工作流后，GitHub Actions 自动构建；也可点击 **Run workflow** 手动执行。工作流测试并构建 Windows 和 macOS 版，生成 Mac 双架构包，上传私有产物。

使用标准 GitHub 托管运行器，消耗账号的私有仓库 Actions 额度，是否产生费用取决于账号额度与计费设置。不需要仓库密钥或 Apple 开发者证书。

## MIDI 范围

支持 PPQ 时间制的标准 MIDI 格式 0/1、running status、零力度 Note Off，以及 UTF-8/简体中文旧编码歌词。默认跳过通道 10 的打击乐。暂不映射延音踏板、弯音、力度曲线、音色、混音或其他 CC；SMPTE 时间制和格式 2 会明确报错。

## 目录

```text
windows/       Windows 原生界面与转换核心
macos/         Swift 包、Mac 原生界面、转换核心、测试、打包脚本
assets/        共用图标与 Windows 资源
tests/windows/ Windows 转换测试和原生拖放事件检查
.github/       私有产物构建工作流
```

这是独立工具，与 Dreamtonics 无隶属关系。仓库不包含专有声库、激活凭据或用户 MIDI / 工程文件。
