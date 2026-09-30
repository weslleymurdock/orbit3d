# Testing and Validation

## Unit tests
Test backend-independent behavior without a physical GPU: transforms, parent/child matrices, camera projection, asset conversion, mesh/index conversion, materials/textures, cache identity and lifecycle transitions.

## Stage 04 testing
The repository now includes targeted backend coverage for the Silk abstraction layer and the 3D host surface. The tests validate resource ownership, viewport changes, renderer lifecycle and the lightweight 3D view surface contract without depending on a live Windows UI automation host.

## Current validation status
- Android ARM64 Release backend and sample build pass for `net10.0-android`.
- Unit tests: `Orbit3D.Engine.Tests` Windows target, 52 passed.
- Physical Android runtime/GPU: validated on M1908C3JGG with GLES; the sample stays foregrounded, displays one draw call/one rendered object, and the tetrahedron is visible in an ADB screenshot.
- Android deployment used a standalone Release APK installed directly through ADB.
- Platform support: only Android has a native Silk surface handler; iOS, Mac Catalyst and Windows are not runtime-validated.

## Stage 05 status
The Android GLES path is visually validated on a physical device. Validation exposed two rendering defects: `System.Numerics` matrices were uploaded with the wrong memory ordering, and the Silk.NET generic `DrawElements(in nint)` overload passed the address of the managed offset variable instead of the element-buffer byte offset. The backend now uploads row-vector matrices in the layout expected by GLSL and invokes `glDrawElements` with the native offset value through a cached delegate. First-frame diagnostics report upload/draw counts, clip-space bounds and GL errors; the physical run reports one draw, 12 indices and `GL=NoError`. The 42-test Windows suite passes. Context recreation and other MAUI platform backends remain unvalidated.

## Stage 06 status
Stage 06 closes the remaining runtime-completeness gaps by aligning GPU resources with the active `IRenderDevice`, invalidating stale resources when a graphics context is lost, and tracking per-frame renderer metrics. CPU-side assets stay available while the new device lazily rebuilds mesh and texture GPU resources from the original `Mesh3D` and `Texture2D` data. Camera interaction remains sample-local and updates the runtime `Camera3D.Transform`, while the renderer reports `FrameTime`, `FramesPerSecond`, `DrawCalls`, and `RenderedItems` from actual draw execution.

The Android sample was also verified on a physical device. ADB logcat identified a startup crash caused by updating the MAUI status label directly from the OpenGL `GLThread`; the sample now dispatches status updates to the UI thread and throttles metric-label updates. Device validation additionally caught and fixed the initial camera offset pointing away from the scene. The imported tetrahedron is visible in the ADB screenshot with live frame and draw metrics, responds to orbit input, and renders again after returning from the launcher and recreating its GLES surface; the app-specific log contains no fatal exception after the fix.

## Integration tests
Where practical, validate Assimp imports, hierarchy conversion, texture resolution and GPU resource creation. Ordinary unit tests must not require a platform graphics device.

## 3D sample
Before 0.1-rc1, provide a runnable sample that loads a known asset, creates a scene and perspective camera, renders through the GPU backend, supports basic interaction and exposes development metrics.

## Platform matrix
Validate every platform the selected renderer claims to support. Record unsupported configurations explicitly.

## Regression
3D changes must not silently remove or alter existing 2D rendering.

## Build
Run focused builds/tests after each change and a complete relevant build before completing a milestone.
