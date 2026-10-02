# Orbit3D Development and Validation Scripts

## Purpose

Orbit3D is developed primarily on Windows. Apple validation requires the macOS/Xcode toolchain, so the project uses a Windows-first SSH/SCP workflow.

The Windows working tree remains authoritative for the current agent session. The scripts transfer the current working tree, including uncommitted changes, to a temporary macOS directory. The Mac performs the Apple build/run, captures evidence, and returns the artifacts.

Apple command-line tools such as simctl and xcodebuild are supplied by Xcode once the active developer directory is configured.

## Script inventory

| Script | Host | Purpose |
| --- | --- | --- |
| tools/remote/apple/validate-apple.ps1 | Windows | Build, archive, SCP upload, SSH execution, and artifact download. |
| tools/remote/apple/remote-validate-apple.sh | macOS | Apple build, launch, screenshot, and diagnostics. |
| tools/remote/README.md | Both | Short operational reference. |

## Prerequisites

### Windows

Required commands: Git, .NET 10 SDK, OpenSSH ssh, OpenSSH scp, tar, robocopy.

Use an ANSI-capable terminal for the colored progress display.

### macOS

Required: macOS compatible with the installed .NET 10/Xcode toolchain, .NET 10 SDK, .NET MAUI workload, Xcode command-line tools, iOS Simulator for iOS validation, screencapture for Mac Catalyst, and SSH server.

.NET MAUI supports running an iOS app from macOS with dotnet build -t:Run -f net10.0-ios and selecting a Simulator using _DeviceName.

Mac Catalyst supports dotnet build -t:Run -f net10.0-maccatalyst.

## SSH setup

    $env:ORBIT3D_APPLE_SSH_HOST = "mac-mini.local"
    $env:ORBIT3D_APPLE_SSH_USER = "weslley"
    $env:ORBIT3D_APPLE_SSH_PORT = "22"
    $env:ORBIT3D_APPLE_SSH_KEY = "$HOME\.ssh\id_ed25519"

Test:

    ssh weslley@mac-mini.local "uname -s"

Expected:

    Darwin

The private key stays on Windows. The scripts never copy it to the Mac.

## iOS Simulator

On the Mac:

    xcrun simctl list devices available

Set the selected UDID on Windows:

    $env:ORBIT3D_IOS_SIMULATOR_UDID = "YOUR-SIMULATOR-UDID"

If omitted, the runner selects the first available iPhone Simulator.

The iOS runner boots the Simulator, waits for readiness, launches the MAUI sample, and captures the device using xcrun simctl io screenshot.

## Stage 08: iOS validation

Run:

    .\tools\remote\apple\validate-apple.ps1

Sequence:

1. Build the solution on Windows.
2. Copy the current working tree to a temporary staging directory.
3. Exclude .git, bin, obj, .vs, and generated artifacts.
4. Create a compressed archive.
5. Connect through SSH.
6. Upload with SCP.
7. Extract under /tmp/orbit3d-validation/<run-id>.
8. Build and launch net10.0-ios.
9. Wait for the rendered scene.
10. Capture a Simulator screenshot.
11. Collect diagnostics.
12. Download artifacts.
13. Remove the remote workspace unless -KeepRemote is used.

The transfer uses the current working tree rather than the last commit, which is important for agent-driven iteration.

### Examples

    .\tools\remote\apple\validate-apple.ps1

    .\tools\remote\apple\validate-apple.ps1 -MacHost "mac-mini.local" -MacUser "weslley"

    .\tools\remote\apple\validate-apple.ps1 -SimulatorId "YOUR-UDID"

    .\tools\remote\apple\validate-apple.ps1 -ProjectPath "games/3D/SilkTriangleSample/SilkTriangleSample.csproj"

## Stage 09: Mac Catalyst validation

Run:

    .\tools\remote\apple\validate-apple.ps1 -Platform maccatalyst

The Mac executes dotnet build -t:Run -f net10.0-maccatalyst and captures the desktop using screencapture. The initial implementation intentionally captures the desktop rather than introducing an additional native window-capture component.

## Fast iteration

Skip the Windows build:

    .\tools\remote\apple\validate-apple.ps1 -SkipWindowsBuild

Keep the remote workspace:

    .\tools\remote\apple\validate-apple.ps1 -KeepRemote

Skip screenshots:

    .\tools\remote\apple\validate-apple.ps1 -NoScreenshot

Release validation:

    .\tools\remote\apple\validate-apple.ps1 -Configuration Release

## Artifacts

    artifacts/
    └── remote-apple/
        └── <run-id>/
            ├── screenshot.png
            ├── build-run.log
            ├── stdout.log
            ├── stderr.log
            ├── relevant-log.txt
            ├── validation.json
            ├── simulator.txt
            ├── xcode-path.txt
            ├── xcode-version.txt
            └── dotnet-version.txt

For Orbit3D, inspect screenshot.png for the expected Toyota GLB, geometry orientation, camera framing, texture visibility, missing triangles, incorrect depth/culling, black surfaces, and obvious corruption.

A screenshot proves visible output only; it does not prove correct GPU ownership or context recreation.

relevant-log.txt filters for Orbit3D, Silk, draw, triangle, texture, context, metrics, errors, and exceptions.

validation.json provides machine-readable run metadata for agents and future automation.

## Agent iteration loop

    Read architecture/skills
      -> implement
      -> Windows build/tests
      -> validate-apple.ps1
      -> inspect validation.json
      -> inspect relevant-log.txt
      -> inspect screenshot.png
      -> diagnose
      -> fix
      -> repeat

Do not treat a successful Windows build as proof of Apple platform support.

## Transport and cleanup

    Windows working tree
      -> temporary archive
      -> SCP
      -> /tmp/orbit3d-validation/<run-id>
      -> remote Apple build/run
      -> artifacts
      -> SCP
      -> Windows artifacts/remote-apple/<run-id>

No permanent Orbit3D service is installed on the Mac. Remote source is removed after the run unless -KeepRemote is specified.

## Security

The scripts use the existing SSH key, do not store passwords, do not copy private keys to the Mac, do not create a persistent daemon, and do not commit or push changes.

Host-specific SSH configuration belongs in environment variables or local SSH configuration, not source control.

## ANSI progress display

The scripts use a thin Unicode progress line with a pastel purple/blue/indigo gradient. The percentage represents orchestration phases, not compiler progress.

Use -NoColor when required. The PowerShell runner also honors NO_COLOR.

## Troubleshooting

SSH:

    ssh -v weslley@mac-mini.local "uname -s"

SCP:

    scp .\README.md weslley@mac-mini.local:/tmp/

Xcode:

    xcode-select -p
    xcodebuild -version
    xcrun simctl list devices

iOS:

    xcrun simctl bootstatus YOUR-UDID -b

Mac Catalyst:

    dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj -c Debug -f net10.0-maccatalyst -t:Run

## Current limitations

- iOS screenshots use simctl.
- Mac Catalyst initially captures the full desktop.
- Runtime metrics are not invented by the script; they must come from application diagnostics.
- The scripts do not install Xcode, .NET workloads, or Simulator runtimes.
- Physical iOS devices are out of scope.
- Code-signing setup is out of scope.
- No CI service is introduced.

Apple ScreenCaptureKit can capture selected displays, applications, and windows, but adopting it would add a native capture component and permission model, so it is intentionally deferred.

## Roadmap use

### Stage 08

Use iOS mode to validate the native iOS graphics surface, context lifecycle, resize, mesh/texture upload, indexed rendering, camera, metrics, and recreation.

### Stage 09

Use Mac Catalyst mode to validate the same runtime path on desktop Apple hardware.

### Stage 10

Run the same representative scene on Android, Windows, iOS, and Mac Catalyst and compare rendering, lifecycle, metrics, performance, and platform limitations.
