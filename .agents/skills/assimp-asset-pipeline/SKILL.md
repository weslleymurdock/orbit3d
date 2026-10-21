# Assimp Asset Pipeline Skill

## Use when
Integrating `Assimp.MAUI 6.0.5-rc4`, importing models or converting Assimp scenes into Orbit assets.

## Required reading
- `AGENTS.md`
- `docs/asset-pipeline.md`
- Actual API in https://github.com/weslleymurdock/Assimp.MAUI

## Rules
- Verify wrapper names against the actual package source.
- Keep Assimp types at the import boundary.
- Convert to Orbit-owned contiguous data before runtime rendering.
- Import outside the frame loop.
- Select post-processing deliberately.
- Preserve hierarchy, transforms, meshes, materials, supported UVs/normals/tangents and textures.
- Handle external and embedded textures where supported.
- Dispose native scenes/wrappers correctly.
- Add representative import tests.
