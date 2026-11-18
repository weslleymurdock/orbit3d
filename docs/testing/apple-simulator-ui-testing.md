# Apple Development and UI Testing (VS Code Remote - SSH + macOS 27.2)

This document defines the authoritative architecture and operational runbook for developing, building, running, and validating Apple targets (`iOS` and `Mac Catalyst`) using **Visual Studio Code Remote - SSH** from Windows to the provisioned **macOS `27.2`** host.

| Platform | Implementation | Build and Execution | Appium UI Tests |
|---|---|---|---|
| **Android** | Windows local VS Code | Windows local (device/emulator) | Windows local (`uiautomator2`) |
| **Windows** | Windows local VS Code | Windows local | Windows local (`windows`) |
| **iOS** | Windows VS Code via Remote - SSH | Remote macOS `27.2` workspace | Remote macOS `27.2` workspace (`xcuitest`) |
| **Mac Catalyst** | Windows VS Code via Remote - SSH | Remote macOS `27.2` workspace | Remote macOS `27.2` workspace (`mac2`) |

---

## 1. Architecture Overview

```text
Windows Workstation
└── Visual Studio Code
    └── Remote - SSH
        └── Remote macOS 27.2 Host
            ├── Git repository on macOS filesystem
            ├── .NET SDK: 11.0.100 (pinned in global.json)
            ├── Apple Xcode & MAUI workloads (confirm exact versions on remote host)
            ├── iOS Simulator & Mac Catalyst runtime
            ├── Agent Extension Host (runs directly on macOS)
            ├── Build tools & Appium Server (XCUITest / Mac2)
            ├── Unit tests (Orbit.Engine.Tests, Orbit3D.Engine.Tests)
            ├── UI tests (SilkTriangleSample.UITests, future AnimationSample.UITests)
            └── artifacts/ (screenshots, logs, test-results)
```

---

## 2. Why VS Code Remote - SSH Replaces Visual Studio Remote Simulator Bridging

While Visual Studio on Windows can pair to a Mac and project a remote iOS Simulator window onto Windows for manual debugging, that bridge splits the workspace and execution context across two operating systems:

1. **Direct Agentic Execution on macOS**: With VS Code Remote - SSH, the VS Code Server (`.vscode-server`) and the Antigravity agent extension (`google.google-antigravity`) run directly on the macOS host where the Git repository, `.NET SDK`, `Xcode`, `xcrun simctl`, and `Appium` reside.
2. **Elimination of Cross-OS File Sync Overhead**: `dotnet restore`, `dotnet build`, `xcodebuild`, and `xcrun simctl` operate directly against the local APFS filesystem (`/Users/weslleyluiz/git/orbit3d`) rather than copying build payloads over MSBuild Pair-to-Mac SSH tunnels.
3. **No BrowserStack or Paid Signing Dependency for Stage 08**: iOS Simulator `.app` bundles (`iossimulator-x64` / `iossimulator-arm64`) execute natively on CoreSimulator without requiring Apple Developer physical-device `.p12` certificates, provisioning profiles, or BrowserStack App Automate cloud devices.

---

## 3. Windows 11 Prerequisites

On the Windows 11 workstation:

1. Install **Visual Studio Code** (User or System Installer).
2. Install the official **Remote - SSH** extension (`ms-vscode-remote.remote-ssh`).
3. Ensure the Windows OpenSSH Client (`ssh.exe`) is available and can reach the macOS host on the local network.

---

## 4. VS Code Remote - SSH Configuration

Configure `~/.ssh/config` on the Windows 11 machine to connect to the macOS host:

```sshconfig
Host mini.local
    HostName 192.168.3.9
    User weslleyluiz
    ForwardAgent yes
```

To connect from Visual Studio Code on Windows:

1. Open the Command Palette (`Ctrl+Shift+P`).
2. Run **Remote-SSH: Connect to Host...** and select `mini.local` (`weslleyluiz@192.168.3.9`).
3. Once connected, select **File > Open Folder...** and open `/Users/weslleyluiz/git/orbit3d`.

---

## 5. macOS Host Prerequisites

Provisioned Apple development and validation environment:

| Component | Requirement / Verification |
|---|---|
| **Operating System** | `macOS 27.2` |
| **.NET SDK** | `11.0.100` (pinned in [`global.json`](../../global.json)) |
| **Xcode & Command Line Tools** | Installed Apple Xcode toolchain (confirm exact version on the remote host via `xcodebuild -version` and `xcode-select -p`) |
| **MAUI Workloads** | `ios`, `maccatalyst`, `maui` (confirm installed workload versions on the remote host via `dotnet workload list`) |
| **iOS Simulator Runtime** | Available iOS Simulator device/runtime (inspect via `xcrun simctl list runtimes` and `xcrun simctl list devices available`) |
| **Appium & Drivers** | Appium server with `xcuitest` (iOS) and `mac2` (Mac Catalyst) drivers |

---

## 6. Cloning and Opening the Repository on macOS

If the repository is not yet cloned on the macOS host:

```bash
mkdir -p ~/git
cd ~/git
git clone https://github.com/weslleymurdock/orbit3d.git
cd orbit3d
git checkout main
```

Open `/Users/weslleyluiz/git/orbit3d` in the connected VS Code Remote - SSH window.

---

## 7. Antigravity Remote Workspace Execution

In the VS Code Remote - SSH window, the **Google Antigravity** extension (`google.google-antigravity`) runs inside the remote extension host (`~/.vscode-server/extensions/google.google-antigravity-1.11.0-darwin-x64`):

- **Environment**: `ANTIGRAVITY_VSCODE_HOST=1`, `ANTIGRAVITY_AGENT=1`, `SSH_CONNECTION=192.168.3.2 ... 192.168.3.9 22`.
- **Capabilities Verified**:
  - Direct read/write access to `/Users/weslleyluiz/git/orbit3d` on macOS.
  - Native zsh command execution on macOS (`dotnet`, `xcodebuild`, `xcrun simctl`, `appium`, `git`, `gh`).
  - Direct capture and visual inspection of iOS Simulator screenshots and unified logs.

---

## 8. Toolchain Versions and Pinning Policy

The repository root pins the .NET SDK in [`global.json`](../../global.json):

```json
{
  "sdk": {
    "rollForward": "disable",
    "version": "11.0.100"
  }
}
```

And [`Directory.Build.props`](../../Directory.Build.props) suppresses strict minor Xcode version mismatches across local and CI environments while pinning `MauiVersion`:

```xml
<Project>
  <PropertyGroup>
    <ValidateXcodeVersion>false</ValidateXcodeVersion>
    <_ValidateXcodeVersion>false</_ValidateXcodeVersion>
    <MauiVersion>10.0.0</MauiVersion>
  </PropertyGroup>
</Project>
```

---

## 9. Verifying and Booting the iPhone Air — iOS 26.5 Simulator

Inspect available runtimes and devices:

```bash
xcrun simctl list runtimes
xcrun simctl list devices available
```

Verify the required **iPhone Air** device under `-- iOS 26.5 --`:

- **Name**: `iPhone Air`
- **UDID**: `34E134C7-25BB-4925-8A3A-65674A943E87`
- **Runtime**: `com.apple.CoreSimulator.SimRuntime.iOS-26-5` (`iOS 26.5`)

Boot the simulator and wait for readiness:

```bash
xcrun simctl boot 34E134C7-25BB-4925-8A3A-65674A943E87
open -a Simulator
xcrun simctl bootstatus 34E134C7-25BB-4925-8A3A-65674A943E87 -b
```

---

## 10. Building `SilkTriangleSample` for iOS Simulator

Restore and build the `net11.0-ios` target in `Release` configuration:

```bash
dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj \
  -c Release \
  -f net11.0-ios
```

This produces the simulator application bundle at:

```text
games/3D/SilkTriangleSample/bin/Release/net11.0-ios/iossimulator-x64/SilkTriangleSample.app
```

---

## 11. Installing and Launching the Application

Install the `.app` bundle onto the booted **iPhone Air** simulator and launch it:

```bash
xcrun simctl install 34E134C7-25BB-4925-8A3A-65674A943E87 \
  games/3D/SilkTriangleSample/bin/Release/net11.0-ios/iossimulator-x64/SilkTriangleSample.app

xcrun simctl launch 34E134C7-25BB-4925-8A3A-65674A943E87 \
  com.remotecodehub.silktrianglesample
```

---

## 12. Capturing Screenshots and Simulator Logs

Create the local unversioned artifact directories:

```bash
mkdir -p artifacts/apple/stage-08/screenshots \
         artifacts/apple/stage-08/logs \
         artifacts/apple/stage-08/test-results/unit-tests \
         artifacts/apple/stage-08/test-results/ui-tests
```

Capture screenshots directly from the framebuffer of the running simulator:

```bash
xcrun simctl io 34E134C7-25BB-4925-8A3A-65674A943E87 screenshot \
  artifacts/apple/stage-08/screenshots/initial-render.png
```

Collect simulator and application logs:

```bash
xcrun simctl spawn 34E134C7-25BB-4925-8A3A-65674A943E87 log show --last 5m --style compact \
  > artifacts/apple/stage-08/logs/simulator.log

grep -E "SilkTriangleSample|SilkSample|Orbit3D" artifacts/apple/stage-08/logs/simulator.log \
  > artifacts/apple/stage-08/logs/application.log
```

### Verified Rendering Telemetry on iPhone Air (iOS 26.5)

- **Native View & Layer**: `SilkIosSurfaceView` backed by `CAEAGLLayer` (`EAGLContext` OpenGL ES 3.0) with `CADisplayLink` render loop.
- **Asset Pipeline**: Loads `toyota-gazoo-racing-wrt-gr-yaris-1-10.glb` via `AssimpModelImporter` (`1` mesh, `20,000` triangles) and sidecar PNG texture (`1008x752`).
- **Diagnostics Overlay**:
  `Phase=Rendering;Context=Ready;Device=Silk;Assets=Loaded;Meshes=1;Texture=1008x752;Items=1;DrawCalls=1;Triangles=20000;FPS=14;Frames=63;Viewport=1260x2195;Yaw=0.00;Pitch=0.00;Dist=5.50`

---

## 13. Running Unit Tests

Execute the unit test suites and output TRX reports to `artifacts/apple/stage-08/test-results/unit-tests/`:

```bash
dotnet test engine/Orbit3D.Engine.Tests/Orbit3D.Engine.Tests.csproj \
  -c Release -f net11.0 \
  --results-directory artifacts/apple/stage-08/test-results/unit-tests \
  --logger "trx;LogFileName=Orbit3D.Engine.Tests.trx"

dotnet test engine/Orbit.Engine.Tests/Orbit.Engine.Tests.csproj \
  -c Release -f net11.0 \
  --results-directory artifacts/apple/stage-08/test-results/unit-tests \
  --logger "trx;LogFileName=Orbit.Engine.Tests.trx"

dotnet test games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj \
  -c Release --filter "Category=Artifact" \
  --results-directory artifacts/apple/stage-08/test-results/unit-tests \
  --logger "trx;LogFileName=ArtifactDiscoveryTests.trx"
```

---

## 14. Running iOS Simulator UI Tests (Appium + XCUITest)

Start the local Appium server:

```bash
appium --address 127.0.0.1 --port 4723 --log artifacts/apple/stage-08/logs/appium.log
```

Execute the `IosSampleAppTests` suite against the booted **iPhone Air — iOS 26.5** simulator:

```bash
export ORBIT3D_USE_BROWSERSTACK=false
export ORBIT3D_IOS_SIMULATOR_UDID="34E134C7-25BB-4925-8A3A-65674A943E87"
export ORBIT3D_IOS_DEVICE="iPhone Air"
export ORBIT3D_IOS_OS_VERSION="26.5"
export ORBIT3D_TEST_ARTIFACTS_DIR="$(pwd)/artifacts/apple/stage-08/screenshots"

dotnet test games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj \
  -c Release \
  --filter "Platform=iOS" \
  --results-directory artifacts/apple/stage-08/test-results/ui-tests \
  --logger "trx;LogFileName=IosSampleAppTests.trx"
```

---

## 15. Local Artifact Storage and Git Exclusion

All local screenshots, logs, and TRX files are stored under `artifacts/apple/stage-08/`. The root [`.gitignore`](../../.gitignore) excludes `artifacts/` so local validation binaries, screenshots, and logs are never committed to Git.

---

## 16. GitHub Actions CI Integration (Helper Only)

GitHub Actions workflows are supplementary helpers for builds, diagnostics, and packaging. They are **never** the authoritative validation mechanism for Apple targets and never replace building and executing apps and Appium UI tests directly in the remote macOS `27.2` VS Code Remote - SSH workspace:
- **`.github/workflows/dotnet-macos.yml`**: Supplementary macOS library build and desktop unit-test workflow (`Orbit.Engine.Tests`, `Orbit3D.Engine.Tests`).
- **`.github/workflows/dotnet-windows.yml`**: Supplementary multi-target build and test workflow.
- **`.github/workflows/silk-triangle-sample-build.yml`**: Supplementary packaging workflow for `SilkTriangleSample` build artifacts.
