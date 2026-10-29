## 简体中文

Vocal Grid 首个版本：轻量原生 MIDI → Synthesizer V Studio 2 工程转换工具。

- Windows 原生 WinForms 界面；macOS 原生 SwiftUI 界面，支持 Apple Silicon 与 Intel。
- 支持文件/目录拖放，输入 BPM 和拍号，音符对齐 32 分音符网格。
- 默认 Mai 一代，可手动填写其他歌姬代号；全部音符设置为汉语普通话。
- 输出 `vocal.svp`，轨道依次命名为 Melody1、Melody2……，复音自动拆分声部。
- 仅分轨，开启“旁通增益和声像”“旁通效果”，立体声、气声输出无、24 位、48000 Hz。
- 已有工程自动备份，不修改源 MIDI。

### 下载与使用

- **Windows**：下载 `VocalGrid-Windows.exe`，直接运行。需要系统带有 .NET Framework 4.x。
- **macOS 13+**：下载 `VocalGrid-macOS-universal.zip`，解压后打开 `Vocal Grid.app`。同时支持 Apple Silicon 和 Intel；应用使用临时签名，尚未完成 Apple Developer ID 签名与公证。
- `SHA256SUMS.txt` 提供两个程序包的 SHA-256 校验值。

需要在本机安装并激活所选歌姬，且声库支持普通话。云端已执行构建与自动测试；尚未验证实际用户设备上的全部交互和 Synthesizer V 音频渲染。当前仓库和 Release 均仅对有权限的用户开放。

## English

The first release of Vocal Grid, a lightweight native MIDI-to-Synthesizer V Studio 2 project converter.

- Native WinForms UI on Windows and SwiftUI UI on macOS, with Apple Silicon and Intel support.
- Drag and drop MIDI files or folders, set BPM and meter, and quantize notes to a 1/32-note grid.
- First-generation Mai by default, with manually entered singer database names; Mandarin on all notes.
- Generate `vocal.svp` with numbered Melody tracks and automatic polyphonic voice splitting.
- Stems only, bypass gain/pan and effects, stereo, aspiration output None, 24-bit / 48 kHz.
- Existing projects are backed up; source MIDI files are never modified.

### Downloads

- **Windows**: run `VocalGrid-Windows.exe`. Requires .NET Framework 4.x.
- **macOS 13+**: extract `VocalGrid-macOS-universal.zip` and open `Vocal Grid.app`. The universal binary supports Apple Silicon and Intel. The app is ad-hoc signed, not Developer ID signed or Apple notarized.
- Verify the downloads with `SHA256SUMS.txt`.

Install and activate the selected singer locally; Mandarin support is required. Cloud builds and automated tests are run before publishing. Full user-device interaction and Synthesizer V audio rendering have not been verified. This release remains within the private repository's access controls.
