# 3D Foundation Skill

## Use when
Implementing core 3D types, scene hierarchy, transforms, cameras, meshes, materials, textures, lights or renderer contracts.

## Required reading
- `AGENTS.md`
- `docs/architecture.md`
- `docs/testing.md`

## Rules
- Keep 3D model types independent from Assimp and graphics APIs.
- Prefer `System.Numerics`.
- Define ownership and disposal explicitly.
- Do not add GPU implementation in a foundation-only task.
- Do not couple 2D GameObject rendering to 3D types unless a concrete integration contract requires it.
- Document coordinate system and matrix conventions.
- Add backend-independent tests for deterministic behavior.
