# Apple iOS Real-Device (BrowserStack) and Simulator UI Testing Guideline

## 1. Decision and Validation Strategy

Orbit3D is developed on a Windows 11 environment without local macOS hardware, Xcode, or physical Apple devices. For **Stage 08**, the **primary validation path** for iOS is **CI-compiled physical-device (`ios-arm64`) `.ipa` artifacts executed on real iPhone hardware hosted by BrowserStack**:

```text
Windows 11 Local Orchestration
  -> GitHub Actions macOS Runner (.github/workflows/silk-triangle-sample-build.yml)
  -> Signed ios-arm64 IPA Artifact (silk-triangle-sample-ios)
  -> Local Artifact Download & Verification (scripts/download-build-artifacts.ps1)
  -> BrowserStack Upload (bs://<app-id>)
  -> Real iPhone Execution (Appium / BrowserStack App Automate + MCP Tools)
  -> Screenshot, Video, and Device Log Retrieval (artifacts/browserstack/stage-08/)
  -> Visual Screenshot & Log Inspection
```

iOS Simulator testing (on a GitHub Actions macOS runner or optional remote macOS host) remains documented below as an **optional development or debugging technique**, while **BrowserStack real-device execution and visual screenshot inspection** is the mandatory acceptance gate for Stage 08.

---

## 2. Scope and Intent

All iOS validation must run the **actual `SilkTriangleSample` MAUI application** (`com.remotecodehub.silktrianglesample`) and the **actual `Orbit3D.Graphics.Silk` iOS OpenGL ES 3.0 (`CAEAGLLayer` + `EAGLContext`) implementation**. Tests must never substitute a mock renderer, bypass native surface initialization, or report success using synthetic values.

For Orbit3D Stage 08 graphics changes, automated UI tests (`IosSampleAppTests`) and visual inspection must verify:

1. **Application Launch (`Test01_ApplicationLaunch`)**: The `.ipa` installs and launches on a physical iPhone without crashing or hanging on the splash screen.
2. **Graphics Surface Initialization (`Test02_GraphicsSurfaceInitialization`)**: `SilkGraphicsSurface` (`SilkIosSurfaceView`) creates its `CAEAGLLayer`, `EAGLContext` (OpenGL ES 3.0), FBO, color renderbuffer (`RGBA8`), and depth renderbuffer (`DepthComponent24`), reporting `Context=Ready;Device=Silk`.
3. **Scene and Asset Loading (`Test03_SceneAndAssetLoading`)**: The Toyota GR Yaris GLB (`toyota-gazoo-racing-wrt-gr-yaris-1-10.glb`, 1 mesh, 20,000 triangles) and CoreGraphics-decoded sidecar texture (`toyota-gazoo-racing-wrt-gr-yaris-1-10.png`, `1008x752` RGBA) load and upload to GPU buffers (`Assets=Loaded;Meshes=1;Texture=1008x752`).
4. **Active Rendering Loop (`Test04_ActiveRenderingLoop`)**: Frame counter (`Frames`) advances over time with `DrawCalls >= 1`, `Triangles >= 20000`, and `FPS > 0`.
5. **Camera Touch Interaction (`Test05_CameraInteraction`)**: W3C touch pan gestures on `SilkGraphicsSurface` update `CameraOrbitController` (`Yaw`/`Pitch`) while rendering continues without GPU errors.
6. **Orientation & Viewport Resize (`Test06_OrientationAndViewportResize`)**: Rotating between `PORTRAIT` and `LANDSCAPE` reallocates `CAEAGLLayer` renderbuffer storage to the new pixel dimensions (`Viewport`) and continues rendering cleanly.
7. **Application Lifecycle (`Test07_ApplicationLifecycle`)**: Backgrounding (`RunAppInBackground`) suspends the OpenGL ES context and releases context-bound GPU resources while the context is current; returning to the foreground recreates the context/renderbuffers and rebuilds GPU resources from retained CPU assets (`IAssetCache`), resuming rendering.

A passing test or build is evidence only for the assertions it actually makes. Compilation, installation, or launch alone is never sufficient to mark Stage 08 complete without visual verification of captured physical-device screenshots.

---

## 3. Toolchain and Version Policy

- The repository root `global.json` and `Directory.Build.props` are authoritative for the .NET SDK, workloads, and pinned Apple package versions:
  - **.NET SDK**: `10.0.100` (`global.json`, `rollForward: latestPatch`)
  - **Workload Version**: `10.0.100` (`global.json`)
  - **Apple SDK Package Version (`MicrosoftiOSSdkPackageVersion`)**: `26.5.10318` (`Directory.Build.props`)
  - **Android SDK Package Version (`MicrosoftAndroidSdkPackageVersion`)**: `35.0.0` (`Directory.Build.props`)
  - **Windows Desktop App Package Version (`MicrosoftWindowsDesktopAppPackageVersion`)**: `10.0.26100.0` (`Directory.Build.props`)
- Do not change repository pins, silently install a different Apple workload release, or upgrade package versions merely to make a runner pass.
- If a hosted runner or environment lacks required toolchain compatibility or signing secrets, report it as an explicit environment/configuration blocker.

---

## 4. Dedicated Multiplatform CI Build Workflow (`.github/workflows/silk-triangle-sample-build.yml`)

The workflow `.github/workflows/silk-triangle-sample-build.yml` builds and publishes all four supported `SilkTriangleSample` platform targets in an isolated matrix (`fail-fast: false`) with 14-day artifact retention:

| Platform | Target Framework | Runtime Identifier | Runner | Published Artifact Name | Primary Package |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Android** | `net10.0-android` | `android-arm64` | `ubuntu-latest` | `silk-triangle-sample-android` | `com.remotecodehub.silktrianglesample-Signed.apk` |
| **iOS** | `net10.0-ios` | `ios-arm64` | `macos-26` | `silk-triangle-sample-ios` | `SilkTriangleSample.ipa` |
| **MacCatalyst** | `net10.0-maccatalyst` | `maccatalyst-x64` | `macos-26` | `silk-triangle-sample-maccatalyst` | `SilkTriangleSample.app` |
| **Windows** | `net10.0-windows10.0.19041.0` | `win-x64` | `windows-latest` | `silk-triangle-sample-windows` | `SilkTriangleSample.exe` + runtime payload |

Each artifact includes `artifact-metadata.json` containing git commit SHA, branch, workflow run ID, build configuration (`Release`), target framework, runtime identifier, file list, sizes, and SHA-256 checksums.

### 4.1 Required GitHub Secrets for iOS Physical-Device (`.ipa`) Signing

Physical iOS devices on BrowserStack require a signed `ios-arm64` `.ipa` archive (`BuildIpa=true`, `ArchiveOnBuild=true`). Configure the following repository secrets in GitHub (`Settings -> Secrets and variables -> Actions`):

| Secret Name | Required | Description |
| :--- | :--- | :--- |
| `APPLE_CERTIFICATE_P12_BASE64` | **Yes** | Base64-encoded Apple Development or Ad-Hoc Distribution `.p12` certificate (`[Convert]::ToBase64String([IO.File]::ReadAllBytes("cert.p12"))`). |
| `APPLE_CERTIFICATE_PASSWORD` | **Yes** | Password protecting the `.p12` certificate file. |
| `APPLE_PROVISIONING_PROFILE_BASE64` | **Yes** | Base64-encoded `.mobileprovision` file matching `com.remotecodehub.silktrianglesample` (or wildcard `*`). |
| `APPLE_SIGNING_IDENTITY` | Optional | Explicit `CodesignKey` override (auto-detected from the temporary keychain when omitted). |
| `APPLE_PROVISIONING_PROFILE_NAME` | Optional | Explicit `CodesignProvision` UUID or profile name override (auto-detected from the `.mobileprovision` plist when omitted). |

When these secrets are present, the iOS job imports the certificate into a temporary macOS keychain, installs the provisioning profile into `~/Library/MobileDevice/Provisioning Profiles/`, and publishes `SilkTriangleSample.ipa` for `ios-arm64`. If the secrets are absent, the iOS job stops with an actionable error message explaining which secrets must be configured.

### 4.2 Triggering the Workflow and Downloading Artifacts Locally

 From the Windows development machine using the GitHub CLI (`gh`):

```powershell
# 1. Trigger the build workflow on main (after pushing silk-triangle-sample-build.yml)
gh workflow run silk-triangle-sample-build.yml -R weslleymurdock/orbit3d --ref main

# 2. Watch the workflow run to completion
gh run watch <run-id> -R weslleymurdock/orbit3d --exit-status

# 3. Download all 4 platform artifacts (or -Platform ios) and verify SHA-256 + IPA structure
.\scripts\download-build-artifacts.ps1 -RunId <run-id> -Platform all

# 4. Run local artifact validation on the downloaded iOS IPA
.\scripts\validate-artifacts.ps1 -Platform iOS -Configuration Release `
  -ArtifactDirectory "artifacts/ci-builds/silk-triangle-sample-ios"
```

Downloaded artifacts are organized under:
- `artifacts/ci-builds/silk-triangle-sample-android/`
- `artifacts/ci-builds/silk-triangle-sample-ios/`
- `artifacts/ci-builds/silk-triangle-sample-maccatalyst/`
- `artifacts/ci-builds/silk-triangle-sample-windows/`
- `artifacts/ci-builds/download-manifest.json`

---

## 5. BrowserStack Real-Device iOS Validation

### 5.1 Environment Configuration

Ensure BrowserStack credentials are set in the local Windows environment (never hardcode or print secret values):

```powershell
# Required BrowserStack credentials (already configured in user environment)
# $env:BROWSERSTACK_USERNAME
# $env:BROWSERSTACK_ACCESS_KEY

# Target BrowserStack App Automate execution
$env:ORBIT3D_USE_BROWSERSTACK = "true"
$env:ORBIT3D_BROWSERSTACK_APP = "bs://<uploaded-app-id>"
$env:ORBIT3D_IOS_DEVICE = "iPhone 15"
$env:ORBIT3D_IOS_OS_VERSION = "17"
$env:ORBIT3D_CAPTURE_SCREENSHOTS = "true"
```

### 5.2 Uploading the `.ipa` and Running Automated UI Tests

1. **Upload the signed `.ipa`** to BrowserStack App Automate (via BrowserStack REST API `https://api-cloud.browserstack.com/app-automate/upload` or BrowserStack MCP tools `setupBrowserStackAppAutomateTests` / `takeAppScreenshot`) to obtain a `bs://<app-id>` URL.
2. **Run the iOS UI test suite** from Windows against BrowserStack App Automate (`https://hub-cloud.browserstack.com/wd/hub/`):
   ```powershell
   dotnet test games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj `
     --configuration Release `
     --filter "Platform=iOS"
   ```
3. **Collect Evidence via BrowserStack MCP Tools**:
   - `listBuildId` / `getBuildId` and `listSessions` / `listTestIds` to locate the build and session IDs.
   - `fetchAutomationScreenshots` and `takeAppScreenshot` to retrieve device screenshots.
   - `getFailureLogs` (`sessionLogs`, `deviceLogs`, `appiumLogs`, `crashLogs`, `video`) to inspect runtime logs and video.
   - Store all collected evidence under `artifacts/browserstack/stage-08/` (`screenshots/`, `logs/`, `session-summary.json`).

### 5.3 Visual Inspection Criteria

Every captured screenshot in `artifacts/browserstack/stage-08/screenshots/` must be visually inspected to confirm:
- The application is not showing a black/blank screen, crash dialog, or splash screen.
- The 3D Toyota GR Yaris mesh is visibly rendered with proper perspective projection, depth ordering, back-face culling, and CoreGraphics-decoded sidecar texture.
- The bottom diagnostics overlay displays `Phase=Rendering;Context=Ready;Device=Silk;Assets=Loaded;Meshes=1;Texture=1008x752;Items=1;DrawCalls=1;Triangles=20000;...`.
- Camera orbit gestures visibly change the viewing angle of the 3D model.
- Portrait/landscape rotation resizes the viewport without clipping or distortion.
- Background/foreground resume restores the rendered 3D model without missing textures or black geometry.

---

## 6. Optional iOS Simulator Testing (Development & Debugging)

When a compatible macOS runner or remote macOS host with Xcode and a matching iOS Simulator runtime is available, simulator builds (`iossimulator-arm64` / `iossimulator-x64`) can be used for fast development debugging:

1. Select the repository-pinned .NET SDK (`10.0.100`) and Apple workload (`26.5.10318`).
2. Verify that `xcrun simctl list runtimes` includes an iOS Simulator runtime matching the installed Xcode SDK build.
3. Build `SilkTriangleSample.csproj` for Simulator (`-f net10.0-ios -c Release`).
4. Validate installation via `.\scripts\validate-artifacts.ps1 -Platform iOS -Configuration Release -SimulatorId "<UDID>"`.
5. Run `dotnet test games/3D/SilkTriangleSample.UITests/SilkTriangleSample.UITests.csproj -c Release --filter "Platform=iOS"` against a local Appium `xcuitest` server (`ORBIT3D_USE_BROWSERSTACK=false`).

> [!IMPORTANT]
> Simulator execution exercises Apple's software/translated OpenGL ES layer and does not prove physical-device code signing, hardware GPU behavior, or Stage 08 completion.

---

## 7. Troubleshooting

### 7.1 Missing Apple Code-Signing Secrets in CI
- **Symptom**: The `Build iOS` job in `.github/workflows/silk-triangle-sample-build.yml` fails at `Validate Apple Signing Secrets for Physical Device IPA`.
- **Resolution**: Add `APPLE_CERTIFICATE_P12_BASE64`, `APPLE_CERTIFICATE_PASSWORD`, and `APPLE_PROVISIONING_PROFILE_BASE64` to the GitHub repository's Actions secrets.

### 7.2 Provisioning Profile Bundle Identifier Mismatch
- **Symptom**: `codesign` or `dotnet publish -f net10.0-ios -r ios-arm64` fails with `No profiles for 'com.remotecodehub.silktrianglesample' were found`.
- **Resolution**: Ensure the provisioning profile in `APPLE_PROVISIONING_PROFILE_BASE64` matches `ApplicationId` (`com.remotecodehub.silktrianglesample`) or uses a wildcard App ID (`*`), and includes the signing certificate's public key.

### 7.3 Xcode / Simulator Runtime Mismatch (`actool` Failure on Simulator Builds)
- **Symptom**: Building for `iossimulator-arm64` on `macos-26` fails in `actool` with `not find any available simulator runtimes for platform iphonesimulator compatible with sdkWatch built for <build>`.
- **Resolution**: Physical-device builds (`-r ios-arm64` in `silk-triangle-sample-build.yml`) target the `iphoneos` device SDK and do not require an installed simulator runtime. For simulator builds, ensure `sudo xcodebuild -downloadPlatform iOS` installs the matching simulator runtime.

### 7.4 BrowserStack App Installation Failure
- **Symptom**: BrowserStack rejects the `.ipa` or fails to install on the physical iPhone.
- **Resolution**: Run `.\scripts\validate-artifacts.ps1 -Platform iOS -ArtifactDirectory "artifacts/ci-builds/silk-triangle-sample-ios"` to verify that `Payload/SilkTriangleSample.app/embedded.mobileprovision` is present, `CFBundleIdentifier` is `com.remotecodehub.silktrianglesample`, and the binary is compiled for `ios-arm64` (not `iossimulator`).

---

## 8. References

- [BrowserStack App Automate with Appium XCUITest](https://www.browserstack.com/docs/app-automate/appium)
- [GitHub Actions: Installing an Apple certificate on macOS runners for Xcode development](https://docs.github.com/en/actions/use-cases-and-examples/deploying/installing-an-apple-certificate-on-macos-runners-for-xcode-development)
- [Appium XCUITest Driver](https://appium.github.io/appium-xcuitest-driver/)
