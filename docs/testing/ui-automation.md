# Orbit3D 3D Sample UI Automation and Testing Infrastructure

This document is the authoritative guide for the UI testing infrastructure of `SilkTriangleSample` located in `games/3D/SilkTriangleSample.UITests/`.

---

## 1. Purpose and Architecture of `SilkTriangleSample.UITests`

`SilkTriangleSample.UITests` provides end-to-end UI automation and rendering verification for the Orbit3D MAUI 3D sample application across Android, iOS, Mac Catalyst, and Windows.

### Architectural Separation
- **Application Target:** Platform-specific TFMs (`net11.0-windows10.0.19041.0`, `net11.0-android`, `net11.0-ios`, `net11.0-maccatalyst`). The real MAUI application builds independently.
- **UI Test Runner:** Pure desktop `.NET 10` (`net11.0`) test project using xUnit v3 and Appium.WebDriver. The test runner is **never** multi-targeted as mobile or Apple application packages, eliminating app-host (`UseAppHost`) and missing bundle identifier (`CFBundleIdentifier`) failures.
- **Automation Transport:** Appium server (HTTP JSONWP / W3C WebDriver protocol on `http://127.0.0.1:4723/`).
- **Platform Drivers:**
  - Windows: `wincore` driver (`automationName: Wincore`)
  - iOS Simulator: `xcuitest` driver
  - Android: `uiautomator2` driver
  - Mac Catalyst: `mac2` driver
- **Authoritative Development & Test Execution Topology:**

| Platform | Implementation | Build and Execution | Appium UI Tests |
|---|---|---|---|
| **Android** | Windows local VS Code | Windows local (device/emulator) | Windows local (`uiautomator2`) |
| **Windows** | Windows local VS Code | Windows local | Windows local (`wincore`) |
| **iOS** | Windows VS Code via Remote - SSH | Remote macOS `27.2` workspace | Remote macOS `27.2` workspace (`xcuitest`) |
| **Mac Catalyst** | Windows VS Code via Remote - SSH | Remote macOS `27.2` workspace | Remote macOS `27.2` workspace (`mac2`) |

- **Real Local/Remote Builds:** Appium tests must execute against genuine application builds produced directly in the corresponding development workspace (not downloaded CI artifacts).

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
- **.NET SDK:** `11.0.100` (pinned in [`global.json`](../../global.json), `rollForward: disable`).
- **MAUI Version:** `11.0.10` in [`Directory.Build.props`](../../Directory.Build.props).
- **macOS Host:** `macOS 27.2` with `.NET SDK 11.0.100` and the Apple/MAUI toolchain (confirm exact Xcode and workload versions on the remote host).

Workloads required per platform:
- Windows: `maui-windows`
- Android: `android`
- iOS: `ios`
- Mac Catalyst: `maccatalyst`

---

## 5. Apple SDK and Xcode Compatibility Policy

- [`Directory.Build.props`](../../Directory.Build.props) sets `ValidateXcodeVersion=false` and `_ValidateXcodeVersion=false` alongside `MauiVersion`.
- All iOS and Mac Catalyst development, builds, execution, and Appium UI automation use **Visual Studio Code Remote - SSH** from Windows to the provisioned **macOS `27.2`** host.
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
  - **Artifact:** `SilkTriangleSample.app` (`games/3D/SilkTriangleSample/bin/Release/net11.0-ios/iossimulator-x64/SilkTriangleSample.app`).
  - **Execution:** Boots `iPhone Air` (`34E134C7-25BB-4925-8A3A-65674A943E87`), exports `ORBIT3D_IOS_SIMULATOR_UDID`, `ORBIT3D_IOS_DEVICE="iPhone Air"`, `ORBIT3D_IOS_OS_VERSION="26.5"` (and optionally `ORBIT3D_IOS_WDA_URL="http://127.0.0.1:8100"` when `WebDriverAgentRunner` is pre-started), and executes `IosSampleAppTests`.
- **Optional Cloud Device Validation (BrowserStack):**
  - Retained in `AppiumDriverFactory` behind `ORBIT3D_USE_BROWSERSTACK=true`, `ORBIT3D_BROWSERSTACK_APP=bs://<app-id>`, `BROWSERSTACK_USERNAME`, and `BROWSERSTACK_ACCESS_KEY`, but no longer required for Stage 08 completion.

### 6.3 Mac Catalyst
- **Prerequisites:** Remote `macOS 27.2` host via VS Code Remote - SSH, Xcode, Accessibility permissions (for Appium `mac2`), active display power state (`caffeinate -u` when running over SSH so `CADisplayLink` and `CAOpenGLLayer` callbacks fire).
- **Appium Driver:** `mac2` (`appium driver install mac2`).
- **Artifact:** `SilkTriangleSample.app` bundle (`games/3D/SilkTriangleSample/bin/<Config>/net11.0-maccatalyst/maccatalyst-x64/SilkTriangleSample.app`).
- **Status & Policy:** Stage 09 implements the native Mac Catalyst Silk graphics surface (`SilkMacSurfaceView` + `CAOpenGLLayer` OpenGL 4.1 core profile) and launches `GraphicSurfacePage` on startup. `MacCatalystGraphicSurfacePage` and `MacCatalystSampleAppTests` validate surface visibility, context creation, asset loading, and frame rendering directly on macOS via Appium `mac2`.

### 6.4 Windows
- **Prerequisites:** Windows 10/11 x64, interactive unlocked desktop session (e.g. `SessionId=2`, not Session 0), Developer Mode enabled, Windows App SDK Runtime.
- **Appium Driver:** `wincore@4.1.2` on Appium `4.0.0` (`automationName: Wincore`, numeric `ms:waitForAppLaunch: 15`; `WinAppDriver.exe` is not required).
- **Artifact:** `win-x64/AnimationSample.exe` / `win-x64/SilkTriangleSample.exe` (unpackaged WinUI 3 distribution).
- **Notes:** `SilkWindowsSurfaceView` exposes `SilkSurfaceAutomationPeer` (`OnCreateAutomationPeer` + `IsOffscreenCore`) for UIA visibility. In `AnimationSample.UITests`, `windows: invoke` is used for deterministic button activation and `ORBIT3D_SILK_CAPTURE_DIR` provides a `gl.ReadPixels` screenshot fallback if `WincoreServer` `getScreenshot` fails when `ForegroundWindow=0`.

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

- **Real Workspace Builds:** Build the sample directly in the corresponding platform workspace (`Windows` local for Windows/Android; `macOS 27.2` Remote - SSH for iOS/Mac Catalyst).
- **Application Build Command:**
  ```powershell
  dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj --configuration Release -f <TargetFramework>
  ```
- **Desktop Test Runner Build Command:**
  ```powershell
  dotnet build games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj --configuration Release
  ```
- **Deterministic Discovery:** `AppArtifactLocator` resolves the exact binary output from `games/3D/SilkTriangleSample/bin/<Config>/<TargetFramework>/...`.
- **Stale Binary Prevention:**
  - Validates that the artifact file, `.ipa` archive, or `.app` bundle exists, has non-zero size, and matches the expected bundle ID (`com.remotecodehub.silktrianglesample`).
  - Throws `FileNotFoundException` or `InvalidDataException` with actionable instructions if missing, corrupted, or stale.
  - Pre-installed or manually placed applications are never accepted silently.

---

## 9. Platform Installation and Execution Prerequisites

- **Windows (local Windows workspace):** Build `net11.0-windows10.0.19041.0` and launch/test `win-x64/SilkTriangleSample.exe` via Appium `windows`.
- **Android (local Windows workspace):** Build `net11.0-android`, verify device/emulator connectivity (`adb devices`), install/run via `adb` or Appium `uiautomator2`.
- **iOS Simulator (remote macOS `27.2` workspace):** Build `net11.0-ios`, boot target simulator (`xcrun simctl boot <UDID>`), and run Appium `xcuitest` tests against `SilkTriangleSample.app`.
- **Mac Catalyst (remote macOS `27.2` workspace):** Build `net11.0-maccatalyst` and run Appium `mac2` tests against `SilkTriangleSample.app`.

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
dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj --configuration Release -f net11.0-windows10.0.19041.0

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
dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj --configuration Release -f net11.0-android

# 2. Run Android UI tests (when emulator is configured)
dotnet test games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj `
  --configuration Release `
  --filter "Platform=Android"
```

**iOS:**
```bash
# 1. Build sample
dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj --configuration Release -f net11.0-ios

# 2. Run iOS UI tests
dotnet test games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj \
  --configuration Release \
  --filter "Platform=iOS"
```

---

## 11. GitHub Actions CI Workflows (Helper Only, Never Validation Authority)

GitHub Actions workflows are supplementary helpers for builds, diagnostics, and packaging. They are **never** the authoritative validation mechanism for any platform or milestone, and CI artifacts must not be downloaded to replace local or Remote - SSH application execution:
- **`.github/workflows/silk-triangle-sample-build.yml`**: Supplementary multiplatform build/packaging workflow.
- **`.github/workflows/dotnet-windows.yml`** / **`.github/workflows/dotnet-macos.yml`**: Supplementary build and automated test workflows.

Agents must never wait for CI or use CI results as a substitute for executing applications and UI tests on real Windows, Android, iOS Simulator, and Mac Catalyst targets.

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
- **Test Project Build:** `SilkTriangleSample.UITests.csproj` targets `net11.0` with xUnit v3 4.0.2 and Appium.WebDriver 9.0.0. Compiles cleanly with 0 warnings and 0 errors.
- **Apple Sample Builds:** `SilkTriangleSample` builds in `Release` mode with 0 warnings and 0 errors for `net11.0-ios` (`iossimulator-x64`) and `net11.0-maccatalyst` (`maccatalyst-x64`).
- **Unit & Artifact Discovery Tests:**
  - `Orbit3D.Engine.Tests`: 45 passed, 0 failed (`net11.0`).
  - `Orbit.Engine.Tests`: 2 passed, 0 failed (`net11.0`).
  - `ArtifactDiscoveryTests` (`Category=Artifact`): 9 passed, 0 failed (`net11.0`).
- **iOS Simulator UI Automation (`IosSampleAppTests` on `iPhone Air — iOS 26.5`):** 7 passed, 0 failed (`AppLaunchesWithoutCrashing`, `GraphicsSurfaceIsInitialized`, `SceneRendersMeshesAndTexturesWithPositiveMetrics`, `CameraOrbitInteractionUpdatesViewWithoutStoppingRenderLoop`, `OrientationChangeResizesSurfaceAndContinuesRendering`, `LifecycleBackgroundAndForegroundRecoversRendering`, `NoUnhandledExceptionsInDiagnostics`).

### 13.2 Platform Constraints
- **Mac Catalyst:** Requires macOS Accessibility permissions for Appium `mac2` and an active display power state (`caffeinate -u` over SSH) for `CADisplayLink`/`CAOpenGLLayer` frame callbacks.
- **Android / Windows UI Tests:** Executed locally on the Windows workstation against a connected Android device/emulator and Windows desktop, never via CI substitution.

### 13.3 Stage 11 Phase 1: `AnimationSample.UITests`
- **Dedicated Project:** `games/3D/AnimationSample.UITests/` tests `games/3D/AnimationSample/` (`com.remotecodehub.animationsample`) independently of `SilkTriangleSample.UITests/`.
- **Android (`Platform=Android`, `uiautomator2`):** Passed 5/5 on `emulator-5554` (`AppLaunchesAndInitialUiIsVisible`, `GraphicsSurfaceAndContextInitialize`, `IdleFbxLoadsSkeletonSkinningAndClips`, `AnimationPlaysTimeProgressesAndControlsWork`, `ContextRecreationPreservesAnimationAndCapturesScreenshot`). Note: MAUI `AutomationId` values resolve via `resource-id` (`com.remotecodehub.animationsample:id/<AutomationId>`) on Android, and diagnostics label updates are throttled (`600ms`) with `waitForIdleTimeout = 0` so continuous 3D rendering does not stall UiAutomator2 queries.
- **Artifact Discovery (`Category=Artifact`):** Passed 4/4 on Windows; on macOS, iOS and MacCatalyst artifact checks pass (2/4), while Windows/Android checks fail when those platforms have not been built locally.
- **Windows (`Platform=Windows`):** Requires `WinAppDriver.exe` (or an interactive desktop session for `wincore`); currently blocked on container/headless host environments lacking `WinAppDriver.exe`.

### 13.4 Stage 11 Phase 2: `AnimationSample.UITests` on Apple (`iOS` & `MacCatalyst`)
- **iOS (`Platform=iOS`, `xcuitest`):** Passed **6/6** on `iPhone 17 — iOS 27.2` (`62EB87F6-E890-4118-AAA2-5864621757BF`, `iossimulator-arm64`), verifying `Idle.fbx` import (`88` bones, `240` nodes, `16` draw calls, `32,888` triangles), advancing animation time and `PoseHash`, playback controls (`Pause`/`Resume`/`Speed`/`Loop`/`Stop`/`Play`), 2 consecutive context recreation cycles, and screenshot capture.
- **Mac Catalyst (`Platform=MacCatalyst`, `mac2`):** Passed **7/7** on `macOS 27.2.1` (`Apple M4`, `caffeinate -u`), verifying all shared animation tests plus `ConsecutiveContextRecreationsPreserveAnimationAndResumeRendering` and `WindowResizeUpdatesViewportAndContinuesRendering` (using `mac2` window-corner drag, as `mac2` does not implement W3C `window/rect` and `System Events` AppleScript requires interactive Automation TCC approval).
- **Evidence:** Screenshots, TRX reports, and logs are archived under `artifacts/stage-11/phase-2/{ios,maccatalyst}/`.

