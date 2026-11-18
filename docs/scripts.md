# Orbit3D Development Tooling and Scripts

## Authoritative Workspace Model
Orbit3D does **not** use Windows-to-macOS build orchestration scripts, source TAR/SCP transfers, or CI-artifact download/validation scripts.

- **Windows and Android**: Develop, build, run, and test directly in the local Windows VS Code workspace using standard `dotnet` and `adb` commands.
- **iOS and Mac Catalyst**: Connect from Windows VS Code to the provisioned **macOS `27.2`** host via **VS Code Remote - SSH**, open the repository on macOS, and run `dotnet`, `xcrun simctl`, and Appium directly in the remote workspace.

For full workflow instructions, see [Development Guide](development.md) and [UI Automation Guide](testing/ui-automation.md).

## Supported Utilities Inventory

| Path | Host | Purpose |
|---|---|---|
| [`tools/remote/apple/fix-appicon.py`](../tools/remote/apple/fix-appicon.py) | macOS | MSBuild helper invoked by `_FixMissingAppIconAssets` in [`SilkTriangleSample.csproj`](../games/3D/SilkTriangleSample/SilkTriangleSample.csproj) before `_CoreCompileImageAssets` on Mac Catalyst to populate missing Resizetizer `appicon.appiconset` and `dotnet_bot` PNG entries for `actool`. |
| [`scripts/package-releases.ps1`](../scripts/package-releases.ps1) | Windows / CI | Builds, packs, and pushes NuGet packages (`Orbit.Engine`, `Orbit.Input`, etc.) for release workflows ([`.github/workflows/nuget-release.yml`](../.github/workflows/nuget-release.yml)). |
| [`scripts/rollback-dotnet-workloads.ps1`](../scripts/rollback-dotnet-workloads.ps1) | Windows / macOS | Legacy workload rollback helper paired with [`scripts/workload-rollback.json`](../scripts/workload-rollback.json) for manual workload maintenance when required. |

## Direct Build and Run Commands

### Windows (local Windows workspace)
```powershell
dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj -c Debug -f net11.0-windows10.0.19041.0
dotnet run --project games/3D/SilkTriangleSample/SilkTriangleSample.csproj -f net11.0-windows10.0.19041.0
```

### Android (local Windows workspace)
```powershell
dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj -c Debug -f net11.0-android
dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj -t:Run -c Debug -f net11.0-android
```

### iOS Simulator (remote macOS `27.2` VS Code Remote - SSH workspace)
```bash
dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj -c Debug -f net11.0-ios
# Install and launch on a booted simulator via xcrun simctl, or use -t:Run
```

### Mac Catalyst (remote macOS `27.2` VS Code Remote - SSH workspace)
```bash
dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj -c Debug -f net11.0-maccatalyst
dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj -t:Run -c Debug -f net11.0-maccatalyst
```
