# Orbit3D 3D Sample UI Automation and Testing Infrastructure

This document is the authoritative guide for the UI testing infrastructure of `SilkTriangleSample` located in `games/3D/SilkTriangleSample.UITests/`.

---

## 1. Purpose and Architecture of `SilkTriangleSample.UITests`

`SilkTriangleSample.UITests` provides end-to-end UI automation and rendering verification for the Orbit3D MAUI 3D sample application across Android, iOS, Mac Catalyst, and Windows.

### Architectural Separation
- **Application Target:** Platform-specific TFMs (`net10.0-windows10.0.19041.0`, `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`). The real MAUI application builds independently.
- **UI Test Runner:** Pure desktop `.NET 10` (`net10.0`) test project using xUnit v3 and Appium.WebDriver. The test runner is **never** multi-targeted as mobile or Apple application packages, eliminating app-host (`UseAppHost`) and missing bundle identifier (`CFBundleIdentifier`) failures.
- **Automation Transport:** Appium server (HTTP JSONWP / W3C WebDriver protocol on `http://127.0.0.1:4723/`).
- **Platform Drivers:**
  - Windows: `windows` driver
  - iOS Simulator: `xcuitest` driver
  - Android: `uiautomator2` driver
  - Mac Catalyst: `mac2` driver
- **CI Hosts:**
  - Windows: `windows-latest`
  - Android: `ubuntu-latest`
  - iOS & Mac Catalyst: `macos-26`
- **Artifact Validation Before Automation:** `scripts/validate-artifacts.ps1` validates genuine build artifacts on disk before any UI test or Appium session attempts to interact with the application.

It ensures that:
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
│   │   ├── IosGraphicSurfacePage.cs
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
- **GraphicSurfacePage Identifiers (Android, iOS, and Windows):**
  - `SilkGraphicsSurface`: The native accelerated graphics surface.
  - `StatusLabel`: Human-readable status indicator.
  - `DiagnosticsLabel`: Structured machine-readable diagnostic contract (`Phase=Rendering;Context=Ready;Device=Silk;Assets=Loaded;Meshes=1;Texture=1008x752;Items=1;DrawCalls=1;Triangles=20000;FPS=...;Frames=...;Viewport=...;Yaw=...;Pitch=...;Dist=...`).
- **MainPage Identifiers (Mac Catalyst fallback):**
  - `HeadlineLabel`: Main page heading ("Hello, World!").
  - `SubHeadlineLabel`: Subheading description.
  - `CounterBtn`: Interactive button.
  - `DotNetBotImage`: Application image.
- **Bounded Explicit Waits:** Avoid fixed sleeps; all element lookups and condition verifications use bounded timeouts (`WaitForElement`, `WaitForText`).

---

## 4. Required .NET SDK and Workload Configuration

The repository pins the following toolchain baseline:
- **.NET SDK:** `10.0.302` (pinned in `global.json`, roll-forward `disable`).
- **Workload Version:** `10.0.302.1` (pinned in `global.json`).
- **Apple SDK Package Release:** `26.5.10318` (pinned in `Directory.Build.props`; installed workload pack `26.5.10315/10.0.100`).
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
- Apple workload and runtime packages adhere strictly to `26.5.10318` pinned in `Directory.Build.props` (`ValidateXcodeVersion=false` and `_ValidateXcodeVersion=false` allow newer minor Xcode point releases such as Xcode 26.6 to compile against the 26.5 workload pack).
- Development and Stage 08 Apple validation use **Visual Studio Code Remote - SSH** from Windows 11 to the provisioned remote macOS host (`macOS 15.6.1`, `Xcode 26.6`) targeting the **iPhone Air — iOS 26.5 Simulator** (`com.apple.CoreSimulator.SimRuntime.iOS-26-5`).
- Hosted macOS runners (`macos-26`) with `xcode-version: '26.0'` are configured in CI.
- CI runs `sudo xcodebuild -runFirstLaunch` and provisions the iOS simulator platform runtime (`sudo xcodebuild -downloadPlatform iOS`) before booting the available iPhone simulator (`iPhone Air` preferred).
- Version pins in `global.json` and `Directory.Build.props` must not be altered arbitrarily.

---

## 6. Platform Automation Prerequisites

### 6.1 Android
- **Prerequisites:** Java 17+ (Temurin/OpenJDK), Android SDK Platform Tools (`adb`), Android emulator or physical device.
- **Appium Driver:** `uiautomator2` (`appium driver install uiautomator2`).
- **Artifact:** `com.remotecodehub.silktrianglesample-Signed.apk`.
- **UI Automation Status:** UI tests remain disabled (`enable-ui-tests: false`) until a reliable emulator execution environment is configured on hosted CI.

### 6.2 iOS Simulator (Primary Stage 08 Validation) & Optional BrowserStack
- **Primary Stage 08 Validation (VS Code Remote - SSH + macOS iPhone Air iOS 26.5 Simulator):**
  - **Prerequisites:** Remote macOS host accessed via VS Code Remote - SSH, Xcode 26.x, iOS 26.5 Simulator runtime (`iPhone Air`), Command Line Tools (`xcrun simctl`), Appium + `xcuitest` driver.
  - **Artifact:** `SilkTriangleSample.app` (`games/3D/SilkTriangleSample/bin/Release/net10.0-ios/iossimulator-x64/SilkTriangleSample.app`).
  - **Execution:** Boots `iPhone Air` (`34E134C7-25BB-4925-8A3A-65674A943E87`), exports `ORBIT3D_IOS_SIMULATOR_UDID`, `ORBIT3D_IOS_DEVICE="iPhone Air"`, `ORBIT3D_IOS_OS_VERSION="26.5"` (and optionally `ORBIT3D_IOS_WDA_URL="http://127.0.0.1:8100"` when `WebDriverAgentRunner` is pre-started), and executes `IosSampleAppTests`.
- **Optional Cloud Device Validation (BrowserStack):**
  - Retained in `AppiumDriverFactory` behind `ORBIT3D_USE_BROWSERSTACK=true`, `ORBIT3D_BROWSERSTACK_APP=bs://<app-id>`, `BROWSERSTACK_USERNAME`, and `BROWSERSTACK_ACCESS_KEY`, but no longer required for Stage 08 completion.

### 6.3 Mac Catalyst
- **Prerequisites:** macOS host, Xcode, Accessibility permissions.
- **Appium Driver:** `mac2` (`appium driver install mac2`).
- **Artifact:** `SilkTriangleSample.app` bundle.
- **Known Limitation & Policy:** Mac Catalyst does not currently implement a native Silk graphics surface (`PlatformNotSupportedException`). Startup loads `MainPage`. MacCatalyst UI automation remains disabled (`enable-ui-tests: false`) until a reliable driver and automation path is demonstrated.

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
   - **Windows Host Mechanism:** Because global npm executables on Windows are `.cmd`/`.ps1` wrappers rather than native Win32 binaries, launching Appium via `Start-Process appium` fails with `%1 is not a valid Win32 application`. Start via `cmd.exe /c` instead:
     ```powershell
     Start-Process -FilePath "cmd.exe" -ArgumentList "/c appium --address 127.0.0.1 --port 4723 --log appium.log" -WindowStyle Hidden
     ```
   - **macOS / Linux Host Mechanism:**
     ```powershell
     Start-Process -FilePath "appium" -ArgumentList "--address 127.0.0.1 --port 4723 --log appium.log"
     ```
   - **Bounded Readiness Check:** Never rely on fixed sleeps. Poll `http://127.0.0.1:4723/status` until ready (bounded timeout, e.g., 60 seconds).
4. Configure environment variables (optional overrides):
   ```powershell
   $env:ORBIT3D_APPIUM_URL = "http://127.0.0.1:4723/"
   $env:ORBIT3D_TEST_TIMEOUT_SECONDS = "30"
   $env:ORBIT3D_CAPTURE_SCREENSHOTS = "true"
   ```

---

## 8. Application Build and Artifact Contract

- **Release Configuration:** All tests and artifact validation require `Release` builds.
- **Application Build Command:**
  ```powershell
  dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj --configuration Release -f <TargetFramework>
  ```
- **Desktop Test Runner Build Command:**
  ```powershell
  dotnet build games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj --configuration Release
  ```
- **Deterministic Discovery:** `AppArtifactLocator` resolves the exact binary output from `artifacts/ci-builds/` (downloaded CI artifacts) or `games/3D/SilkTriangleSample/bin/Release/<TargetFramework>/...`.
- **Stale Binary Prevention & IPA Inspection:**
  - Validates that the artifact file, `.ipa` archive, or `.app` bundle exists.
  - Verifies file size > 0 and calculates SHA256 content hash.
  - For iOS `.ipa` files (`InspectIosIpaArchive`), verifies valid ZIP structure, `Payload/*.app/Info.plist`, `CFBundleIdentifier == com.remotecodehub.silktrianglesample`, non-empty Mach-O executable, and `embedded.mobileprovision`.
  - Throws `FileNotFoundException` or `InvalidDataException` with actionable instructions if missing, corrupted, or stale.
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
- Resolves `com.remotecodehub.silktrianglesample-Signed.apk`.
- Installs via `adb install -r <apkPath>`.
- Verifies package via `adb shell pm list packages com.remotecodehub.silktrianglesample`.

### 9.3 iOS (Physical-Device `.ipa` or Simulator `.app`)
```powershell
# Validate downloaded CI physical-device .ipa archive (works on Windows and macOS):
.\scripts\validate-artifacts.ps1 -Platform iOS -Configuration Release -ArtifactDirectory "artifacts/ci-builds/silk-triangle-sample-ios"

# Or validate Simulator .app bundle on macOS:
.\scripts\validate-artifacts.ps1 -Platform iOS -Configuration Release -SimulatorId "<UDID>"
```
- When an `.ipa` is present (or `-ArtifactDirectory` points to a downloaded CI artifact), inspects the ZIP archive structure, verifies `Payload/SilkTriangleSample.app/Info.plist`, confirms `CFBundleIdentifier` matches `com.remotecodehub.silktrianglesample`, verifies the Mach-O executable and `embedded.mobileprovision`, and calculates SHA-256.
- When validating a `.app` simulator bundle on macOS: boots simulator (`xcrun simctl boot <UDID>`), installs app (`xcrun simctl install <UDID> <appPath>`), and confirms installation (`xcrun simctl get_app_container <UDID> com.remotecodehub.silktrianglesample`).

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
dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj --configuration Release -f net10.0-windows10.0.19041.0

# 2. Build test runner
dotnet build games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj --configuration Release

# 3. Run Windows UI tests
dotnet test games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj `
  --configuration Release `
  --no-build `
  --filter "Platform=Windows"
```

**Android:**
```powershell
# 1. Build sample
dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj --configuration Release -f net10.0-android

# 2. Run Android UI tests (when emulator is configured)
dotnet test games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj `
  --configuration Release `
  --filter "Platform=Android"
```

**iOS:**
```bash
# 1. Build sample
dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj --configuration Release -f net10.0-ios

# 2. Run iOS UI tests
dotnet test games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj \
  --configuration Release \
  --filter "Platform=iOS"
```

---

## 11. GitHub Actions CI Workflows

### 11.1 Dedicated Multiplatform Build Workflow (`.github/workflows/silk-triangle-sample-build.yml`)
This workflow builds and publishes all four application targets (`Android`, `iOS`, `MacCatalyst`, `Windows`) in `Release` configuration (`fail-fast: false`) with 14-day artifact retention:
- `silk-triangle-sample-android` (`net10.0-android`, `android-arm64` on `ubuntu-latest`)
- `silk-triangle-sample-ios` (`net10.0-ios` on `macos-26`: builds signed `ios-arm64` `.ipa` when `APPLE_CERTIFICATE_P12_BASE64`, `APPLE_CERTIFICATE_PASSWORD`, and `APPLE_PROVISIONING_PROFILE_BASE64` secrets are configured, or builds and archives the `iossimulator-x64` `.app` bundle `SilkTriangleSample-iossimulator.app.zip` when physical-device signing secrets are absent)
- `silk-triangle-sample-maccatalyst` (`net10.0-maccatalyst`, `maccatalyst-x64` `.app` on `macos-26`)
- `silk-triangle-sample-windows` (`net10.0-windows10.0.19041.0`, `win-x64` on `windows-latest`)

Download and verify artifacts locally on Windows using:
```powershell
.\scripts\download-build-artifacts.ps1 -RunId <run-id> -Platform all
```

### 11.2 CI Validation Matrix (`.github/workflows/dotnet-windows.yml`)
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
          os: macos-26
          target: net10.0-ios
          workloads: ios
          enable-ui-tests: true
        - platform: MacCatalyst
          os: macos-26
          target: net10.0-maccatalyst
          workloads: maccatalyst
          enable-ui-tests: false
```

- **Step Sequence:**
  1. Checkout repository
  2. Platform prerequisites (Java JDK for Android, Xcode 26.0 and first-launch/simulator setup for iOS/MacCatalyst)
  3. Prepare iOS Simulator (deterministic query preferring `iPhone Air`, boot, and UDID export to `GITHUB_ENV`)
  4. Setup .NET `10.0.302` with matrix workload (`dotnet workload install maui ${{ matrix.workloads }} --version 10.0.302.1`)
  5. Build real sample application in Release (`dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj`)
  6. Validate application build artifact (`validate-artifacts.ps1`)
  7. Setup Node.js and Appium + Platform driver
  8. Start Appium server with Windows-compatible process launch (`cmd.exe /c`) and poll status readiness check
  9. Build UI test runner (`dotnet build games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj`)
  10. Run UI tests with test discovery validation (`--filter "Platform=${{ matrix.platform }}"`)
  11. Publish test results (.trx) and diagnostics artifacts (`if: always()`).

---

## 12. Diagnostics and Artifact Retention

- **Screenshots:** Stored under `artifacts/uitests/<Platform>/screenshots/` and `artifacts/apple/stage-08/screenshots/`.
- **Failure Dumps:** On assertion failure, `DiagnosticsCollector` saves:
  - Screenshot: `<TestName>_<timestamp>.png`
  - Page Source XML: `<TestName>_<timestamp>_pagesource.xml`
  - Exception Details: `<TestName>_<timestamp>_error.txt`
- **CI Retention:** GitHub Actions uploads all artifacts under `artifacts/uitests/` and `appium.log` on failure.

---

## 13. Validation Status and Limitations

### 13.1 Executed and Validated on Remote macOS Host via VS Code Remote - SSH
- **Test Project Build:** `SilkTriangleSample.UITests.csproj` targets `net10.0` with xUnit v3 4.0.2 and Appium.WebDriver 9.0.0. Compiles cleanly with 0 warnings and 0 errors.
- **Apple Sample Builds:** `SilkTriangleSample` builds in `Release` mode with 0 warnings and 0 errors for `net10.0-ios` (`iossimulator-x64`) and `net10.0-maccatalyst` (`maccatalyst-x64`).
- **Unit & Artifact Discovery Tests:**
  - `Orbit3D.Engine.Tests`: 45 passed, 0 failed (`net10.0`).
  - `Orbit.Engine.Tests`: 2 passed, 0 failed (`net10.0`).
  - `ArtifactDiscoveryTests` (`Category=Artifact`): 9 passed, 0 failed (`net10.0`).
- **iOS Simulator UI Automation (`IosSampleAppTests` on `iPhone Air — iOS 26.5`):** 7 passed, 0 failed (`AppLaunchesWithoutCrashing`, `GraphicsSurfaceIsInitialized`, `SceneRendersMeshesAndTexturesWithPositiveMetrics`, `CameraOrbitInteractionUpdatesViewWithoutStoppingRenderLoop`, `OrientationChangeResizesSurfaceAndContinuesRendering`, `LifecycleBackgroundAndForegroundRecoversRendering`, `NoUnhandledExceptionsInDiagnostics`).

### 13.2 Platform Constraints
- **Mac Catalyst:** `SilkGraphicsSurface` intentionally throws `PlatformNotSupportedException` until Stage 09 implements the native Mac Catalyst surface; `App.xaml.cs` falls back to `MainPage`.
- **Android / Windows UI Tests on macOS Host:** Validated via GitHub Actions CI (`windows-latest` and `ubuntu-latest`).

