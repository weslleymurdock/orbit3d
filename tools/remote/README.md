# Apple Build Helpers

Apple development, builds, execution, and UI automation run **exclusively** inside the provisioned **macOS `27.2`** repository workspace via **Visual Studio Code Remote - SSH** from Windows.

The legacy Windows-to-macOS SSH/SCP orchestration scripts (`validate-apple.ps1`, `remote-validate-apple.sh`) have been removed and are no longer supported.

## Preserved Build Helpers
- [`apple/fix-appicon.py`](apple/fix-appicon.py): Invoked automatically on non-Windows hosts by the `_FixMissingAppIconAssets` MSBuild target in [`games/3D/SilkTriangleSample/SilkTriangleSample.csproj`](../../games/3D/SilkTriangleSample/SilkTriangleSample.csproj) before `_CoreCompileImageAssets` when building `net11.0-maccatalyst`. It ensures `Contents.json` and expected appicon/image PNGs exist in Resizetizer output directories so Apple's `actool` succeeds cleanly.

See [Development Guide](../../docs/development.md) and [Apple Simulator UI Testing](../../docs/testing/apple-simulator-ui-testing.md) for the current VS Code Remote - SSH workflow.
