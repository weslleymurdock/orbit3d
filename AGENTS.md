# Orbit3D Agent Instructions

## Project goal
Orbit3D is a fork of Orbit Engine focused on runtime 3D game support while preserving the existing .NET MAUI 2D engine. The first 3D release targets **0.1-rc1** and does not include an editor.

The primary 3D asset importer is **Assimp.MAUI 6.0.5-rc4**:
https://github.com/weslleymurdock/Assimp.MAUI

Assimp is an asset importer, not the Orbit renderer.

## Runtime baseline
- .NET 10 and .NET MAUI.
- Existing 2D rendering uses `GraphicsView`, `IDrawable` and `ICanvas`.
- Existing 2D behavior must remain functional.
- Do not silently remove an existing MAUI target to make 3D work.

## Architecture rules
1. Keep Orbit-facing 3D types independent from Assimp types.
2. Use `System.Numerics` for math where practical.
3. Separate game/runtime model, asset import, graphics abstraction and platform backend.
4. Game code must not directly depend on OpenGL, Metal, Direct3D or Vulkan.
5. Platform graphics APIs belong behind renderer/device abstractions.
6. Do not use `ICanvas` as the primary 3D rasterization pipeline.
7. Do not copy vertices, indices or native asset data every frame.
8. GPU resources require explicit ownership and disposal.
9. Public Orbit APIs must not expose SWIG/native Assimp objects.
10. Preserve the existing 2D API unless a breaking change is explicitly required.

## 0.1-rc1 scope
Required: transforms, scene hierarchy, cameras, meshes, materials, textures, basic lights, Assimp importing, GPU renderer abstraction, accelerated renderer path, MAUI 3D surface, depth/culling/indexed drawing/textures/basic lighting, caching, metrics, tests and a runnable sample.

Out of scope: editor, advanced PBR authoring, advanced shadows/post-processing, 3D physics, ECS rewrite and a full skeletal animation system unless required by a concrete milestone.

## Backend rule
Compare OpenGL/OpenGL ES, Metal, Direct3D, Vulkan and established cross-platform .NET graphics abstractions before selecting the implementation. Evaluate platform coverage, MAUI surface integration, shader model, maintenance and resource lifetime.

The renderer API must allow platform-specific implementations without leaking backend types into game code.

## Assimp rule
Read the actual Assimp.MAUI API before integration. Do not infer wrapper names from desktop Assimp documentation. Convert Assimp Scene/Node/Mesh/Material/Texture data into Orbit-owned data and release native resources at the correct boundary.

## Performance
- No per-frame Assimp traversal.
- No per-frame vertex marshaling.
- No GPU-to-CPU readback in normal rendering.
- Avoid allocations in Update/Render.
- Upload immutable mesh data once.
- Cache imported assets and GPU resources.
- Measure before adding batching.
- Track frame time/FPS and draw-call/triangle counts when instrumentation exists.

## Lifecycle
Respect MAUI UI-thread requirements and graphics-context requirements. Handle view creation, resize, suspension, recreation and disposal without leaking native or GPU resources.

## Code style
Follow existing repository style. Public APIs require XML documentation. Prefer focused types and explicit ownership. Avoid speculative abstractions and unrelated refactors. Do not add editor dependencies during runtime milestones.

## Validation
For every stage:
1. Build affected projects.
2. Run relevant tests.
3. Validate 2D when renderer/runtime changes.
4. Validate a real MAUI target when platform code changes.
5. Report unsupported platforms explicitly.

Read `docs/` and the relevant `.github/skills/*/SKILL.md` before implementing a 3D milestone.
