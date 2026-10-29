# Vocal Grid for macOS

[English documentation](../README.md) · [简体中文说明](../README.zh-CN.md)

Native SwiftUI / AppKit app, requiring macOS 13 or later. No third-party packages or web runtime.

Build from the repository root:

```bash
bash macos/build-macos.sh --universal
```

Requires Xcode 15+ or Command Line Tools with Swift 5.9+. The script runs converter tests, compiles both Apple Silicon and Intel architectures, creates the ICNS icon, packages `macos/dist/Vocal Grid.app`, and applies a local ad-hoc signature.

The private [GitHub Actions workflow](https://github.com/yukinsnow/vocal-grid/actions/workflows/build.yml) performs this build in the cloud and uploads a ZIP artifact. It does not publish releases or change repository visibility. See the latest workflow run for the actual build status.

The app is not Developer ID signed or notarized. Automated build success does not validate Synthesizer V voice installation, activation, or rendered audio. Install and activate the selected singer in Synthesizer V Studio 2 before opening generated projects.

默认 Mai 一代，支持手填歌姬代号、文件和目录拖放、32 分音符量化，以及预设的普通话干声分轨导出选项。完整中文使用方法、导出参数与限制见上方中文说明。