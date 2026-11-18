# Orbit3D Stages

This document is the implementation roadmap for Orbit3D. Each stage has a focused scope and a definition of done (DoD). Completed stages describe the functionality already integrated into `main`; planned stages describe the next runtime milestones.

## Platform target/version policy

Apple and non-Apple targets must use the versions pinned by the repository root [`global.json`](../global.json) and [`Directory.Build.props`](../Directory.Build.props). Do not arbitrarily upgrade or modify `global.json`, `Directory.Build.props`, target frameworks, or package/workload pins.

- **.NET SDK Baseline**: `11.0.100` (pinned in `global.json` with `rollForward: disable`).
- **Windows and Android**: Developed, built, executed, and validated (including Appium UI tests) directly in the local **Windows** VS Code workspace.
- **iOS and Mac Catalyst**: Developed, built, executed, and validated (including Appium UI tests) directly on the provisioned **macOS `27.2`** host via **Visual Studio Code Remote - SSH** from Windows. Confirm exact Xcode and workload-set versions on the remote macOS host as environment prerequisites.
- **No Remote Build Orchestration**: Do not transfer working trees via TAR/SCP or run standalone Windows-to-macOS SSH build scripts as a replacement for VS Code Remote - SSH.
- **CI Policy**: GitHub Actions CI workflows are supplementary helpers only. CI status is never authoritative validation and is never a Stage Definition of Done requirement.

## Stage 01 — 3D foundation

Stage 01 establishes the backend-neutral 3D runtime model on top of the existing Orbit Engine and .NET MAUI stack. It adds transforms, scene hierarchy, cameras, meshes, materials, textures and basic lighting without coupling game/runtime code to a graphics API. It also establishes the initial Assimp importer boundary and the test project used by later stages.

### DoD

- [x] Create `Orbit3D.Engine` and `Orbit3D.Engine.Tests`.
- [x] Target .NET 10 and the supported MAUI configurations.
- [x] Add `Transform3D` with position, rotation and scale.
- [x] Add `Node3D` and parent/child scene hierarchy support.
- [x] Add `Scene3D`.
- [x] Add `Camera3D`.
- [x] Add `Mesh3D`, `Model3D`, `Material3D` and `Texture2D`.
- [x] Add basic `Light3D` support.
- [x] Define `IModelImporter` and the Orbit-owned asset boundary.
- [x] Keep public Orbit3D APIs independent from Assimp/native types.
- [x] Add initial unit-test coverage for backend-independent runtime behavior.
- [x] Preserve the existing Orbit 2D API and rendering path.

## Stage 02 — Assimp asset pipeline

Stage 02 integrates Assimp.MAUI as the CPU-side asset importer and converts imported data into Orbit-owned runtime objects. The importer handles meshes, hierarchy, materials and texture references while releasing native Assimp resources at the import boundary. No GPU work is performed by the asset pipeline.

### DoD

- [x] Integrate the actual Assimp.MAUI 6.0.5-rc5 API.
- [x] Import model files through `AssimpModelImporter`.
- [x] Support stream/file input through the existing importer boundary.
- [x] Convert Assimp scenes and nodes into `Model3D`/`Node3D` data.
- [x] Convert meshes into Orbit-owned vertex/index data.
- [x] Convert material information into `Material3D`.
- [x] Preserve texture path references for later runtime upload.
- [x] Use the required triangulation, vertex welding, normal, tangent and UV processing flags.
- [x] Release Assimp native resources after conversion.
- [x] Keep Assimp types out of public Orbit3D contracts.
- [x] Add tests for asset conversion and mesh/index conversion.
- [x] Document the asset-pipeline boundary and current limitations.

## Stage 03 — backend-neutral GPU contracts

Stage 03 defines the renderer/device abstraction required to run 3D content without exposing a graphics API to game code. It introduces explicit GPU resources, render state, pipelines, shaders, buffers, textures, render targets and indexed drawing. The contracts are intentionally backend-neutral so Silk.NET can be introduced without changing the engine API.

### DoD

- [x] Define `IRenderDevice`.
- [x] Define `IRenderer3D`.
- [x] Define vertex and index buffer abstractions.
- [x] Define texture, shader and shader-program abstractions.
- [x] Define pipeline and render-state abstractions.
- [x] Define render-target/surface abstractions.
- [x] Define viewport and camera/view/projection contracts.
- [x] Define depth testing and face-culling state.
- [x] Define indexed drawing.
- [x] Require explicit ownership/disposal for GPU resources.
- [x] Keep OpenGL, OpenGL ES, Metal, Direct3D and Vulkan types out of Engine public APIs.
- [x] Add backend-independent contract/lifecycle tests.
- [x] Document the renderer boundary and resource ownership model.

## Stage 04 — Silk backend foundation

Stage 04 selects Silk.NET as the first concrete graphics backend and implements the backend resource model behind the neutral contracts. The backend establishes device ownership, context-aware resources, shader compilation, buffers, textures and render-state management. The design also establishes the lifecycle rules needed for MAUI graphics contexts.

### DoD

- [x] Add the dedicated `Orbit3D.Graphics.Silk` project.
- [x] Isolate Silk.NET references from `Orbit3D.Engine`.
- [x] Implement `SilkRenderDevice`.
- [x] Implement `SilkRenderer3D`.
- [x] Implement GPU vertex and index buffers.
- [x] Implement shader/program compilation and linking.
- [x] Implement texture creation/upload.
- [x] Implement pipeline/render-state handling.
- [x] Implement depth, culling and blending state.
- [x] Implement framebuffer/render-target support.
- [x] Track graphics-context ownership and resource lifetime.
- [x] Ensure stale GPU resources are not deleted through unrelated contexts.
- [x] Add backend lifecycle and ownership tests.
- [x] Keep the backend isolated from game/runtime code.

## Stage 05 — Android Silk/OpenGL ES surface

Stage 05 connects the Silk backend to a real MAUI Android surface and validates the first accelerated 3D rendering path on physical hardware. It adds the OpenGL ES context lifecycle, native surface hosting, resize handling and indexed drawing. The stage also establishes the first end-to-end path from imported CPU mesh data to a visible GPU-rendered object.

### DoD

- [x] Implement the Android native Silk graphics surface.
- [x] Create and manage an OpenGL ES 3 context.
- [x] Integrate the surface with the MAUI view lifecycle.
- [x] Handle context creation, loss, resize and disposal.
- [x] Upload CPU mesh data to GPU buffers in the active context.
- [x] Render indexed geometry through Silk.NET/OpenGL ES.
- [x] Implement depth testing and back-face culling.
- [x] Validate `System.Numerics` matrix layout against GLSL.
- [x] Correct indexed draw element offsets for the OpenGL ES path.
- [x] Add the Android 3D sample surface and rendering loop.
- [x] Validate the backend on physical Android hardware.
- [x] Confirm no regression to the existing 2D engine.
- [x] Document unsupported platforms and lifecycle limitations.

## Stage 06 — runtime scene, queue, cache and camera

Stage 06 turns the renderer into a real runtime scene path rather than a diagnostic-only renderer. It adds CPU asset caching, GPU resource caching, scene traversal into a render queue, camera integration, material/texture binding and frame metrics. The sample uses the runtime scene and supports basic camera interaction while preserving CPU assets for GPU-context recreation.

### DoD

- [x] Add the CPU-side `IAssetCache`/model asset cache.
- [x] Define deterministic asset/cache identity from import settings.
- [x] Support concurrent cache access without duplicate imports.
- [x] Add `RenderItem3D` and `RenderQueue3D`.
- [x] Traverse `Scene3D` hierarchy into renderable queue items.
- [x] Apply accumulated node transforms to render items.
- [x] Integrate `Camera3D` view/projection matrices with the renderer.
- [x] Add GPU mesh/texture caching through `GpuResourceCache`.
- [x] Make GPU resources aware of the active `IRenderDevice`.
- [x] Rebuild GPU resources from CPU assets after context/device recreation.
- [x] Add basic material and texture binding.
- [x] Add basic directional lighting to the runtime render path.
- [x] Add `RenderMetrics` including frame time, FPS, draw calls and rendered items.
- [x] Move the sample to the real `Scene3D` runtime path.
- [x] Add basic orbit/zoom camera interaction.
- [x] Validate surface recreation and GPU resource rebuilding on Android.
- [x] Add tests for cache identity, scene traversal, camera, queue, metrics and lifecycle.
- [x] Document the runtime render path and cache/resource model.

## Stage 07 — Windows Silk/WGL surface

Stage 07 extends the concrete Silk backend to Windows and validates the same runtime scene path through a native WGL/OpenGL surface hosted by MAUI/WinUI. It adds Windows context lifecycle, resize and composition handling while reusing the Stage 06 renderer and caches. The sample is validated with a real GLB asset and matching sidecar texture from the repository `assets` directory.

### DoD

- [x] Implement the Windows native Silk graphics surface.
- [x] Create and manage the Windows WGL/OpenGL context.
- [x] Integrate the native surface with the MAUI Windows view lifecycle.
- [x] Handle WGL context creation, resize, loss and disposal.
- [x] Resolve WinUI desktop-composition ordering so the WGL surface is visible.
- [x] Preserve XAML/MAUI input hit testing while rendering through the native surface.
- [x] Reuse the Stage 06 `Scene3D`, `RenderQueue3D`, camera and cache path.
- [x] Load and render a real GLB asset from `assets`.
- [x] Load and upload the matching same-basename PNG texture.
- [x] Validate indexed drawing, depth/culling and textured material binding.
- [x] Validate Windows surface resizing while the model remains visible.
- [x] Validate context-loss disposal while the WGL context is current.
- [x] Preserve the Android implementation and existing runtime path.
- [x] Add/document Windows visual validation, logs and platform limitations.
- [x] Keep iOS and Mac Catalyst explicitly unsupported until their native stages.

## Stage 08 — iOS Silk/OpenGL ES 3 surface, VS Code Remote - SSH macOS simulator validation and CI builds

Stage 08 completes the native iOS Silk graphics surface (`CAEAGLLayer` + `EAGLContext` OpenGL ES 3.0 with color and 24-bit depth renderbuffers, `DefaultFramebuffer` binding, CoreGraphics sidecar texture decoding, and UIKit application lifecycle suspension/recreation), adopts the **Windows 11 + Visual Studio Code Remote - SSH + remote macOS host** development and validation workflow on the **iPhone Air — iOS 26.5 Simulator**, repairs the repository GitHub Actions CI workflows (`.github/workflows/dotnet-macos.yml`, `.github/workflows/dotnet-windows.yml`, `.github/workflows/silk-triangle-sample-build.yml`, `.github/workflows/nuget-release.yml`), and validates the real `SilkTriangleSample` application with automated Appium/XCUITest UI tests (`IosSampleAppTests`), unit tests, and visual screenshot inspection. The Engine contracts and runtime scene path remain unchanged; only the platform backend/hosting layer and validation pipeline are extended.

### Current Implementation & Validation Status

- **Implemented & Validated (`net11.0-ios` on `iPhone Air — iOS 26.5 Simulator`)**: Native `SilkIosSurfaceView` (`CAEAGLLayer` + `EAGLContext` OpenGL ES 3.0), color (`RGBA8`) + depth (`DepthComponent24`) renderbuffers attached to the layer FBO, `SilkGraphicsContext.DefaultFramebuffer` propagation in `SilkRenderer3D` and `SilkRenderDevice`, dimension-change guard on `RenderBufferStorage`, `UIKit` lifecycle hooks (`OnResignActivation`, `DidEnterBackground`, `WillEnterForeground`, `OnActivated`), CoreGraphics `UIImage`/`CGBitmapContext` sidecar PNG decoding in `SampleTextureLoader`, `GraphicSurfacePage` launch on iOS in `App.xaml.cs`, structured diagnostics contract (`Phase`, `Context`, `Device`, `Assets`, `Meshes`, `Texture`, `Items`, `DrawCalls`, `Triangles`, `FPS`, `Frames`, `Viewport`, `Yaw`, `Pitch`, `Dist`), 4-platform CI build workflow (`.github/workflows/silk-triangle-sample-build.yml`), historical CI artifact helpers (subsequently retired), and 7-test iOS UI automation suite (`IosSampleAppTests` + `IosGraphicSurfacePage`).
- **Historical Development & Validation Environment**: Windows 11 client running Visual Studio Code connected via Remote - SSH to the former `macOS 15.6.1` (`Xcode 26.6`, Apple SDK `26.5.10315/10318`) host with Antigravity executing directly in the remote macOS workspace and validating on the `iPhone Air — iOS 26.5` Simulator (`UDID: 34E134C7-25BB-4925-8A3A-65674A943E87`). BrowserStack is no longer a required Stage 08 dependency.

### DoD

- [x] Implement the iOS native Silk graphics surface (`CAEAGLLayer` + `EAGLContext`).
- [x] Select and implement the supported iOS graphics context path required by Silk.NET (`Silk.NET.OpenGLES` 3.0 with `DefaultFramebuffer` FBO + `DepthComponent24` renderbuffer).
- [x] Integrate the native surface with MAUI iOS lifecycle and layout.
- [x] Handle context/surface creation, resize, suspension, recreation and disposal.
- [x] Reuse the existing `IRenderDevice`/`IRenderer3D` contracts without leaking platform types.
- [x] Build multiplatform release artifacts (`Android`, `iOS`, `MacCatalyst`, `Windows`) in `.github/workflows/silk-triangle-sample-build.yml`.
- [x] Render the existing Stage 07 sample scene on the `iPhone Air — iOS 26.5` Simulator on the remote macOS host via VS Code Remote - SSH.
- [x] Validate mesh upload, indexed drawing, depth and culling on the iOS Simulator (`1` indexed draw call, `20,000` triangles).
- [x] Validate the GLB/sidecar texture runtime path on iOS (`SampleTextureLoader` CoreGraphics RGBA decoding of `1008x752` texture).
- [x] Validate camera interaction, orientation resize, and runtime metrics via automated UI tests (`IosSampleAppTests`).
- [x] Validate graphics-surface recreation across background/foreground transitions without stale GPU-resource use.
- [x] Capture and visually verify iOS Simulator screenshots and device/application logs under `artifacts/apple/stage-08/`.
- [x] Add focused platform and artifact-inspection tests (`ArtifactDiscoveryTests`, `IosSampleAppTests`).
- [x] Document the VS Code Remote - SSH + macOS Simulator development architecture, CI workflows, supported iOS Simulator configuration, and known limitations.
- [x] Confirm existing Android, Windows and 2D engine paths remain functional.

## Stage 09 — Mac Catalyst Silk surface

Stage 09 adds the native Mac Catalyst Silk graphics surface (`CAOpenGLLayer` + CGL OpenGL 4.1 core profile) and integrates it with the MAUI Mac Catalyst window/view lifecycle. The same backend-neutral renderer and runtime scene are reused, with platform-specific context and surface management isolated in the Silk project. The stage provides a real Mac Catalyst validation target before cross-platform stabilization.

### Current Implementation & Validation Status

- **Implemented & Validated (`net11.0-maccatalyst` on `macOS 15.6.1`, `Intel UHD Graphics 630`)**: Native `SilkMacSurfaceView` (`UIView` hosting a dynamically registered Objective-C `CAOpenGLLayer` subclass `OrbitSilkOpenGLLayer` with a CGL `kCGLOGLPVersion_GL4_Core` OpenGL 4.1 core-profile pixel format, 24-bit color + 8-bit alpha + 24-bit depth buffer, `CADisplayLink` frame scheduling, and `SilkNativeContextLifetime` context ownership), `DefaultFramebuffer` propagation from the compositor's bound FBO, `SilkGraphicsMauiExtensions` Mac Catalyst handler and lifecycle registration (`SceneDidDisconnect`, `WillTerminate`), `GraphicSurfacePage` launch on Mac Catalyst in `App.xaml.cs` and `MauiProgram.cs`, CoreGraphics `SampleTextureLoader` sidecar PNG decoding (`1008x752`), 6 `SilkNativeContextLifetimeTests` unit tests, updated `MacCatalystGraphicSurfacePage` + `MacCatalystSampleAppTests`, and runtime GPU validation evidence under `artifacts/apple/stage-09/`.

### DoD

- [x] Implement the Mac Catalyst native Silk graphics surface (`SilkMacSurfaceView` + `CAOpenGLLayer` / CGL OpenGL 4.1 core profile).
- [x] Select and implement the supported Mac Catalyst graphics context path required by Silk.NET (`Silk.NET.OpenGL` with CGL `kCGLOGLPVersion_GL4_Core` and `isOpenGles: false` `#version 330 core` shaders).
- [x] Integrate the native surface with MAUI Mac Catalyst lifecycle and layout.
- [x] Handle context/surface creation, resize, suspension, recreation and disposal.
- [x] Reuse the existing `IRenderDevice`/`IRenderer3D` contracts.
- [x] Render the existing sample scene on a real Mac Catalyst target (`macOS 15.6.1`, `Intel(R) UHD Graphics 630`, OpenGL `4.1 INTEL-23.7.2`).
- [x] Validate mesh upload, indexed drawing, depth and culling (`1` indexed draw call, `20,000` triangles, 24-bit depth buffer).
- [x] Validate the GLB/sidecar texture runtime path (`toyota-gazoo-racing-wrt-gr-yaris-1-10.glb` + `1008x752` PNG).
- [x] Validate camera interaction and runtime metrics (orbit, zoom, resize from `1330x914` to `1247x748`, live FPS/frame-time/draw-call/triangle diagnostics).
- [x] Validate graphics-surface recreation without stale GPU-resource use (two repeated suspend/recreate cycles with `ContextLost` raised while `current=True`).
- [x] Add focused platform tests where feasible (`SilkNativeContextLifetimeTests`, updated `SilkGraphicsBackendTests`, `MacCatalystSampleAppTests`).
- [x] Document the supported Mac Catalyst/macOS matrix and known limitations.
- [x] Confirm Android, Windows and iOS paths remain functional.

## Stage 10 — cross-platform graphics validation and stabilization

Stage 10 consolidates the four MAUI graphics targets into a single supported runtime matrix and removes platform-specific instability discovered during implementation. It focuses on lifecycle correctness, rendering consistency, resource ownership, performance instrumentation and regression coverage rather than introducing new rendering features. The stage establishes a stable cross-platform baseline before animation work begins.

### Current Implementation & Validation Status

- **Implemented & Validated Across 4 Platforms (`Windows`, `Android`, `iOS`, `Mac Catalyst`)**: Consolidated cross-platform rendering baseline using the Silk.NET graphics backend and backend-neutral `Orbit3D.Engine` runtime scene (`toyota-gazoo-racing-wrt-gr-yaris-1-10.glb` + sidecar PNG `1008x752`). Full surface lifecycle exercised on all four platforms (context creation, surface resize, camera orbit/zoom, suspend/context loss, resume/context recreation across 2 full cycles). Verified CPU asset caches survive context loss and GPU caches re-upload resources once per context. Complete validation evidence preserved under `artifacts/stage-10/{windows,android,ios,maccatalyst,summary}/`.

### DoD

- [x] Validate Android, Windows, iOS and Mac Catalyst with the same runtime scene path.
- [x] Validate context creation, resize, suspension, recreation and disposal on all four platforms.
- [x] Confirm CPU asset caches survive graphics-context recreation.
- [x] Confirm GPU caches never reuse resources from an invalid context/device.
- [x] Confirm no GPU resource leaks across repeated surface recreation.
- [x] Validate mesh, texture, material, depth, culling and indexed-draw behavior on all four platforms.
- [x] Validate camera interaction and render metrics on all four platforms.
- [x] Establish a documented supported-platform/version/device matrix.
- [x] Add or strengthen backend-independent regression tests for lifecycle and cache behavior.
- [x] Record platform-specific limitations and unsupported configurations.
- [x] Measure representative frame/draw/triangle metrics on each platform.
- [x] Verify the existing 2D Orbit rendering path remains functional.
- [x] Update architecture, rendering, performance, testing and development documentation.

## Stage 11 — 3D animation

Stage 11 introduces runtime 3D skeletal/skinning animation after all four MAUI graphics backends have been implemented and stabilized. Animation data and evaluation remain backend-neutral inside `Orbit3D.Engine`, while GPU skinning or another accelerated implementation is added behind the existing renderer abstraction and validated across Android, Windows, iOS, and Mac Catalyst.

`SilkTriangleSample` remains the static rendering and graphics baseline; animation is validated through a dedicated second MAUI sample application and UI automation suite.

### Mandatory animation asset

- **Required Asset**: [`assets/Idle.fbx`](../assets/Idle.fbx) (existing Mixamo FBX model with embedded animated skeletal hierarchy, skeleton/bones, skinning/vertex-to-bone influence data, and animation clip).
- Stage 11 implementation and validation **must** locate and use `assets/Idle.fbx` directly through the Orbit3D Assimp asset-import pipeline, preserving and exercising its embedded skeleton, skinning weights, and animation data.
- **Prohibited**: Do not replace `assets/Idle.fbx` with another model, download a different Mixamo model, generate a synthetic skeleton/animation substitute, discard skinning/animation data to render it statically, or commit a redundant copy of the asset without a concrete technical requirement.
- If the current FBX importer lacks support for any skeleton, skinning, or animation data in `assets/Idle.fbx`, Stage 11 must implement the required importer support and document any format limits accurately.

### Dedicated animation sample and UI automation

- **Sample Application**: `games/3D/AnimationSample/` (`ApplicationId`: `com.remotecodehub.animationsample`), a standalone runnable MAUI app using `Orbit3D.Engine` and `Orbit3D.Graphics.Silk` that loads `assets/Idle.fbx`, exposes stable accessibility identifiers, and demonstrates skeleton/bone hierarchy, skinning weights, clip discovery/selection, playback, pause/resume, looping, time progression, observable animation/rendering diagnostics, and GPU resource recreation on context loss across Android, Windows, iOS, and Mac Catalyst.
- **UI Automation Project**: `games/3D/AnimationSample.UITests/`, a dedicated Appium UI test project (structurally parallel to `SilkTriangleSample.UITests/`) supporting Android (`uiautomator2`), Windows (`windows`), iOS Simulator (`xcuitest`), and Mac Catalyst (`mac2`). Tests must assert real animation behavior on `assets/Idle.fbx` using observable state and diagnostics (launch, surface initialization, `Idle.fbx` load, skeleton/skinning/clip availability, time progression, pause/resume, looping, diagnostics pose/time metrics, continued rendering, context recreation recovery, and zero unhandled exceptions or invalid GPU resource usage).
- **Solution Integration**: Integrate `AnimationSample/AnimationSample.csproj` and `AnimationSample.UITests/AnimationSample.UITests.csproj` into `games/3D/Orbit3D.Games.slnx`, keeping `SilkTriangleSample` and `SilkTriangleSample.UITests` independent.

### DoD

- [ ] Inspect and use the existing `assets/Idle.fbx` Mixamo model.
- [ ] Preserve and validate its embedded skeleton, skinning, and animation data.
- [ ] Ensure the FBX importer supports all data required by `assets/Idle.fbx`.
- [ ] Define backend-neutral animation/skeleton runtime data.
- [ ] Extend the Assimp conversion boundary for supported skeletal animation data (bones, inverse bind transforms, weights, and animation clips).
- [ ] Preserve animation data in CPU-owned assets for context recreation.
- [ ] Add runtime animation evaluation independent of the graphics backend without per-frame Assimp traversal.
- [ ] Define the renderer contract required for accelerated skinning and implement the selected Silk GPU skinning path.
- [ ] Create the dedicated `AnimationSample` MAUI application (`games/3D/AnimationSample/`).
- [ ] Set its application ID to `com.remotecodehub.animationsample`.
- [ ] Load and render `assets/Idle.fbx` through the Orbit3D asset pipeline.
- [ ] Demonstrate the model's imported skeleton and bone hierarchy.
- [ ] Demonstrate imported skin weights and vertex influences.
- [ ] Demonstrate playback of the animation contained in `assets/Idle.fbx`.
- [ ] Demonstrate pause/resume, looping, and time progression.
- [ ] Create the dedicated `AnimationSample.UITests` Appium project (`games/3D/AnimationSample.UITests/`).
- [ ] Integrate both projects into `games/3D/Orbit3D.Games.slnx`.
- [ ] Expose stable accessibility identifiers and runtime animation diagnostics.
- [ ] Implement meaningful Appium animation tests for Android (`uiautomator2`).
- [ ] Implement meaningful Appium animation tests for Windows (`windows`).
- [ ] Implement meaningful Appium animation tests for iOS (`xcuitest`).
- [ ] Implement meaningful Appium animation tests for Mac Catalyst (`mac2`).
- [ ] Validate animation and rendering through actual application execution on all four platforms (Android/Windows locally on Windows; iOS/Mac Catalyst on macOS `27.2` via VS Code Remote - SSH).
- [ ] Validate graphics-context/device recreation while animation is active.
- [ ] Add backend-independent animation unit tests.
- [ ] Preserve the independent static rendering regression coverage provided by `SilkTriangleSample.UITests`.
- [ ] Document animation asset requirements, supported formats, limitations, and test procedures.
- [ ] Keep editor, ECS rewrite, advanced PBR, advanced shadows and post-processing outside this stage unless separately scoped.

> [!IMPORTANT]
> Do not mark Stage 11 complete merely because animation engine unit tests pass; both the engine functionality and the dedicated `AnimationSample` + `AnimationSample.UITests` must be validated on all four platforms.

### Stage 11 Phase 1 Status (Windows & Android — In Progress; Apple Pending Phase 2)
- Implemented backend-neutral skeletal animation (`Skeleton3D`, `SkinningData`, `AnimationClip3D`, `AnimationEvaluator`), `AssimpModelImporter` skeletal/clip conversion for `assets/Idle.fbx` (88 bones, 5,420 vertices, 8,222 faces, `mixamo.com` clip), and `Orbit3D.Graphics.Silk` GPU skinning using an `RGBA32F` `4x128` `uBoneTexture` (`texelFetch`, `MaxSupportedBones = 128`) compatible with both OpenGL 3.3 Core (Windows WGL) and OpenGL ES 3.0 (Android).
- Created `games/3D/AnimationSample/` (`com.remotecodehub.animationsample`) and `games/3D/AnimationSample.UITests/` and integrated both into `games/3D/Orbit3D.Games.slnx`.
- **Validated**: `Orbit3D.Engine.Tests` (`net11.0`: 60/60, `net11.0-windows10.0.19041.0`: 74/74), `AnimationSample.UITests` (`Category=Artifact`: 4/4, `Platform=Android` on `emulator-5554`: 5/5), and direct Windows `AnimationSample.exe` WGL execution (`OpenGL 3.3.0`, RTX 3060).
- **Blocked on host**: Windows Appium UI tests (`WinAppDriver.exe` not installed; `wincore` sees 0 child windows in non-interactive session).
- **Deferred to Phase 2**: iOS (`xcuitest`) and Mac Catalyst (`mac2`) implementation and validation on macOS `27.2`.
