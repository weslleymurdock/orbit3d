# Testing and Validation

## Unit tests
Test backend-independent behavior without a physical GPU: transforms, parent/child matrices, camera projection, asset conversion, mesh/index conversion, materials/textures, cache identity and lifecycle transitions.

## Stage 04 testing
The repository now includes targeted backend coverage for the Silk abstraction layer and the 3D host surface. The tests validate resource ownership, viewport changes, renderer lifecycle and the lightweight 3D view surface contract without depending on a live Windows UI automation host.

## Current validation status
- Build validated: Windows net10.0-windows10.0.19041.0 path
- Unit tests: `Orbit3D.Engine.Tests` Windows target, 41 passed
- Runtime/GPU validated: not performed; no Silk-backed MAUI native surface or visual sample is present
- Platform support: no OpenGL/OpenGLES target is claimed as runtime-validated

## Stage 05 status
The Windows backend project compiles with C# unsafe blocks disabled, and the test suite validates backend-neutral behavior plus rejection of a non-current graphics context. These checks do not prove shader execution or visible pixels. Triangle, cube, resize/present, context loss and physical-device validation remain outstanding.

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
