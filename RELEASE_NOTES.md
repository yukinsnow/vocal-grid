## 简体中文

Vocal Grid v1.1：修复 SVP 声库与小调设置，并新增多 MIDI 合并。

- 多选、追加或拖入多个 MIDI / 文件夹，同起点合并为一个 vocal.svp；按各自 PPQ 换算，轨道连续编号，默认输出到首个输入目录。
- 声库输入直接对应 database.name：默认 Mai，二代 Mai 填 Mai 2。支持从已有 SVP 读取完整声库标记或粘贴 database JSON，移除臆测版本号。
- 根据原始公开工程核实星尘 Infinity 一代名称 MEDIUM5·Stardust；二代标记请从本机已正确设置的 SVP 读取。
- 自然小调实际写入 NaturalMinor，修复被 SV2 回退为大调的问题。
- 明确量化音符与 SV2 本机工具栏网格的区别。4/8/16/32/64 量化及关闭量化均有回归测试。
- 紧凑双栏主界面，所有设置一屏可见，无滚动条；设置继续保存在本机。

Windows 下载 VocalGrid-Windows.exe；macOS 13+ 下载 VocalGrid-macOS-universal.zip（Apple Silicon + Intel，尚未 Apple 公证）。仓库保持私有。云端构建和自动测试通过后发布；未验证未安装声库的实际音频渲染。

## English

- Merge multiple MIDI files at a common project origin, normalize each file's PPQ, retain separate lanes and number tracks continuously. Output follows the first input folder.
- Direct database.name input (Mai / Mai 2), complete database JSON input, and Read from SVP for exact engine/version metadata.
- Correct NaturalMinor serialization; remove speculative voice version numbers.
- Distinguish note quantization from SV2's locally stored toolbar grid.
- Compact two-column native UI without scrolling, with persistent local preferences.

Windows executable and macOS 13+ universal ZIP are attached with SHA-256 checksums. The repository remains private. The Mac app is ad-hoc signed, not Apple notarized. Actual rendering with unavailable voicebanks has not been verified.
