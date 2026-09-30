# Testing and Validation

## Unit tests
Test backend-independent behavior without a physical GPU: transforms, parent/child matrices, camera projection, asset conversion, mesh/index conversion, materials/textures, cache identity and lifecycle transitions.

## Stage 04 testing
The repository now includes targeted backend coverage for the Silk abstraction layer and the 3D host surface. The tests validate resource ownership, viewport changes, renderer lifecycle and the lightweight 3D view surface contract without depending on a live Windows UI automation host.

## Current validation status
- Android backend and sample build pass for `net10.0-android`.
- Unit tests: `Orbit3D.Engine.Tests` Windows target, 42 passed.
- Physical Android runtime/GPU: validated on M1908C3JGG with GLES; the imported tetrahedron uploads as 12 vertices/12 indices, submits one indexed draw, reports `GL=NoError`, and is visible in the captured screen.
- Android deployment used the .NET Android `Install` target so Debug fast-deployed assemblies matched the APK.
- Platform support: only Android has a native Silk surface handler; iOS, Mac Catalyst and Windows are not runtime-validated.

## Stage 05 status
The Android GLES path is visually validated on a physical device. Validation exposed two rendering defects: `System.Numerics` matrices were uploaded with the wrong memory ordering, and the Silk.NET generic `DrawElements(in nint)` overload passed the address of the managed offset variable instead of the element-buffer byte offset. The backend now uploads row-vector matrices in the layout expected by GLSL and invokes `glDrawElements` with the native offset value through a cached delegate. First-frame diagnostics report upload/draw counts, clip-space bounds and GL errors; the physical run reports one draw, 12 indices and `GL=NoError`. The 42-test Windows suite passes. Context recreation and other MAUI platform backends remain unvalidated.

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
