# Vocal Grid · macOS 原生版源码

使用 SwiftUI / AppKit，最低 macOS 13。无第三方依赖，不使用 Electron、浏览器运行时或 .NET。

**交付状态：这是源码与构建包，不是已编译的 Mac 应用。当前在 Windows 环境制作，未完成 macOS 编译、界面运行或 SV2 出音验证。请在 Mac 上执行下述脚本；脚本先运行测试，测试与编译均成功后才生成 `.app`。**

## 在 Mac 上构建

需要 Xcode 15+ 或包含 Swift 5.9+ 的 Apple Command Line Tools。仅构建时需要开发工具；生成后的应用不需要这些工具。

在终端进入本文件所在目录，执行：

```bash
bash build-macos.sh
```

生成 `dist/Vocal Grid.app`，架构与执行构建的 Mac 相同。需要同时支持 Apple Silicon 和 Intel 时，执行：

```bash
bash build-macos.sh --universal
```

脚本会运行 `swift test`、编译应用、生成 ICNS 图标、组装 `.app` 并临时签名，最后检查签名和 Info.plist。未购买 Apple Developer ID，也未做 Apple 公证；本地构建的临时签名与正式对外分发签名不同。

也可用 Xcode 打开 `Package.swift` 阅读、调试源码。

## 使用

1. 将生成的 `Vocal Grid.app` 移到希望放置它的文件夹，双击运行。
2. 选择 MIDI，或把 MIDI / 包含 MIDI 的文件夹拖入导入区域。多个 MIDI 会列出供选择，每次转换一个，只扫描文件夹当前层级。
3. 输入 BPM、拍号。默认歌姬代号 `Mai`，代际为一代；其他声库需手动填写准确名称并选择对应代际。
4. 选择输出目录，也可将文件夹拖入保存区域。默认保存到 `.app` 所在文件夹；若该位置不可写，请选择有写入权限的目录。
5. 生成 `vocal.svp`，用 Mac 版 Synthesizer V Studio 2 打开，等待合成后导出。

支持浅色/深色模式、Retina 显示、原生文件选择器。转换在后台运行。快捷键 `⌘ Return` 开始生成。

## 与 Windows 版相同的转换设置

- 音符起点和终点量化到最近的 32 分音符，最短一个网格。
- 使用输入的固定 BPM、拍号，覆盖原 MIDI 的速度/拍号变化，保留音乐节拍位置。
- 所有输出轨命名为 Melody1、Melody2……；不同通道分开，复音拆成多个单声部。
- 默认 Mai 一代，可手动填写其他歌姬代号。代号是工程里的 `mainRef.database.name`，不是激活序列号或商品 UUID。声库需要支持普通话并在本机安装、激活。
- 所有音符使用汉语普通话；有 MIDI 歌词时保留（UTF-8/GB18030），没有时默认“啦”。
- 仅分轨，开启“旁通增益和声像”“旁通效果”两个开关，立体声、气声输出“无”、24 位、48000 Hz。
- 生成文件固定为 `vocal.svp`，音频导出前缀 `vocal`，导出目录与工程目录相同。
- 已有工程先复制为带时间戳的 `.bak`，再原子写入新工程；不修改原 MIDI。
- 支持 PPQ 制 MIDI 格式 0、1，默认跳过通道 10 打击乐。忽略延音踏板、弯音、力度和其他 CC；不支持 SMPTE 和格式 2。

## 测试与资源

`Tests/VocalGridCoreTests` 包含 MIDI 解析、复音、量化、命名、歌姬设置、两个旁通开关、输出格式、歌词、备份、目录识别和损坏数据处理测试。它们需要在 Mac 上实际执行；此交付不声称测试已经通过。

`Assets/VocalGrid.svg` 为图标矢量原稿；`Assets/VocalGrid.png` 为构建时生成 ICNS 的图像源。应用图标、系统菜单、窗口和文件对话框均由 macOS 原生机制提供。

参考：[Apple SwiftUI 拖放接口](https://developer.apple.com/documentation/swiftui/view/ondrop(of:istargeted:perform:)-6lin8)、[Swift 官方构建说明](https://www.swift.org/documentation/server/guides/building.html)。
