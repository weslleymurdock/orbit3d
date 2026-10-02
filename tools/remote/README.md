# Remote Apple Validation

Windows remains the primary Orbit3D workspace. The macOS host is a temporary validation runner reached through SSH/SCP.

## Scripts

- tools/remote/apple/validate-apple.ps1 — Windows orchestrator.
- tools/remote/apple/remote-validate-apple.sh — macOS Apple runner.

The Windows script archives the current working tree, excluding Git metadata and generated output, uploads it with SCP, invokes the Apple runner through SSH, and downloads artifacts.

Supported targets: ios and maccatalyst.

No permanent Orbit3D daemon is installed on the Mac.

## Required Windows configuration

    $env:ORBIT3D_APPLE_SSH_HOST = "mac-mini.local"
    $env:ORBIT3D_APPLE_SSH_USER = "weslley"
    $env:ORBIT3D_APPLE_SSH_PORT = "22"
    $env:ORBIT3D_APPLE_SSH_KEY = "$HOME\.ssh\id_ed25519"
    $env:ORBIT3D_IOS_SIMULATOR_UDID = "YOUR-SIMULATOR-UDID"

A passwordless SSH key is recommended. Never commit private keys or passwords.

## Examples

    .\tools\remote\apple\validate-apple.ps1
    .\tools\remote\apple\validate-apple.ps1 -Platform maccatalyst
    .\tools\remote\apple\validate-apple.ps1 -SimulatorId "YOUR-UDID"
    .\tools\remote\apple\validate-apple.ps1 -SkipWindowsBuild
    .\tools\remote\apple\validate-apple.ps1 -KeepRemote
    .\tools\remote\apple\validate-apple.ps1 -NoScreenshot
    .\tools\remote\apple\validate-apple.ps1 -Configuration Release

## Output

Results are downloaded to artifacts/remote-apple/<run-id>/.

Typical files include screenshot.png, build-run.log, stdout.log, stderr.log, relevant-log.txt, validation.json, and Xcode/.NET version information.

## Agent workflow

Use one deterministic command:

    Implement -> Windows build/tests -> validate-apple.ps1 -> inspect screenshot/logs -> fix -> repeat

A successful Windows build is not evidence that an Apple platform works.
