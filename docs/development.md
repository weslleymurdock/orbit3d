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

## Public API
Public engine APIs require XML documentation. Prefer stable Orbit concepts over third-party implementation details.

## Dependencies
Before adding a graphics dependency evaluate target frameworks, native dependencies, license, platform coverage, surface integration, shader support and maintenance.

## Commits
Use focused commit messages. Do not mix unrelated formatting or refactoring into milestone changes.

## Editor
The editor is explicitly deferred.
