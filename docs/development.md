# Development Guide

## Structure
- `engine/` — reusable engine projects (`Orbit.Engine`, `Orbit.Input`, `Orbit3D.Engine`, `Orbit3D.Graphics.Silk`, and unit test projects).
- `games/` — sample/game applications and UI automation projects.
- `assets/` — repository assets (including `assets/Idle.fbx` for Stage 11 animation).
- `scripts/` — release packaging and workload maintenance utilities.
- `tools/` — build helpers (such as `tools/remote/apple/fix-appicon.py`).
- `.github/skills/` and `.agents/skills/` — specialized agent guidance.
- `docs/` — architecture, testing, roadmap, and development rules.

## Authoritative Development and Validation Topology

Orbit3D uses a split-workspace topology where each platform is built, executed, and validated directly on its native host:

| Platform | Workspace & Implementation | Build & Execution | Appium UI Tests |
|---|---|---|---|
| **Windows** | Windows local VS Code | Windows local | Windows local (`wincore`) |
| **Android** | Windows local VS Code | Windows local (device/emulator) | Windows local (`uiautomator2`) |
| **iOS** | Windows VS Code via Remote - SSH | Remote macOS `27.2` workspace | Remote macOS `27.2` workspace (`xcuitest`) |
| **Mac Catalyst** | Windows VS Code via Remote - SSH | Remote macOS `27.2` workspace | Remote macOS `27.2` workspace (`mac2`) |

### Windows and Android Workflow
- Open the repository in the local Windows VS Code workspace.
- Implement, build, execute on Windows and Android devices/emulators, run Appium UI tests, and inspect screenshots, logs, and diagnostics locally.
- No remote macOS execution is required for Windows or Android targets.

### Apple Workflow (iOS and Mac Catalyst)
- Connect from Windows VS Code to the provisioned **macOS `27.2`** host using **Visual Studio Code Remote - SSH**.
- Open the repository workspace directly on the remote macOS filesystem and run the agent inside that remote VS Code workspace.
- Implement Apple-specific code, build with `.NET SDK 11.0.100` and the installed Apple toolchain (confirm exact Xcode/workload versions on the remote host as an environment prerequisite), run the iOS Simulator and Mac Catalyst apps, execute Appium UI tests (`xcuitest` / `mac2`), and inspect screenshots and logs directly on macOS.
- Do **not** use Windows-to-macOS archive transfers, SCP staging, or remote SSH build-orchestration scripts as a substitute for VS Code Remote - SSH.

### Cross-Platform Changes
When a change spans both platform groups:
1. Implement and validate Windows and Android in the local Windows workspace.
2. Connect to the remote macOS `27.2` workspace via VS Code Remote - SSH and implement/validate iOS and Mac Catalyst there.
3. Keep both workspaces synchronized via the normal Git workflow.
4. Report real execution evidence separately per platform.

## Toolchain and SDK Baseline
- **.NET SDK**: `11.0.100` (pinned in [`global.json`](../global.json) with `rollForward: disable`).
- **Windows**: Local Windows 10/11 workstation with .NET SDK `11.0.100` and MAUI Windows/Android workloads.
- **macOS**: Remote **macOS `27.2`** host with `.NET SDK 11.0.100` and the Apple/MAUI toolchain (exact Xcode and workload-set versions are environment prerequisites to be confirmed on the remote host).
- Do not arbitrarily modify `global.json`, [`Directory.Build.props`](../Directory.Build.props), target frameworks, or package/workload pins unless a concrete incompatibility is discovered and justified.

## CI Policy (Helper Only)
- GitHub Actions workflows (`.github/workflows/`) are optional supporting infrastructure for builds, diagnostics, packaging, and supplementary checks.
- CI is **never** the authoritative validation mechanism for an implementation stage.
- A passing CI run does not prove a platform works, and a failing CI run does not invalidate a locally/remotely validated implementation.
- Never wait for CI or substitute CI status/artifacts for executing real applications on Windows, Android, iOS Simulator, or Mac Catalyst.

## Milestones
Implement 3D in independent stages. Do not implement future-stage functionality early merely because an abstraction is obvious.

For each stage: inspect current code, read relevant guidance, make the smallest coherent change, build, execute and test on real targets, and document limitations.

## Historical Stage Notes
- **Stage 05 (Android)**: Established the `GLSurfaceView`/GLES 3 backend in `Orbit3D.Graphics.Silk` and physical-device rendering.
- **Stage 07 (Windows)**: Established the non-activating owned WGL OpenGL 3.3 popup surface (`UseSilkGraphics()`) to avoid WinUI composition occlusion, rendering `SilkTriangleSample` on `net11.0-windows10.0.19041.0`.
- **Stage 08 (iOS)**: Established `SilkIosSurfaceView` (`CAEAGLLayer` + `EAGLContext` OpenGL ES 3.0) and CoreGraphics sidecar texture decoding in `SampleTextureLoader`.
- **Stage 09 (Mac Catalyst)**: Established `SilkMacSurfaceView` (`CAOpenGLLayer` + CGL OpenGL 4.1 core profile) via `UseSilkGraphics()`. Build and run directly on macOS via `dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj -c Debug -f net11.0-maccatalyst`.
- **Stage 10 (Cross-Platform Stabilization)**: Consolidated lifecycle, context recreation, CPU/GPU caching, and rendering consistency across Windows, Android, iOS, and Mac Catalyst.
- **Stage 11 — Phase 1 (Windows & Android Skeletal Animation)**: Added backend-neutral `Skeleton3D`, `SkinningData`, `AnimationClip3D`, and `AnimationEvaluator` in `Orbit3D.Engine`, Assimp skeletal/clip import for `assets/Idle.fbx`, `RGBA32F` bone-texture GPU skinning in `Orbit3D.Graphics.Silk`, and `games/3D/AnimationSample/` + `games/3D/AnimationSample.UITests/`.
- **Stage 11 — Phase 2 (iOS & Mac Catalyst Skeletal Animation)**: Standardized central `MauiVersion` (`11.0.10` in `Directory.Build.props`), added `SceneDelegate.cs` for Apple `UIApplicationSceneManifest` startup in `AnimationSample`, and validated `AnimationSample` + `IosAnimationTests` (6/6 on `iPhone 17 — iOS 27.2`) and `MacCatalystAnimationTests` (7/7 on `macOS 27.2.1` `Apple M4`) directly on the remote macOS host via VS Code Remote - SSH.
- **Stage 11 — Phase 3 (Windows & Android Final Validation)**: Recovered Windows UI automation using Appium `4.0.0` + `wincore@4.1.2` (`automationName: Wincore`, `SilkSurfaceAutomationPeer`, `windows: invoke`, and `ORBIT3D_SILK_CAPTURE_DIR` readback fallback), passing `WindowsAnimationTests` (5/5), and revalidated `AndroidAnimationTests` (5/5 with `uiautomator2@9.2.1` on `emulator-5554`) against `MauiVersion=11.0.10`.

## Public API
Public engine APIs require XML documentation. Prefer stable Orbit concepts over third-party implementation details.

## Dependencies
Before adding a graphics dependency evaluate target frameworks, native dependencies, license, platform coverage, surface integration, shader support and maintenance.

## Commits
Use focused commit messages. Do not mix unrelated formatting or refactoring into milestone changes.

## Editor
The editor is explicitly deferred.
