# Vocal Grid

[简体中文](README.zh-CN.md) · English

<img src="assets/VocalGrid.png" width="88" alt="Vocal Grid icon: MIDI notes becoming a vocal waveform">

A lightweight, native MIDI-to-Synthesizer V Studio 2 project converter. Import a MIDI file, enter the tempo and time signature, and generate `vocal.svp` with quantized notes and dry stem export settings.

## Native apps

| Platform | Implementation | Requirements |
| --- | --- | --- |
| Windows | C# / WinForms | Windows with .NET Framework 4.x |
| macOS | SwiftUI / AppKit | macOS 13 or later; Apple Silicon or Intel |

No Electron, web runtime, third-party MIDI parser, voice database, or singing synthesis engine is bundled. Synthesizer V performs the actual audio rendering after you open the generated project.

## Features

- Optionally quantize note starts and ends to 1/4, 1/8, 1/16, 1/32, or 1/64 notes (default 1/32). Turn alignment off to preserve MIDI timing.
- Use a fixed BPM and time signature supplied by the user, replacing MIDI tempo/meter changes while preserving musical beat positions.
- Name output tracks after the MIDI filename without its extension; add numeric suffixes only for duplicates. Split MIDI channels and overlapping notes into separate monophonic voices without discarding chord notes.
- Optional project scale metadata: C major by default, any chromatic root with major/minor, or disable it. This does not transpose or force notes into the scale.
- Remember rhythm, singer, grid, scale and export choices locally between launches.
- Set Mandarin Chinese on all notes. Preserve MIDI lyric events when available; otherwise use the editable default lyric `啦`.
- Select or drop multiple MIDI files or folders and merge them into one SVP. Folder discovery is nonrecursive.
- Drag a folder onto the output section to choose where to save `vocal.svp`.
- Back up an existing project before writing its replacement. Original MIDI files are never modified.

## Configurable export options (initial defaults)

| Option | Value |
| --- | --- |
| Output | Stems only; all generated tracks selected; no mixdown |
| Bypass gain and pan | On |
| Bypass effects | On |
| Channels | Stereo (mono also available) |
| Aspiration output | None (`noAspiration`); optional additional aspiration files (`asFiles`) |
| Bit depth / sample rate | 24-bit / 48,000 Hz; selectable 16/24/32-bit and 44,100/48,000/88,200/96,000 Hz |
| Audio name prefix | `vocal` |
| Export destination | Same folder as the generated project |

Export content supports stems only, mixdown only, or both. The two bypass switches are independently configurable and initially enabled. “Aspiration: None” refers to the editor's export option; the converter does not apply additional audio processing.

## Downloads

For versioned packages, open [Releases](https://github.com/yukinsnow/vocal-grid/releases). Public releases contain Windows and universal macOS ZIPs plus SHA-256 checksums.

### CI artifacts

Open **Actions → Build native apps → a successful run → Artifacts**. Repository access and a GitHub login are required. Artifacts expire after 14 days. Regular builds do not publish GitHub Releases or change repository visibility. A manual run with a release tag publishes tested binaries to a GitHub Release.

- `VocalGrid-macOS-universal`: contains a ZIP with `Vocal Grid.app` and a SHA-256 checksum. Extract the artifact download, then extract the inner app ZIP.
- `VocalGrid-Windows`: contains `MidiToVocal.exe`.

The macOS package is locally ad-hoc signed. It is not Developer ID signed or notarized. Cloud compilation checks the application build, core tests, bundle structure, signature integrity, and both CPU architectures; it does not verify Synthesizer V voice activation or audio output.

## Use

1. Open the app and select or drop a MIDI file.
2. Enter BPM (quarter notes per minute), time signature, and optional default lyric.
4. Choose the destination folder, then generate `vocal.svp`.
5. Open it in Synthesizer V Studio 2, wait for synthesis, then export the selected stems.

The singer must be installed and activated locally and support Mandarin. Singer names are not validated online. The converter cannot infer missing song lyrics from MIDI notes.

The default output folder follows each selected MIDI's input folder. Selecting or dropping a different output folder disables this mode; the mode and manual output folder are remembered. Re-enable **Follow MIDI input folder** to restore it. Existing projects receive timestamped `.bak` backups.

Windows preferences: `%LOCALAPPDATA%/VocalGrid/settings.json`. macOS preferences: standard local UserDefaults for bundle `local.vocalgrid.macos`. These settings are not uploaded or synced. The Windows custom popup control has been removed; ordinary choices use native controls, and singer/generation use a text field. Unexpected UI exceptions are reported with a local error log instead of silently closing.

Authorship is shown in the app as **GitHub @Yukinsnow**.

## Build locally

### Windows

From the repository root in PowerShell:

```powershell
./windows/build.ps1
./tests/windows/verify.ps1
```

Output: `dist/windows/MidiToVocal.exe`. The Windows installation's .NET Framework C# compiler is used. No extra SDK is required.

### macOS

Install Xcode 15+ or Command Line Tools with Swift 5.9+, then run:

```bash
bash macos/build-macos.sh
# Or build both CPU architectures:
bash macos/build-macos.sh --universal
```

Output: `macos/dist/Vocal Grid.app`. The script runs the test suite before compiling and packaging. You can also open `macos/Package.swift` in Xcode.

## Cloud builds

The GitHub Actions workflow runs on changes to app sources, tests, assets, or the workflow itself. It can also be triggered manually with **Run workflow**. It tests and builds both native apps, packages a universal macOS app, and uploads build artifacts. It uses standard GitHub-hosted runners; usage is subject to the account's private Actions allowance and billing settings. No secrets or Apple developer certificates are required.

## MIDI scope

Supports standard MIDI format 0/1 with PPQ timing, running status, velocity-zero Note Off, and UTF-8/Chinese legacy lyric decoding. All MIDI channels, including channel 10, are preserved; there is no percussion-skip option. Sustain pedal, pitch bends, velocity shaping, instrument patches, mixer settings, and other CC events are not mapped to singing parameters. SMPTE timing and MIDI format 2 are rejected with an error.

## Project structure

```text
windows/       Native Windows UI and converter
macos/         Swift package, native Mac UI, converter, tests, packaging
assets/        Shared icon artwork and Windows app resources
tests/windows/ Windows conversion and native drop-event checks
.github/       Native artifact build workflow
```

This tool is an independent utility and is not affiliated with Dreamtonics. No proprietary voice data, activation credentials, or user MIDI/projects are included.

## v1.1: format corrections and multi-file merging

- Select, append or drop multiple MIDI files/folders and merge them into one `vocal.svp`. Each file starts at the same project origin; its own PPQ is normalized to musical beats. Source lanes stay separate, with continuous Melody numbering. Output follows the first input file's folder by default.
- Compact two-column native UI with all settings visible and no scrolling.
- Singer text maps directly to `mainRef.database.name`: `Mai` (default), `Mai 2`. Paste a complete database JSON object or use **Read from SVP** to preserve the exact engine and version from a correctly configured project. The old pipe syntax remains compatible; no speculative version 201 is assigned.
- Natural minor now serializes as `NaturalMinor`, verified against an SV2-saved project, instead of the unrecognized `Minor` value.
- Quantization changes note onset/duration. The grid shown in SV2's toolbar is an editor preference stored locally by SV2, not an SVP project field. This app does not modify SV2's global preferences.
- The original SVP distributed by [shibobiiiiiii](https://www.bilibili.com/video/BV13L4y1g7s2/) confirms first-generation Stardust Infinity AI as `MEDIUM5·Stardust`, native language `mandarin`, backend `SVR2AI`. This is not evidence of the second-generation identifier; use Read from SVP for that version. Third-party score data is not bundled.


## v1.1.1

The voice input displays only the name after Read from SVP, while preserving imported engine/version metadata internally and across restarts. Manual edits replace it. First-generation Mai remains the default. Windows releases now ship as VocalGrid-Windows.zip; extract and run VocalGrid-Windows.exe. The header is shorter with additional height to prevent clipping, and the Chinese scale labels are Natural Major / Natural Minor.


## v1.1.3 track names

Tracks use the MIDI filename without its extension. Duplicate names, including split polyphonic lanes, receive numeric suffixes starting at 2. Matching is case-insensitive and reserves other input filenames, so Piano.mid, another Piano.mid and Piano2.mid become Piano, Piano3 and Piano2. Embedded MIDI track names are not used. The repository and releases are now public.

