# Testing and Validation

## Unit tests
Test backend-independent behavior without a physical GPU: transforms, parent/child matrices, camera projection, asset conversion, mesh/index conversion, materials/textures, cache identity and lifecycle transitions.

## Stage 04 testing
The repository now includes targeted backend coverage for the Silk abstraction layer and the 3D host surface. The tests validate resource ownership, viewport changes, renderer lifecycle and the lightweight 3D view surface contract without depending on a live Windows UI automation host.

## Current validation status
- Build: all `Orbit3D.Graphics.Silk` target frameworks passed (`net10.0`, Android, iOS, Mac Catalyst and Windows)
- Unit tests: `Orbit3D.Engine.Tests` Windows target, 41 passed
- Android sample packaging: succeeds with six warnings about duplicate Assimp native libraries and Android 16 16 KB page-size requirements
- Runtime/GPU validated: not performed; no visible triangle/cube was verified
- Device/emulator launch: Pixel 7 API 36 is available, but MAUI debug launch is blocked because no startup project is selected in VS Code
- Workload repair: `dotnet workload repair` failed because the cached source for `Microsoft.Android.Sdk.Windows.Msi.x64` was unavailable (`0x0000064c`); `dotnet workload install maui-android --skip-manifest-update` installed the required workload, with an old iOS preview manifest cleanup warning
- Platform support: no OpenGL/OpenGLES target is claimed as runtime-validated; iOS, Mac Catalyst and Windows currently have no Silk surface handler

## Stage 05 status
The backend uses managed spans and C# unsafe blocks are disabled. Windows and Android target builds pass, the Android sample packages, and the Windows backend-independent test suite passes 41/41. These checks do not prove shader execution or visible pixels. Runtime launch could not be attempted because no MAUI startup project was selected in VS Code. Cube rendering, automatic context resource restoration and physical-device validation remain outstanding.

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
