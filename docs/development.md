# Development Guide

## Structure
- `engine/` — reusable engine projects.
- `games/` — sample/game applications.
- `assets/` — repository assets.
- `scripts/` — development scripts.
- `.github/skills/` — specialized agent guidance.
- `docs/` — architecture and development rules.

## Milestones
Implement 3D in independent stages. Do not implement future-stage functionality early merely because an abstraction is obvious.

For each stage: inspect current code, read relevant guidance, make the smallest coherent change, build, test and document limitations.

## Stage 05 status
The Silk backend contains real OpenGL resource and indexed-draw commands. An Android MAUI `SilkGraphicsSurface` now owns a `GLSurfaceView`/GLES 3 context, exposes context/resize/render lifecycle events and relies on GLSurfaceView for present. The Android backend and OBJ sample build after installing `maui-android`; the sample imports and renders the tetrahedron from `Resources/Raw/poly.obj`. Packaging emits duplicate-native-library warnings. `dotnet workload repair` could not repair a cached Android SDK MSI source, while the targeted workload install succeeded with an old iOS preview manifest cleanup warning. Runtime visual validation is pending because the current VS Code session has no MAUI startup project selected. iOS, Mac Catalyst and Windows handlers remain pending. Do not report Stage 05 complete or a platform runtime-supported until the imported model is visibly rendered and its lifecycle is validated.

## Stage 07 Windows
The sample targets `net10.0-windows10.0.19041.0` on Windows hosts and registers the native WGL surface through `UseSilkGraphics()`. From `games/3D/SilkTriangleSample/`, run `dotnet run -f net10.0-windows10.0.19041.0`. A supported OpenGL 3.3 driver is required. The WinUI handler overlays the view with an owned, non-activating WGL popup because WinUI's composition surface occludes native child windows; it tracks the MAUI bounds and DPI, passes pointer hit-testing through, and destroys GPU/context resources on unload or host-window close. The sample packages the Toyota GLB and same-basename PNG from `assets/`; the asset-specific missing-UV fallback is documented in `asset-pipeline.md`. Model/texture rendering, resize and host-window teardown were visually/runtime validated on Windows.

## Stage 08 iOS
The sample targets `net10.0-ios` (`ApplicationId`: `com.remotecodehub.silktrianglesample`) and uses `SilkGraphicsSurfaceHandler` (`CAEAGLLayer` + `EAGLContext` OpenGL ES 3.0) with CoreGraphics sidecar texture decoding in `SampleTextureLoader`. Apple development and validation use Visual Studio Code Remote - SSH from Windows 11 to the provisioned remote macOS host (`macOS 15.6.1`, `Xcode 26.6`, `.NET SDK 10.0.302`) running the `iPhone Air — iOS 26.5` Simulator (`UDID: 34E134C7-25BB-4925-8A3A-65674A943E87`) and automated Appium/XCUITest UI tests (`IosSampleAppTests`). Multiplatform release artifacts are also built in CI via `.github/workflows/silk-triangle-sample-build.yml`.

## Stage 09 Mac Catalyst
The sample targets `net10.0-maccatalyst` (`ApplicationId`: `com.remotecodehub.silktrianglesample`, `SupportedOSPlatformVersion`: `17.0` on the sample and `15.0` on `Orbit3D.Engine`) and registers `SilkGraphicsSurfaceHandler` (`SilkMacSurfaceView` + `CAOpenGLLayer` + CGL OpenGL 4.1 core profile) via `UseSilkGraphics()`. Build and run on macOS with `dotnet build games/3D/SilkTriangleSample/SilkTriangleSample.csproj -c Debug -f net10.0-maccatalyst` or `scripts/validate-maccatalyst.sh`. Sidecar PNG decoding shares the `IOS || MACCATALYST` CoreGraphics path in `SampleTextureLoader`. Validation evidence (rendered frame captures and diagnostic logs) is stored under `artifacts/apple/stage-09/`.

## Public API
Public engine APIs require XML documentation. Prefer stable Orbit concepts over third-party implementation details.

## Dependencies
Before adding a graphics dependency evaluate target frameworks, native dependencies, license, platform coverage, surface integration, shader support and maintenance.

## Commits
Use focused commit messages. Do not mix unrelated formatting or refactoring into milestone changes.

## Editor
The editor is explicitly deferred.
