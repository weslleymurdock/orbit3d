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

## Public API
Public engine APIs require XML documentation. Prefer stable Orbit concepts over third-party implementation details.

## Dependencies
Before adding a graphics dependency evaluate target frameworks, native dependencies, license, platform coverage, surface integration, shader support and maintenance.

## Commits
Use focused commit messages. Do not mix unrelated formatting or refactoring into milestone changes.

## Editor
The editor is explicitly deferred.
