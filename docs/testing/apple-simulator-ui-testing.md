# Apple Simulator Development and UI Testing (VS Code Remote - SSH + macOS)

This document defines the Stage 08 architecture and operational runbook for developing, building, running, and validating `SilkTriangleSample` on Apple targets using **Visual Studio Code Remote - SSH** from Windows 11 to a provisioned macOS host with the **iPhone Air — iOS 26.5 Simulator**.

---

## 1. Architecture Overview

```text
Windows 11
└── Visual Studio Code
    └── Remote - SSH
        └── Remote macOS Host (mini.local / 192.168.3.9)
            ├── Git repository: /Users/weslleyluiz/git/orbit3d
            ├── .NET SDK: 10.0.302 (workloadSet: 10.0.302.1)
            ├── MAUI workloads: ios, maccatalyst, macos, maui
            ├── Xcode: 26.6 (Build 17F113)
            ├── iOS Simulator
            │   └── iPhone Air - iOS 26.5 (34E134C7-25BB-4925-8A3A-65674A943E87)
            ├── Antigravity Agent (Remote Extension Host)
            ├── Build tools & Appium Server (XCUITest / Mac2)
            ├── Unit tests (Orbit.Engine.Tests, Orbit3D.Engine.Tests)
            ├── UI tests (SilkTriangleSample.UITests)
            └── artifacts/
                └── apple/
                    └── stage-08/
                        ├── screenshots/
                        ├── logs/
                        └── test-results/
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

Verified baseline on the remote macOS host (`mini.local`):

| Component | Verified Version / Path |
|---|---|
| **Operating System** | macOS Sequoia `15.6.1` (`Build 24G90`, `Darwin 24.6.0 x86_64`) |
| **Xcode** | `Xcode 26.6` (`Build 17F113`) at `/Applications/Xcode.app/Contents/Developer` |
| **Command Line Tools** | Xcode 26.6 (`xcode-select -p`) |
| **.NET SDK** | `10.0.302` (`/usr/local/share/dotnet/dotnet`) |
| **Workload Set** | `10.0.302.1` (`ios 26.5.10315/10.0.100`, `maccatalyst 26.5.10315/10.0.100`, `maui 10.0.20/10.0.100`) |
| **iOS Simulator Runtime** | `iOS 26.5` (`26.5 - 23F77`, `com.apple.CoreSimulator.SimRuntime.iOS-26-5`) |
| **Validation Simulator** | `iPhone Air` (`UDID: 34E134C7-25BB-4925-8A3A-65674A943E87`) |
| **Node.js & Appium** | Node `v20.19.0`, `appium@3.9.0`, `xcuitest@12.22.8`, `mac2@4.6.1` |

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

The repository root pins the exact SDK and workload set in [`global.json`](../../global.json):

```json
{
  "sdk": {
    "version": "10.0.302",
    "workloadVersion": "10.0.302.1",
    "rollForward": "disable"
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

Restore and build the `net10.0-ios` target in `Release` configuration:

```bash
dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj \
  -c Release \
  -f net10.0-ios
```

This produces the simulator application bundle at:

```text
games/3D/SilkTriangleSample/bin/Release/net10.0-ios/iossimulator-x64/SilkTriangleSample.app
```

---

## 11. Installing and Launching the Application

Install the `.app` bundle onto the booted **iPhone Air** simulator and launch it:

```bash
xcrun simctl install 34E134C7-25BB-4925-8A3A-65674A943E87 \
  games/3D/SilkTriangleSample/bin/Release/net10.0-ios/iossimulator-x64/SilkTriangleSample.app

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
  -c Release -f net10.0 \
  --results-directory artifacts/apple/stage-08/test-results/unit-tests \
  --logger "trx;LogFileName=Orbit3D.Engine.Tests.trx"

dotnet test engine/Orbit.Engine.Tests/Orbit.Engine.Tests.csproj \
  -c Release -f net10.0 \
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

## 16. GitHub Actions CI Integration

- **`.github/workflows/dotnet-macos.yml`**: Installs `.NET SDK 10.0.302` and MAUI workload set `10.0.302.1` on `macos-26`, builds `Orbit.Engine`, `Orbit.Input`, `Orbit3D.Engine`, and `Orbit3D.Graphics.Silk` across `net10.0`, `net10.0-ios`, and `net10.0-maccatalyst`, and runs `Orbit.Engine.Tests` and `Orbit3D.Engine.Tests` with TRX artifact upload.
- **`.github/workflows/dotnet-windows.yml`**: Builds all engine libraries and runs unit tests (`Orbit.Engine.Tests`, `Orbit3D.Engine.Tests`, `ArtifactDiscoveryTests`), then validates `SilkTriangleSample` across `Windows`, `Android`, `iOS` (including iOS Simulator provisioning and Appium/XCUITest UI tests), and `MacCatalyst`.
- **`.github/workflows/silk-triangle-sample-build.yml`**: Builds and packages `SilkTriangleSample` artifacts for `Android` (`.apk`), `Windows` (`win-x64`), `MacCatalyst` (`.app.zip`), and `iOS` (packaging `SilkTriangleSample-iossimulator.app.zip` when physical-device Apple signing secrets are absent, or signed `.ipa` when configured).
