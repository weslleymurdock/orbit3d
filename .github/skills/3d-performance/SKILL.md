# 3D Performance Skill

## Use when
Optimizing model conversion, GPU uploads, render loops, resource caches, culling or frame timing.

## Required reading
- `AGENTS.md`
- `docs/performance.md`

## Rules
- Measure before optimizing.
- No per-frame Assimp traversal or vertex marshaling.
- No normal-path GPU-to-CPU readback.
- Avoid allocations in Update/Render.
- Cache CPU assets and GPU resources separately.
- Upload immutable buffers once.
- Recreate device resources after graphics-context loss.
- Start with frustum culling and simple render queues.
- Add batching/instancing only when measurements justify complexity.
