# Orbit3D Agent Instructions

## Project goal
Orbit3D is a fork of Orbit Engine focused on runtime 3D game support while preserving the existing .NET MAUI 2D engine. The first 3D release targets **0.1-rc1** and does not include an editor.

The primary 3D asset importer is **Assimp.MAUI 6.0.5-rc5**:
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

## Git operations and remote changes
- Do not create commits or push changes by default. Keep implementation changes in the working tree unless the task explicitly authorizes a commit/push or the requested deliverable cannot be completed without publishing the changes.
- A task explicitly requiring a GitHub Actions CI build, workflow dispatch, or CI-produced artifacts authorizes the minimum necessary commit and push of the relevant, validated changes to the intended branch when those changes must be present on GitHub for the workflow to run. This authorization is limited to that task and its required CI inputs; it is not blanket permission to publish unrelated or unfinished work.
- When the user explicitly requests a commit, push, release, or other remote write, perform only the requested remote operation and its necessary prerequisites.
- Before committing, inspect the current branch, working-tree status, staged diff, and recent history. Stage only files relevant to the task and use a descriptive commit message.
- Before pushing, verify the destination remote and branch. Never force-push, rewrite shared history, or push to a different branch unless the user explicitly requests it.
- If a required remote operation fails because of authentication, branch protection, permissions, conflicts, or network errors, preserve local changes and report the exact blocker and recovery action. Do not claim the remote operation succeeded.
