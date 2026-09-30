# Testing and Validation

## Unit tests
Test backend-independent behavior without a physical GPU: transforms, parent/child matrices, camera projection, asset conversion, mesh/index conversion, materials/textures, cache identity and lifecycle transitions.

## Stage 04 testing
The repository now includes targeted backend coverage for the Silk abstraction layer and the 3D host surface. The tests validate resource ownership, viewport changes, renderer lifecycle and the lightweight 3D view surface contract without depending on a live Windows UI automation host.

## Current validation status
- Android ARM64 Release backend and sample build pass for `net10.0-android`.
- Unit tests: `Orbit3D.Engine.Tests` Windows target, 54 passed.
- Physical Android runtime/GPU: validated on M1908C3JGG with GLES; the sample stays foregrounded, displays one draw call/one rendered object, and the tetrahedron is visible in an ADB screenshot.
- Android deployment used a standalone Release APK installed directly through ADB.
- Native Silk surfaces are implemented for Android and Windows; iOS and Mac Catalyst remain unsupported.
- The existing `Orbit.Engine` Windows target builds. The legacy 2D `Orbit` app build remains blocked by its existing `..\..\..\engine\Orbit.Engine\Orbit.Engine.csproj` reference, which resolves to the missing `games/engine` directory from that sample.

## Stage 05 status
The Android GLES path is visually validated on a physical device. Validation exposed two rendering defects: `System.Numerics` matrices were uploaded with the wrong memory ordering, and the Silk.NET generic `DrawElements(in nint)` overload passed the address of the managed offset variable instead of the element-buffer byte offset. The backend now uploads row-vector matrices in the layout expected by GLSL and invokes `glDrawElements` with the native offset value through a cached delegate. First-frame diagnostics report upload/draw counts, clip-space bounds and GL errors; the physical run reports one draw, 12 indices and `GL=NoError`. The 42-test Windows suite passes. Context recreation and other MAUI platform backends remain unvalidated.

## Stage 06 status
Stage 06 closes the remaining runtime-completeness gaps by aligning GPU resources with the active `IRenderDevice`, invalidating stale resources when a graphics context is lost, and tracking per-frame renderer metrics. CPU-side assets stay available while the new device lazily rebuilds mesh and texture GPU resources from the original `Mesh3D` and `Texture2D` data. Camera interaction remains sample-local and updates the runtime `Camera3D.Transform`, while the renderer reports `FrameTime`, `FramesPerSecond`, `DrawCalls`, and `RenderedItems` from actual draw execution.

The Android sample was also verified on a physical device. ADB logcat identified a startup crash caused by updating the MAUI status label directly from the OpenGL `GLThread`; the sample now dispatches status updates to the UI thread and throttles metric-label updates. Device validation additionally caught and fixed the initial camera offset pointing away from the scene. The imported tetrahedron is visible in the ADB screenshot with live frame and draw metrics, responds to orbit input, and renders again after returning from the launcher and recreating its GLES surface; the app-specific log contains no fatal exception after the fix.

## Stage 07 Windows status
The Windows sample was launched with `dotnet run -f net10.0-windows10.0.19041.0`. Initial logs showed a valid WGL context and an indexed draw, but a screenshot revealed that WinUI's desktop-composition surface covered the WGL child window. The handler now uses a non-activating owned popup aligned over the MAUI view, with a private WGL device context and hit-testing passed through to XAML. The Toyota model and its sidecar texture are visibly rendered. The run logged one imported mesh, a decoded 1008x752 PNG, one queued item, one indexed draw and 20,000 triangles.

The Windows sample uses the actual Toyota GLB and its matching PNG from `assets/`. Both checked-in GLBs have geometry only and omit material/image/UV data; the sample binds the same-basename PNG as a real texture and computes a sample-only side projection for missing UVs. This limitation affects UV alignment and does not validate author-authored glTF material import.

Resizing the host window from 1440x753 to 1100x700 updated the graphics surface from 1424x669 to 1084x616 pixels while keeping the model visible. Closing the window logged context loss while the WGL context was current, allowing GPU resources to be disposed before native context destruction. The captured screenshots and stdout/stderr logs are in the session artifact folder. The later DevFlow-enabled run restored `Microsoft.Maui.DevFlow.Agent` `0.1.0-preview.12.26421.1` for the Windows Debug target and was launched with `dotnet run -f net10.0-windows10.0.19041.0`. The `SilkTriangleSample` WinUI agent connected on port 9223; its live visual tree exposed the MAUI surface, the native `Orbit3D.Silk.WglSurface` popup, and frame metrics reporting one draw call and one rendered object. DevFlow's regular XAML screenshot does not include that separate WGL popup, so `devflow-windows-native-capture.png` was captured from the Windows desktop; it shows the Toyota model rendered in the running window. DevFlow console logs confirm WGL context creation, GLB/sidecar texture import, an indexed draw of 20,000 triangles, and subsequent graphics-surface resize events (1232x575 and 1364x718). The DevFlow warning/error log query was empty. The standalone `console.err` capture is empty; `debug.out` belongs to an earlier broker startup and contains only its port assignment, not application diagnostics from the DevFlow run. A separate CDB-attached diagnostic run captured a second-chance `0xc000027b` exception in `Microsoft.UI.Xaml.dll`; this was not reproduced by the final `dotnet run`, resize, and normal-close validation.

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
