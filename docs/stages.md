# Orbit3D Stages

This document is the implementation roadmap for Orbit3D. Each stage has a focused scope and a definition of done (DoD). Completed stages describe the functionality already integrated into `main`; planned stages describe the next runtime milestones.

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

- [x] Integrate the actual Assimp.MAUI 6.0.5-rc4 API.
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

## Stage 08 — iOS Silk/Metal surface

Stage 08 adds the native iOS Silk graphics surface and the platform-specific context/surface lifecycle required to render the existing Orbit3D runtime on iOS. The Engine contracts and runtime scene path remain unchanged; only the platform backend/hosting layer is extended. Validation must cover a real iOS MAUI target and the existing sample scene, including resize and lifecycle transitions.

### DoD

- [ ] Implement the iOS native Silk graphics surface.
- [ ] Select and implement the supported iOS graphics context path required by Silk.NET.
- [ ] Integrate the native surface with MAUI iOS lifecycle and layout.
- [ ] Handle context/surface creation, resize, suspension, recreation and disposal.
- [ ] Reuse the existing `IRenderDevice`/`IRenderer3D` contracts without leaking platform types.
- [ ] Render the existing Stage 07 sample scene on a real iOS target.
- [ ] Validate mesh upload, indexed drawing, depth and culling.
- [ ] Validate the GLB/sidecar texture runtime path.
- [ ] Validate camera interaction and runtime metrics.
- [ ] Validate graphics-surface recreation without stale GPU-resource use.
- [ ] Add focused platform tests where feasible.
- [ ] Document the supported iOS device/OS matrix and known limitations.
- [ ] Confirm existing Android and Windows paths remain functional.

## Stage 09 — Mac Catalyst Silk surface

Stage 09 adds the native Mac Catalyst Silk graphics surface and integrates it with the MAUI Mac Catalyst window/view lifecycle. The same backend-neutral renderer and runtime scene are reused, with platform-specific context and surface management isolated in the Silk project. The stage provides a real Mac Catalyst validation target before cross-platform stabilization.

### DoD

- [ ] Implement the Mac Catalyst native Silk graphics surface.
- [ ] Select and implement the supported Mac Catalyst graphics context path required by Silk.NET.
- [ ] Integrate the native surface with MAUI Mac Catalyst lifecycle and layout.
- [ ] Handle context/surface creation, resize, suspension, recreation and disposal.
- [ ] Reuse the existing `IRenderDevice`/`IRenderer3D` contracts.
- [ ] Render the existing sample scene on a real Mac Catalyst target.
- [ ] Validate mesh upload, indexed drawing, depth and culling.
- [ ] Validate the GLB/sidecar texture runtime path.
- [ ] Validate camera interaction and runtime metrics.
- [ ] Validate graphics-surface recreation without stale GPU-resource use.
- [ ] Add focused platform tests where feasible.
- [ ] Document the supported Mac Catalyst/macOS matrix and known limitations.
- [ ] Confirm Android, Windows and iOS paths remain functional.

## Stage 10 — cross-platform graphics validation and stabilization

Stage 10 consolidates the four MAUI graphics targets into a single supported runtime matrix and removes platform-specific instability discovered during implementation. It focuses on lifecycle correctness, rendering consistency, resource ownership, performance instrumentation and regression coverage rather than introducing new rendering features. The stage establishes a stable cross-platform baseline before animation work begins.

### DoD

- [ ] Validate Android, Windows, iOS and Mac Catalyst with the same runtime scene path.
- [ ] Validate context creation, resize, suspension, recreation and disposal on all four platforms.
- [ ] Confirm CPU asset caches survive graphics-context recreation.
- [ ] Confirm GPU caches never reuse resources from an invalid context/device.
- [ ] Confirm no GPU resource leaks across repeated surface recreation.
- [ ] Validate mesh, texture, material, depth, culling and indexed-draw behavior on all four platforms.
- [ ] Validate camera interaction and render metrics on all four platforms.
- [ ] Establish a documented supported-platform/version/device matrix.
- [ ] Add or strengthen backend-independent regression tests for lifecycle and cache behavior.
- [ ] Record platform-specific limitations and unsupported configurations.
- [ ] Measure representative frame/draw/triangle metrics on each platform.
- [ ] Verify the existing 2D Orbit rendering path remains functional.
- [ ] Update architecture, rendering, performance, testing and development documentation.

## Stage 11 — 3D animation

Stage 11 introduces runtime 3D animation after all four MAUI graphics backends have been implemented and stabilized. The first animation scope is imported skeletal/skinning animation from supported Assimp assets, while keeping animation data and evaluation backend-neutral. GPU skinning or another accelerated implementation is added only behind the existing renderer abstraction and validated across the completed platform matrix.

### DoD

- [ ] Define backend-neutral animation/skeleton runtime data.
- [ ] Extend the Assimp conversion boundary for supported skeletal animation data.
- [ ] Import bones, inverse bind transforms, weights and animation clips.
- [ ] Preserve animation data in CPU-owned assets for context recreation.
- [ ] Add runtime animation evaluation independent of the graphics backend.
- [ ] Add animated pose updates without per-frame Assimp traversal.
- [ ] Define the renderer contract required for accelerated skinning.
- [ ] Implement the selected Silk GPU skinning path.
- [ ] Validate animated meshes with a real imported asset.
- [ ] Validate animation playback, looping and time progression.
- [ ] Validate context/device recreation for animated assets.
- [ ] Validate animation rendering on Android and Windows, then iOS and Mac Catalyst.
- [ ] Add backend-independent animation tests.
- [ ] Add platform rendering/regression tests where practical.
- [ ] Document supported animation formats, limits and runtime behavior.
- [ ] Keep editor, ECS rewrite, advanced PBR, advanced shadows and post-processing outside this stage unless separately scoped.
