# Orbit3D 3D Sample UI Automation and Testing Infrastructure

This document is the authoritative guide for the UI testing infrastructure of `SilkTriangleSample` located in `games/3D/SilkTriangleSample.UITests/`.

---

## 1. Purpose of `SilkTriangleSample.UITests`

`SilkTriangleSample.UITests` provides end-to-end UI automation and rendering verification for the Orbit3D MAUI 3D sample application across Android, iOS, Mac Catalyst, and Windows. It ensures that:
- The real MAUI application builds, installs, and launches on native targets and simulators.
- Native graphics surfaces (`SilkGraphicsSurface`) initialize the real Silk graphics device and renderer (no mock renderers).
- Real 3D assets (GLB models, sidecar textures) load into Orbit3D scenes and upload to GPU buffers.
- Genuine rendering progress (indexed draw calls, frame metrics) is verified through an observable diagnostics contract.
- Visual diagnostics (screenshots, page sources, server logs) are retained on test failure.

---

## 2. Platform Directory Structure

The test project is structured with dedicated directories for each supported target platform:

```text
games/3D/SilkTriangleSample.UITests/
├── SilkTriangleSample.UITests.csproj
├── Android/
│   ├── Pages/
│   │   └── AndroidGraphicSurfacePage.cs
│   └── Tests/
│       └── AndroidGraphicSurfaceTests.cs
├── iOS/
│   ├── Pages/
│   │   └── IosMainPage.cs
│   └── Tests/
│       └── IosSampleAppTests.cs
├── MacCatalyst/
│   ├── Pages/
│   │   └── MacCatalystMainPage.cs
│   └── Tests/
│       └── MacCatalystSampleAppTests.cs
├── Windows/
│   ├── Pages/
│   │   └── WindowsGraphicSurfacePage.cs
│   └── Tests/
│       └── WindowsGraphicSurfaceTests.cs
└── Infrastructure/
    ├── AppArtifactLocator.cs
    ├── AppArtifactManifest.cs
    ├── AppiumDriverFactory.cs
    ├── BasePage.cs
    ├── DiagnosticsCollector.cs
    ├── PlatformKind.cs
    ├── PlatformTestFixture.cs
    ├── TestConfiguration.cs
    └── Tests/
        └── ArtifactDiscoveryTests.cs
```

---

## 3. Page Object Design and Selector Conventions

Each platform implements the Page Object pattern (`BasePage`), isolating UI automation selectors and interactions from test assertions:
- **Stable Accessibility Identifiers:** Elements are targeted via `MobileBy.AccessibilityId(...)`, backed by `AutomationId` on MAUI `VisualElement` controls.
- **GraphicSurfacePage Identifiers:**
  - `SilkGraphicsSurface`: The native accelerated graphics surface.
  - `StatusLabel`: Human-readable status indicator.
  - `DiagnosticsLabel`: Structured machine-readable diagnostic contract (`Phase=...;Context=...;Assets=...;DrawCalls=...;Triangles=...;FPS=...;Frames=...`).
- **MainPage Identifiers (iOS and Mac Catalyst):**
  - `HeadlineLabel`: Main page heading ("Hello, World!").
  - `SubHeadlineLabel`: Subheading description.
  - `CounterBtn`: Interactive button.
  - `DotNetBotImage`: Application image.
- **Bounded Explicit Waits:** Avoid fixed sleeps; all element lookups and condition verifications use bounded timeouts (`WaitForElement`, `WaitForText`).

---

## 4. Required .NET SDK and Workload Configuration

The repository pins the following toolchain baseline:
- **.NET SDK:** `10.0.100` (pinned in `global.json`, roll-forward `latestPatch`).
- **Workload Version:** `10.0.100` (pinned in `global.json`).
- **Apple SDK Package Release:** `26.5.10318` (pinned in `Directory.Build.props`).
- **Android SDK Package Version:** `35.0.0` (pinned in `Directory.Build.props`).
- **Windows Desktop App Package Version:** `10.0.26100.0` (pinned in `Directory.Build.props`).
- **Sample MAUI Version:** `10.0.110` (overridden in `SilkTriangleSample.csproj`).

Workloads required per platform:
- Windows: `maui-windows`
- Android: `android`
- iOS: `ios`
- Mac Catalyst: `maccatalyst`

---

## 5. Apple SDK and Xcode Compatibility Policy

As defined in `docs/testing/apple-simulator-ui-testing.md`:
- Apple workload and runtime packages must adhere strictly to `26.5.10318`.
- Hosted macOS runners (`macos-15`) with Xcode 26.6 are supported.
- Tests run against the iOS Simulator; physical device testing is a separate stage.
- Version pins must not be altered locally or in CI to fit runner image mismatches.

---

## 6. Platform Automation Prerequisites

### 6.1 Android
- **Prerequisites:** Java 17+ (Temurin/OpenJDK), Android SDK Platform Tools (`adb`), Android emulator or physical device.
- **Appium Driver:** `uiautomator2` (`appium driver install uiautomator2`).
- **Artifact:** `com.companyname.silktrianglesample-Signed.apk`.

### 6.2 iOS Simulator
- **Prerequisites:** macOS host, Xcode 26.6+, iOS Simulator runtime, Command Line Tools (`xcrun simctl`).
- **Appium Driver:** `xcuitest` (`appium driver install xcuitest`).
- **Artifact:** `SilkTriangleSample.app` (Simulator x64/arm64 bundle, not `.ipa`).

### 6.3 Mac Catalyst
- **Prerequisites:** macOS host, Xcode, Accessibility permissions.
- **Appium Driver:** `mac2` (`appium driver install mac2`).
- **Artifact:** `SilkTriangleSample.app` bundle.
- **Known Limitation:** Mac Catalyst does not currently implement a native Silk graphics surface (`PlatformNotSupportedException`). Startup loads `MainPage`.

### 6.4 Windows
- **Prerequisites:** Windows 10/11 x64, Developer Mode enabled, Windows App SDK Runtime.
- **Appium Driver:** `windows` (`appium driver install windows`).
- **Artifact:** `win-x64/SilkTriangleSample.exe` (unpackaged WinUI 3 distribution).

---

## 7. Appium Server Configuration and Startup

1. Install Appium globally:
   ```bash
   npm install -g appium
   ```
2. Install the required platform driver:
   ```bash
   # Windows
   appium driver install windows

   # Android
   appium driver install uiautomator2

   # iOS
   appium driver install xcuitest

   # Mac Catalyst
   appium driver install mac2
   ```
3. Start Appium server:
   ```powershell
   appium --address 127.0.0.1 --port 4723
   ```
4. Configure environment variables (optional overrides):
   ```powershell
   $env:ORBIT3D_APPIUM_URL = "http://127.0.0.1:4723/"
   $env:ORBIT3D_TEST_TIMEOUT_SECONDS = "30"
   $env:ORBIT3D_CAPTURE_SCREENSHOTS = "true"
   ```

---

## 8. Application Build and Artifact Contract

- **Release Configuration:** All tests and artifact validation require `Release` builds.
- **Build Command:**
  ```powershell
  dotnet build games/3D/Orbit3D.Games.slnx --configuration Release -f <TargetFramework>
  ```
- **Deterministic Discovery:** `AppArtifactLocator` resolves the exact binary output from `games/3D/SilkTriangleSample/bin/Release/<TargetFramework>/...`.
- **Stale Binary Prevention:**
  - Validates that the artifact file or `.app` bundle exists.
  - Verifies file size > 0 and calculates SHA256 content hash.
  - Throws `FileNotFoundException` with actionable rebuild instructions if missing or stale.
  - Pre-installed or manually placed applications are never accepted silently.

---

## 9. Platform Packaging and Installation Validation

Artifact validation is executed independently from UI tests via `scripts/validate-artifacts.ps1`:

### 9.1 Windows
```powershell
.\scripts\validate-artifacts.ps1 -Platform Windows -Configuration Release
```
- Verifies `SilkTriangleSample.exe` and required assets (`toyota-gazoo-racing-wrt-gr-yaris-1-10.glb`, `toyota-gazoo-racing-wrt-gr-yaris-1-10.png`).
- Stages the unpackaged layout into `artifacts/installation/Windows`.
- **Strict Policy:** Does not launch the executable during the validation step.

### 9.2 Android
```powershell
.\scripts\validate-artifacts.ps1 -Platform Android -Configuration Release
```
- Resolves `com.companyname.silktrianglesample-Signed.apk`.
- Installs via `adb install -r <apkPath>`.
- Verifies package via `adb shell pm list packages com.companyname.silktrianglesample`.

### 9.3 iOS Simulator
```powershell
.\scripts\validate-artifacts.ps1 -Platform iOS -Configuration Release -SimulatorId "<UDID>"
```
- Resolves `SilkTriangleSample.app` bundle.
- Boots simulator: `xcrun simctl boot <UDID>`.
- Installs app: `xcrun simctl install <UDID> <appPath>`.
- Confirms installation: `xcrun simctl get_app_container <UDID> com.companyname.silktrianglesample`.

### 9.4 Mac Catalyst
```powershell
.\scripts\validate-artifacts.ps1 -Platform MacCatalyst -Configuration Release
```
- Resolves `SilkTriangleSample.app` bundle and verifies `Contents/Info.plist`.
- **Strict Policy:** Does not launch the executable during the validation step.

---

## 10. Running Tests Locally

### 10.1 Run Artifact Discovery Tests (Host Independent)
Does not require Appium or connected devices:
```powershell
dotnet test games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj `
  --configuration Release `
  --filter "Category=Artifact"
```

### 10.2 Run Platform UI Tests
Requires running Appium server and target platform driver:

**Windows:**
```powershell
# 1. Build sample
dotnet build games/3D/Orbit3D.Games.slnx --configuration Release -f net10.0-windows10.0.19041.0

# 2. Run Windows UI tests
dotnet test games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj `
  --configuration Release `
  --no-build `
  --filter "Platform=Windows"
```

**Android:**
```powershell
# 1. Build sample
dotnet build games/3D/Orbit3D.Games.slnx --configuration Release -f net10.0-android

# 2. Run Android UI tests
dotnet test games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj `
  --configuration Release `
  --no-build `
  --filter "Platform=Android"
```

**iOS:**
```bash
# 1. Build sample
dotnet build games/3D/Orbit3D.Games.slnx --configuration Release -f net10.0-ios

# 2. Run iOS UI tests
dotnet test games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj \
  --configuration Release \
  --no-build \
  --filter "Platform=iOS"
```

---

## 11. GitHub Actions CI Matrix Integration

Workflow `.github/workflows/dotnet-windows.yml` includes the `sample-3d-validation` matrix job:

```yaml
sample-3d-validation:
  name: 3D Sample Validation (${{ matrix.platform }})
  runs-on: ${{ matrix.os }}
  strategy:
    fail-fast: false
    matrix:
      include:
        - platform: Windows
          os: windows-latest
          target: net10.0-windows10.0.19041.0
          workloads: maui-windows
          enable-ui-tests: true
        - platform: Android
          os: ubuntu-latest
          target: net10.0-android
          workloads: android
          enable-ui-tests: false
        - platform: iOS
          os: macos-15
          target: net10.0-ios
          workloads: ios
          enable-ui-tests: true
        - platform: MacCatalyst
          os: macos-15
          target: net10.0-maccatalyst
          workloads: maccatalyst
          enable-ui-tests: false
```

- **Matrix Workloads:** Set up using `with: workloads: ${{ matrix.workloads }}`.
- **Solution Build:** `dotnet build games/3D/Orbit3D.Games.slnx --configuration Release -f ${{ matrix.target }}`.
- **Step Sequence:**
  1. Checkout
  2. Platform prerequisites (Java, Xcode, simulators)
  3. Setup .NET with matrix workload
  4. Build solution in Release
  5. Validate build artifact
  6. Execute UI tests (if enabled for target)
  7. Publish test results (.trx) and diagnostics artifacts (`if: always()`).

---

## 12. Diagnostics and Artifact Retention

- **Screenshots:** Stored under `artifacts/uitests/<Platform>/screenshots/`.
- **Failure Dumps:** On assertion failure, `DiagnosticsCollector` saves:
  - Screenshot: `<TestName>_<timestamp>.png`
  - Page Source XML: `<TestName>_<timestamp>_pagesource.xml`
  - Exception Details: `<TestName>_<timestamp>_error.txt`
- **CI Retention:** GitHub Actions uploads all artifacts under `artifacts/uitests/` and `appium.log` on failure.

---

## 13. Local Validation Status and Limitations

### 13.1 Executed and Validated Locally
- **Test Project Build:** `SilkTriangleSample.UITests.csproj` targets `net10.0` with xUnit v3 4.0.2 and Appium.WebDriver 9.0.0. Compiles cleanly with 0 warnings and 0 errors.
- **Solution Integration:** `games/3D/Orbit3D.Games.slnx` builds completely in Release mode for `net10.0-windows10.0.19041.0`.
- **Deterministic Artifact Discovery Tests:** 5 passed, 0 failed via `dotnet test` and xUnit v3 in-process runner (`dotnet run`).
- **Windows Packaging Validation:** `scripts/validate-artifacts.ps1 -Platform Windows` successfully validated `win-x64/SilkTriangleSample.exe`, asset presence, and staging directory creation without launching the executable.
- **2D Engine Compatibility:** `Orbit.Engine.Tests` executed and passed (2 passed, 0 failed).
- **Fast-fail Driver Diagnostics:** Verified that attempting to run UI tests without Appium running fails cleanly with actionable troubleshooting instructions.

### 13.2 Infrastructure Blockers on Local Machine
- **Android UI Execution:** Blocked locally by missing active emulator/device (`adb devices` lists no connected device) and mismatched local Android workload version against pinned `35.0.0`.
- **iOS / Mac Catalyst UI Execution:** Blocked locally because the development machine runs Windows; macOS runner with Xcode / `simctl` is required.
- **Windows Appium Driver:** The local npm Appium installation has `uiautomator2` installed, but lacks the `windows` driver (`appium driver install windows`).

