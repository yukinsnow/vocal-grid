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

- 可独立开启/关闭音符对齐。网格支持 4/8/16/32/64 分音符，默认 32；关闭时保留 MIDI 的原始时间位置。
- 使用用户输入的固定 BPM、拍号，覆盖 MIDI 原有速度和拍号变化，保留音符的音乐节拍位置。
- 输出轨优先使用 MIDI 文件名（去扩展名），重名时追加数字（如 `Piano2`、`Piano3`）；不同 MIDI 通道分开，复音自动拆成独立单声部，保留和弦中的音符。
- 默认 **Mai 一代**。文本输入直接对应 SVP 的 `mainRef.database.name`：`Mai`、`Mai 2`。可粘贴完整 `database` JSON，或点击“从 SVP 读取”，保留真实的声库引擎和版本。代号不是激活码或商品 UUID。
- 可选调式音阶：默认 C 大调，支持 12 个主音的自然大调/自然小调（SVP 字段 `NaturalMinor`），也可关闭。只写入工程音阶信息，不修改音符音高。
- 节奏、声库、量化、音阶、导出参数自动保存在本机，下次启动恢复。
- 全部音符设置汉语普通话；保留 MIDI 中的歌词事件，没有歌词时默认填写“啦”，可自行修改。
- 支持多选、追加或拖入多个 MIDI / 目录（只读取目录当前层级），合并生成一个 SVP。各文件从工程同一起点开始，按各自 PPQ 换算拍数，保留源文件的独立声部，轨道连续编号。默认输出到首个输入文件的目录。
- 支持把目标文件夹拖入保存区域，输出文件名固定为 `vocal.svp`。
- 已有工程自动备份后再替换，不修改原 MIDI。

## 可配置的音频导出选项（首次启动默认值）

| 选项 | 设置 |
| --- | --- |
| 导出内容 | 仅分轨，全部生成轨启用，无混音输出 |
| 旁通增益和声像 | 开启 |
| 旁通效果 | 开启 |
| 声道 | 默认立体声，可选单声道 |
| 气声输出 | 默认无，可选增加文件输出气声（`asFiles`） |
| 位深 / 采样率 | 默认 24 位 / 48000 Hz；可选 16/24/32 位及 44100/48000/88200/96000 Hz |
| 音频文件名前缀 | `vocal` |
| 音频保存目录 | 与生成的工程相同 |

导出内容支持仅分轨、仅混音、分轨加混音；两个旁通开关独立可配置，默认均开启。“气声输出：无”指编辑器中的导出选项；转换工具本身不额外处理音频。

## 下载

正式版本请进入 [Releases](https://github.com/yukinsnow/vocal-grid/releases)，包含 Windows 程序、Mac 通用版 ZIP 和 SHA-256 校验文件。仓库及 Release 已公开，下载无需仓库访问权限。

### 私有 CI 构建产物

进入仓库 **Actions → Build native apps → 成功的构建记录 → Artifacts**。需要登录 GitHub，并具有本私有仓库的访问权限。产物保留 14 天；常规构建不发布 Release，也不会修改仓库可见性。手动执行时填写版本标签，可将通过测试的程序发布到私有 GitHub Release。

- `VocalGrid-macOS-universal`：包含装有 `Vocal Grid.app` 的 ZIP 和 SHA-256 校验文件。先解压 GitHub 下载的产物包，再解压里面的应用 ZIP。
- `VocalGrid-Windows`：包含 `MidiToVocal.exe`。

macOS 应用使用本地临时签名，尚未进行 Developer ID 签名及 Apple 公证。云构建会检查编译、核心测试、应用包结构、签名完整性和双架构；不会验证 Synthesizer V 声库激活状态或实际音频输出。

## 使用

1. 打开应用，多选或拖入 MIDI；可继续添加文件，或清空后重新选择。
2. 输入 BPM（四分音符每分钟）、拍号，可修改默认歌词。
3. 默认输入 `Mai`；Mai 二代填 `Mai 2`。特殊声库建议点击“从 SVP 读取”，选择在 SV2 已正确设置声库并保存的工程。完整标记也可直接粘贴到文本框。
4. 选择保存目录，生成 `vocal.svp`。
5. 用 Synthesizer V Studio 2 打开，等待合成后导出分轨。

对应歌姬需要在本机安装、激活，并支持汉语普通话。工具不在线核验手填代号，也无法从纯 MIDI 推断整首歌词。

默认输出到首个输入 MIDI 所在文件夹。手动选择或拖入其他输出目录时，会关闭“跟随 MIDI 输入目录”；该模式和手动目录也会记住。重新勾选可恢复跟随。旧工程保留为带时间戳的 `.bak`。

Windows 本机设置位于 `%LOCALAPPDATA%/VocalGrid/settings.json`；Mac 使用 `local.vocalgrid.macos` 的本机 UserDefaults。不会上传或联网同步这些设置。Windows 已移除旧自绘弹出菜单，普通选项改用系统原生控件，歌姬及代际改为文本输入；异常会给出提示并记录本机日志，避免无提示关闭。

应用页脚标注 **GitHub @Yukinsnow**。

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

支持 PPQ 时间制的标准 MIDI 格式 0/1、running status、零力度 Note Off，以及 UTF-8/简体中文旧编码歌词。保留所有 MIDI 通道，包括通道 10，不再提供跳过打击乐选项。暂不映射延音踏板、弯音、力度曲线、音色、混音或其他 CC；SMPTE 时间制和格式 2 会明确报错。

## 目录

```text
windows/       Windows 原生界面与转换核心
macos/         Swift 包、Mac 原生界面、转换核心、测试、打包脚本
assets/        共用图标与 Windows 资源
tests/windows/ Windows 转换测试和原生拖放事件检查
.github/       私有产物构建工作流
```

这是独立工具，与 Dreamtonics 无隶属关系。仓库不包含专有声库、激活凭据或用户 MIDI / 工程文件。

## v1.1 格式修复与边界

- 主界面为紧凑双栏，一屏展示全部设置，不使用滚动条。
- 修复自然小调写成 `Minor` 后被 SV2 回退为大调的问题，实际输出 `NaturalMinor`。
- 移除臆测的二代声库版本 `201`。直接名称模式只为默认 Mai 设置已验证的版本；其他声库交由编辑器匹配。若匹配有差异，请从已正确设置的 SVP 读取完整标记。
- “量化音符”实际更改输出音符的 onset / duration。SV2 工具栏的编辑网格属于 SV2 本机设置，不是 SVP 工程字段；打开工程后显示 32 分不代表转换量化失效。工具不会改写 SV2 全局偏好。
- 从 [shibobiiiiiii 公开配布的原始 SVP](https://www.bilibili.com/video/BV13L4y1g7s2/) 核对到星尘 Infinity 一代 AI 的准确名称为 **`MEDIUM5·Stardust`**（中间是 `·`），引擎 `SVR2AI`，原生语言 `mandarin`。该样本不是二代证据；未确认二代名称，不将它猜写为 `Stardust 2`。二代请用“从 SVP 读取”。仅记录格式字段，不随程序分发第三方乐谱。
- 选择自然大调/自然小调只设置音阶标记，不把旋律转调，也不改音高。

## v1.1.1 界面与下载

从 SVP 读取后输入框只显示声库名称，完整引擎及版本信息在内部保留并随本机设置保存；手动改名后使用新名称。已移除下方示例说明，默认 Mai 一代。界面调式名称为自然大调 / 自然小调。Windows Release 下载 VocalGrid-Windows.zip，解压后运行 VocalGrid-Windows.exe。


## v1.1.3 轨道命名

使用 MIDI 文件名（不含扩展名）作为轨道名。同一文件拆出的多个声部及不同目录里的同名 MIDI 会在名称后追加数字，从 2 开始；按不区分大小写去重，并避开其他输入文件的原名。例如 Piano.mid、另一个 Piano.mid、Piano2.mid 会得到 Piano、Piano3、Piano2。内部 MIDI Track Name 事件不作为命名来源。
