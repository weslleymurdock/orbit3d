# GPU Rendering Skill

## Use when
Implementing renderer abstractions, GPU resources, shaders, render passes or backend integration.

## Required reading
- `AGENTS.md`
- `docs/rendering.md`
- `docs/performance.md`

## Rules
- `ICanvas` is not the primary 3D renderer.
- Keep renderer interfaces backend-neutral.
- Never expose native backend objects through game APIs.
- Implement depth testing, culling, indexed drawing and resource binding before advanced effects.
- Avoid CPU readback.
- Compile/cache shaders appropriately.
- Define resource ownership and device-recreation behavior.
- Measure draw calls, triangles and frame time.
