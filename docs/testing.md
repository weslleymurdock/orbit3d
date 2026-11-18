# Testing and Validation

## Authoritative Testing and Validation Topology

| Platform | Implementation | Build and Execution | Appium UI Tests |
|---|---|---|---|
| **Android** | Windows local VS Code | Windows local (device/emulator) | Windows local (`uiautomator2`) |
| **Windows** | Windows local VS Code | Windows local | Windows local (`wincore`) |
| **iOS** | Windows VS Code via Remote - SSH | Remote macOS `27.2` workspace | Remote macOS `27.2` workspace (`xcuitest`) |
| **Mac Catalyst** | Windows VS Code via Remote - SSH | Remote macOS `27.2` workspace | Remote macOS `27.2` workspace (`mac2`) |

- Appium tests must run against **real application builds** produced in the corresponding development workspace.
- Do not download CI artifacts or use Windows-to-macOS archive transfers as a substitute for building and running directly on the target host.

## CI Policy (Helper Only, Never Validation Authority)
- GitHub Actions CI workflows are supplementary infrastructure for builds, diagnostics, packaging, and automated checks.
- CI status is **never** a substitute for executing applications and UI tests on real target platforms and is **never** a Stage Definition of Done requirement.
- When a target cannot be executed because of a missing device, unavailable simulator, or environment limitation, report it explicitly as **blocked** or **not executed**.

## Unit tests
Test backend-independent behavior without a physical GPU: transforms, parent/child matrices, camera projection, asset conversion, mesh/index conversion, materials/textures, cache identity and lifecycle transitions.

## Stage 04 testing
The repository now includes targeted backend coverage for the Silk abstraction layer and the 3D host surface. The tests validate resource ownership, viewport changes, renderer lifecycle and the lightweight 3D view surface contract without depending on a live Windows UI automation host.

## Current validation status
- Android ARM64 Release backend and sample build pass for `net11.0-android`.
- Unit tests: `Orbit3D.Engine.Tests` Windows target, 54 passed.
- Physical Android runtime/GPU: validated on M1908C3JGG with GLES; the sample stays foregrounded, displays one draw call/one rendered object, and the tetrahedron is visible in an ADB screenshot.
- Android deployment used a standalone Release APK installed directly through ADB.
- Native Silk surfaces are implemented and runtime-validated for Android (`GLSurfaceView` OpenGL ES 3.0), Windows (WGL OpenGL 3.3 core popup), iOS (`CAEAGLLayer` + `EAGLContext` OpenGL ES 3.0), and Mac Catalyst (`CAOpenGLLayer` + CGL OpenGL 4.1 core profile).
- The existing `Orbit.Engine` Windows target builds. The legacy 2D `Orbit` app build remains blocked by its existing `..\..\..\engine\Orbit.Engine\Orbit.Engine.csproj` reference, which resolves to the missing `games/engine` directory from that sample.

## Stage 05 status
The Android GLES path is visually validated on a physical device. Validation exposed two rendering defects: `System.Numerics` matrices were uploaded with the wrong memory ordering, and the Silk.NET generic `DrawElements(in nint)` overload passed the address of the managed offset variable instead of the element-buffer byte offset. The backend now uploads row-vector matrices in the layout expected by GLSL and invokes `glDrawElements` with the native offset value through a cached delegate. First-frame diagnostics report upload/draw counts, clip-space bounds and GL errors; the physical run reports one draw, 12 indices and `GL=NoError`. The 42-test Windows suite passes. Context recreation and other MAUI platform backends remain unvalidated.

## Stage 06 status
Stage 06 closes the remaining runtime-completeness gaps by aligning GPU resources with the active `IRenderDevice`, invalidating stale resources when a graphics context is lost, and tracking per-frame renderer metrics. CPU-side assets stay available while the new device lazily rebuilds mesh and texture GPU resources from the original `Mesh3D` and `Texture2D` data. Camera interaction remains sample-local and updates the runtime `Camera3D.Transform`, while the renderer reports `FrameTime`, `FramesPerSecond`, `DrawCalls`, and `RenderedItems` from actual draw execution.

The Android sample was also verified on a physical device. ADB logcat identified a startup crash caused by updating the MAUI status label directly from the OpenGL `GLThread`; the sample now dispatches status updates to the UI thread and throttles metric-label updates. Device validation additionally caught and fixed the initial camera offset pointing away from the scene. The imported tetrahedron is visible in the ADB screenshot with live frame and draw metrics, responds to orbit input, and renders again after returning from the launcher and recreating its GLES surface; the app-specific log contains no fatal exception after the fix.

## Stage 07 Windows status
The Windows sample was launched with `dotnet run -f net11.0-windows10.0.19041.0`. Initial logs showed a valid WGL context and an indexed draw, but a screenshot revealed that WinUI's desktop-composition surface covered the WGL child window. The handler now uses a non-activating owned popup aligned over the MAUI view, with a private WGL device context and hit-testing passed through to XAML. The Toyota model and its sidecar texture are visibly rendered. The run logged one imported mesh, a decoded 1008x752 PNG, one queued item, one indexed draw and 20,000 triangles.

The Windows sample uses the actual Toyota GLB and its matching PNG from `assets/`. Both checked-in GLBs have geometry only and omit material/image/UV data; the sample binds the same-basename PNG as a real texture and computes a sample-only side projection for missing UVs. This limitation affects UV alignment and does not validate author-authored glTF material import.

Resizing the host window from 1440x753 to 1100x700 updated the graphics surface from 1424x669 to 1084x616 pixels while keeping the model visible. Closing the window logged context loss while the WGL context was current, allowing GPU resources to be disposed before native context destruction. The captured screenshots and stdout/stderr logs are in the session artifact folder. The later DevFlow-enabled run restored `Microsoft.Maui.DevFlow.Agent` `0.1.0-preview.12.26421.1` for the Windows Debug target and was launched with `dotnet run -f net11.0-windows10.0.19041.0`. The `SilkTriangleSample` WinUI agent connected on port 9223; its live visual tree exposed the MAUI surface, the native `Orbit3D.Silk.WglSurface` popup, and frame metrics reporting one draw call and one rendered object. DevFlow's regular XAML screenshot does not include that separate WGL popup, so `devflow-windows-native-capture.png` was captured from the Windows desktop; it shows the Toyota model rendered in the running window. DevFlow console logs confirm WGL context creation, GLB/sidecar texture import, an indexed draw of 20,000 triangles, and subsequent graphics-surface resize events (1232x575 and 1364x718). The DevFlow warning/error log query was empty. The standalone `console.err` capture is empty; `debug.out` belongs to an earlier broker startup and contains only its port assignment, not application diagnostics from the DevFlow run. A separate CDB-attached diagnostic run captured a second-chance `0xc000027b` exception in `Microsoft.UI.Xaml.dll`; this was not reproduced by the final `dotnet run`, resize, and normal-close validation.

## Stage 08 iOS status
The iOS `SilkGraphicsSurfaceHandler` and `SilkIosSurfaceView` (`CAEAGLLayer` + `EAGLContext` OpenGL ES 3.0) are implemented in `Orbit3D.Graphics.Silk`:
- Attaches both a color renderbuffer (`RGBA8`) and a 24-bit depth renderbuffer (`DepthComponent24`) to the `CAEAGLLayer` framebuffer object, propagating `DefaultFramebuffer` to `SilkRenderer3D` and `SilkRenderDevice` so `BeginFrame`/`EndFrame` and render-target restoration bind the `CAEAGLLayer` FBO rather than `0`.
- Allocates `CAEAGLLayer` renderbuffer storage only when view bounds are non-zero and only reallocates when pixel dimensions change (`UpdateSurfaceDimensions`).
- Integrates with `UIKit` lifecycle events (`OnResignActivation`, `DidEnterBackground`, `WillEnterForeground`, `OnActivated`) in `SilkGraphicsMauiExtensions` to pause rendering, release GPU resources while the `EAGLContext` is current on backgrounding, and recreate the surface/context and GPU resources from retained CPU assets (`IAssetCache`) upon returning to the foreground.
- Decodes the sidecar PNG texture (`toyota-gazoo-racing-wrt-gr-yaris-1-10.png`) into RGBA pixels on iOS/MacCatalyst using `UIImage` and `CGBitmapContext` in `SampleTextureLoader`, and launches `GraphicSurfacePage` on iOS in `App.xaml.cs`.
- Validated on the `iPhone Air — iOS 26.5` Simulator (`UDID: 34E134C7-25BB-4925-8A3A-65674A943E87`) on the provisioned remote macOS host via Visual Studio Code Remote - SSH from Windows 11. All 7 automated Appium/XCUITest UI tests (`IosSampleAppTests`), 45 `Orbit3D.Engine.Tests` unit tests, 2 `Orbit.Engine.Tests` unit tests, and 9 `ArtifactDiscoveryTests` pass, with visual screenshot verification confirming real OpenGL ES 3.0 rendering of the Toyota GR Yaris model and sidecar texture (`1` indexed draw call, `20,000` triangles), camera orbit interaction, landscape orientation resize, and background/foreground lifecycle recovery.

## Stage 09 Mac Catalyst status
The Mac Catalyst `SilkGraphicsSurfaceHandler` and `SilkMacSurfaceView` (`CAOpenGLLayer` + CGL OpenGL 4.1 core profile) are implemented in `Orbit3D.Graphics.Silk`:
- Registers an Objective-C `CAOpenGLLayer` subclass (`OrbitSilkOpenGLLayer`) at runtime via `libobjc.dylib`, requesting a hardware-accelerated, double-buffered CGL OpenGL 4.1 core-profile pixel format (`kCGLOGLPVersion_GL4_Core`, 24-bit color, 8-bit alpha, 24-bit depth) and loading OpenGL/CGL entry points from `/System/Library/Frameworks/OpenGL.framework/OpenGL`.
- Adopts and retains the compositor-provided `CGLContextObj` via `SilkNativeContextLifetime`, wrapping it in `SilkGraphicsContext` (`isOpenGles: false`, `presentAfterRenderCallback: true`) and updating `DefaultFramebuffer` and surface dimensions from `GL_FRAMEBUFFER_BINDING` and `GL_VIEWPORT` inside `drawInCGLContext:pixelFormat:forLayerTime:displayTime:`.
- Ensures deterministic GPU resource cleanup on surface suspension, replacement, or disposal by making the retained `CGLContextObj` current (`CGLSetCurrentContext`) before raising `ContextLost`, covered by 6 unit tests in `SilkNativeContextLifetimeTests` (51 total `Orbit3D.Engine.Tests` passing).
- Validated on the remote macOS host (`macOS 15.6.1`, `Xcode 26.6`, `Intel(R) UHD Graphics 630`, OpenGL `4.1 INTEL-23.7.2`, GLSL `4.10`). Because Remote - SSH executes in a non-GUI `Background` audit session where macOS `screencapture` cannot capture the WindowServer display when no interactive console session is unlocked, `SilkMacSurfaceView` includes an opt-in FBO readback capture hook (`ORBIT3D_SILK_CAPTURE_DIR`) and `GraphicSurfacePage` includes an opt-in validation command pump (`ORBIT3D_COMMAND_FILE`), both active only when their respective environment variables are set and living inside the App Sandbox container (`~/Library/Containers/com.remotecodehub.silktrianglesample/Data/tmp/orbit3d-capture`). Captured PNG frames and logs under `artifacts/apple/stage-09/` verify initial GLB + sidecar PNG rendering (`1` indexed draw call, `20,000` triangles, `1330x914` viewport), camera orbit and zoom, window resize (`1247x748`), and two repeated surface suspension/recreation cycles without stale GPU resource usage.

## Stage 10 cross-platform graphics validation status (Historical)
Stage 10 completed the cross-platform rendering baseline across all four supported targets (prior to the macOS `27.2` VS Code Remote - SSH consolidation):
- **Windows** (`net11.0-windows10.0.19041.0`): WGL OpenGL 3.3 Core Profile. Validated on Windows 11 workstation. Full lifecycle executed with initial render, camera orbit/zoom, window resize, and 2 context loss/recreation cycles (`count=1,2,3`, `cpuAssetCached=True`, single GPU mesh and texture upload per context). 9 UI tests in `SilkTriangleSample.UITests` passed.
- **Android** (`net11.0-android`): EGL OpenGL ES 3.0. Validated on physical device Xiaomi Redmi Note 8 (`M1908C3JGG`, Android 11 / API 30). Full lifecycle executed with initial render, camera orbit/zoom, surface resize, activity pause/resume, and 2 context loss/recreation cycles (`count=1,2,3`, `cpuAssetCached=True`, single GPU mesh and texture upload per context).
- **iOS** (`net11.0-ios`): EAGL OpenGL ES 3.0. Validated on iPhone Air Simulator (`34E134C7-25BB-4925-8A3A-65674A943E87`) on the former macOS 15.6.1 host. Full lifecycle executed with initial render, camera orbit/zoom, surface resize (960x780 -> 1260x2195), and 2 context loss/recreation cycles (`count=1,2,3`, `cpuAssetCached=True`, single GPU mesh and texture upload per context).
- **Mac Catalyst** (`net11.0-maccatalyst`): CGL OpenGL 4.1 Core Profile (`4.1 INTEL-23.7.2`, Intel UHD Graphics 630). Validated on the former macOS 15.6.1 host. Full build, launch, surface resize (1247x784 -> 1247x748), GLB and sidecar PNG upload (60,000 vertices, 60,000 indices, 1008x752 texture), and first rendered frame (1 indexed draw, 20,000 triangles).
- **Unit & Integration Tests**:
  - `Orbit.Engine.Tests` (2D engine): 2/2 tests passed on `net11.0`.
  - `Orbit3D.Engine.Tests` (3D engine): 55/55 passed on `net11.0`, 67/67 passed on `net11.0-windows10.0.19041.0`.
  - `SilkTriangleSample.UITests`: 9/9 UI automation tests passed.
- Evidence artifacts are archived under `artifacts/stage-10/{windows,android,ios,maccatalyst,summary}/`.

## Stage 11 Phase 1 skeletal animation status (Windows & Android)
Phase 1 implemented and validated runtime skeletal animation for `assets/Idle.fbx` (88 bones, 4 meshes, 5,420 vertices, 8,222 faces, `mixamo.com` 4.0s clip at 60 ticks/sec) on Windows and Android:
- **GPU Skinning Strategy**: Uses a `4x128` `RGBA32F` `uBoneTexture` on `TextureUnit.Texture1` sampled via `texelFetch` in `BasicLitSkinned` / `BasicLitTexturedSkinned` shaders (`#version 330 core` on desktop GL, `#version 300 es` on GLES 3.0) instead of a 128-bone UBO, avoiding Android SwiftShader GLES 3.0 `GL_MAX_VERTEX_UNIFORM_VECTORS (256)` shader compilation limits.
- **Unit Tests (`Orbit3D.Engine.Tests`)**: 60/60 passed on `net11.0`; 74/74 passed on `net11.0-windows10.0.19041.0` (including `IdleFbxImporterTests` and `SkeletalAnimationTests`).
- **Android (`net11.0-android`)**: `AnimationSample` (`com.remotecodehub.animationsample`) and `AnimationSample.UITests` (`Platform=Android`, `uiautomator2`) passed 5/5 on `emulator-5554`, including playback progression, pause/resume/loop/speed controls, context recreation recovery, and screenshot verification.
- **Windows (`net11.0-windows10.0.19041.0`)**: Direct execution of `AnimationSample.exe` confirmed WGL OpenGL 3.3.0 context creation (`NVIDIA GeForce RTX 3060 Laptop GPU/PCIe/SSE2`) and `MainWindowHandle` creation without errors. Automated Windows Appium UI tests are **blocked** on this host (`WinAppDriver.exe` not installed; `wincore@4.1.2` cannot enumerate windows in the container session).
- **Evidence**: Archived under `artifacts/stage-11/phase-1/{windows,android,unit-tests,ui-tests}/`.

## Stage 11 Phase 2 skeletal animation status (iOS & Mac Catalyst)
Phase 2 validated the existing `Orbit3D.Engine` skeletal animation runtime and `Orbit3D.Graphics.Silk` `RGBA32F` `4x128` `uBoneTexture` GPU skinning path on Apple targets directly on the remote `macOS 27.2.1` (`arm64`, `Apple M4`, `Xcode 27.2`) host via VS Code Remote - SSH:
- **MAUI Version & Apple Scene Startup**: Fixed `Directory.Build.props` `MauiVersion` to `11.0.10` (matching nuget.org; `11.0.100` does not exist on NuGet and failed `NU1102`) and removed project-level `MauiVersion` overrides. Added `SceneDelegate.cs` (`MauiUISceneDelegate`) to `AnimationSample/Platforms/iOS/` and `AnimationSample/Platforms/MacCatalyst/`.
- **iOS (`net11.0-ios`, `iossimulator-arm64`)**: `SilkIosSurfaceView` (`CAEAGLLayer` + `EAGLContext` OpenGL ES 3.0, `OpenGL ES GLSL ES 3.00`, `Apple Software Renderer`) compiles the `#version 300 es` `BasicLitSkinned` shader, uploads the `RGBA32F` `4x128` bone texture (`NEAREST`), and renders `Idle.fbx` (`88` bones, `240` nodes, `16` draw calls, `32,888` triangles). `IosAnimationTests` (`xcuitest`) passed **6/6** on `iPhone 17 — iOS 27.2` (`62EB87F6-E890-4118-AAA2-5864621757BF`). Note: `iPhone Air — iOS 26.5` (`34E134C7-25BB-4925-8A3A-65674A943E87`) is absent on Xcode 27.2, and `iPhone Air — iOS 27.2` (`006860C8-F1E7-4065-A4A1-039ADDF6F04E`) failed simulator data migration on boot.
- **Mac Catalyst (`net11.0-maccatalyst`, universal `maccatalyst-arm64`/`maccatalyst-x64`)**: `SilkMacSurfaceView` (`CAOpenGLLayer` + CGL OpenGL `4.1 Metal - 91.7` on `Apple M4`, GLSL `4.10`, 32-bit depth) compiles the `#version 330 core` `BasicLitSkinned` shader and renders animated `Idle.fbx` (`16` draw calls, `32,888` triangles). `MacCatalystAnimationTests` (`mac2` with `caffeinate -u`) passed **7/7**, including window resize (`osascript` was blocked by System Events TCC timeout; `mac2` corner-drag resize succeeded) and 2 consecutive context recreation cycles.
- **Unit & Regression Tests**: `Orbit.Engine.Tests` (`net11.0`: **2/2**), `Orbit3D.Engine.Tests` (`net11.0`: **60/60**). On the macOS host, `net11.0-android` build is **blocked** (`XA5300`: Android SDK not installed on macOS) and `net11.0-windows10.0.19041.0` is **not executed** (Windows-only TFM). Windows Appium UI tests remain **blocked** on the Windows host (`WinAppDriver.exe` not installed).
- **Evidence**: Archived under `artifacts/stage-11/phase-2/{ios,maccatalyst,unit-tests,regression,summary}/`.

## Stage 11 Phase 3 Windows & Android final validation status
- **Windows (`net11.0-windows10.0.19041.0`, `MauiVersion=11.0.10`)**: Validated in interactive desktop session (`SessionId=2`) using Appium `4.0.0` + `wincore@4.1.2` (`automationName = "Wincore"`, `ms:waitForAppLaunch = 15`); `WinAppDriver.exe` is not installed and is not required by `wincore`. Added `SilkSurfaceAutomationPeer` to `SilkWindowsSurfaceView` (`OnCreateAutomationPeer` + `IsOffscreenCore`) so `SilkGraphicsSurface` is visible in WinUI 3 UIA, used `windows: invoke` for button activation in `AnimationSurfacePageObject`, and used `ORBIT3D_SILK_CAPTURE_DIR` `gl.ReadPixels` fallback in `DiagnosticsCollector` when `WincoreServer` `getScreenshot` returns `InternalError: Identificador inválido` (`ForegroundWindow=0`). `WindowsAnimationTests` passed **5/5** (`29.1s`).
- **Android (`net11.0-android`, `MauiVersion=11.0.10`)**: Rebuilt Release APK and revalidated on `emulator-5554` (`pixel_7_-_api_36_0_1`) using Appium `4.0.0` + `uiautomator2@9.2.1` (`APPIUM_HOME=C:\Users\wesll\.appium-horizon-appium4`). `AndroidAnimationTests` passed **5/5** (`29.9s`).
- **Unit & Artifact Tests**: `Orbit.Engine.Tests` (`net11.0`: **2/2**; note `net11.0-windows10.0.19041.0` fails `LoadSceneShouldChangeStateToLoaded` in headless `testhost` with `REGDB_E_CLASSNOTREG` because `GraphicsView` requires an initialized WinUI runtime), `Orbit3D.Engine.Tests` (`net11.0`: **60/60**, `net11.0-windows10.0.19041.0`: **74/74**), and `AnimationSample.UITests` (`Category=Artifact`: **4/4**).
- **Evidence**: Archived under `artifacts/stage-11/phase-3/{environment,windows,android,unit-tests,regression,summary}/`.

## Integration tests
Where practical, validate Assimp imports, hierarchy conversion, texture resolution and GPU resource creation. Ordinary unit tests must not require a platform graphics device.

## 3D sample
Before 0.1-rc1, provide a runnable sample that loads a known asset, creates a scene and perspective camera, renders through the GPU backend, supports basic interaction and exposes development metrics.

## Platform matrix
Validate every platform the selected renderer claims to support. Record unsupported configurations explicitly.

## Regression
3D changes must not silently remove or alter existing 2D rendering.

## UI Automation and Testing Infrastructure
For the 3D sample UI testing infrastructure, Appium driver setup, Page Objects, platform artifact packaging, and matrix CI configuration, see [UI Automation Guide](testing/ui-automation.md).
For the Apple simulator testing policy, see [Apple Simulator UI Testing Guideline](testing/apple-simulator-ui-testing.md).

## Build
Run focused builds/tests after each change and a complete relevant build before completing a milestone.
