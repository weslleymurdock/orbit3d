# 3D Asset Pipeline

## Assimp role
`Assimp.MAUI 6.0.5-rc4` is the runtime importer:
https://github.com/weslleymurdock/Assimp.MAUI

Orbit public APIs must not expose Assimp wrapper/native types.

## Pipeline
`file/stream/bytes -> Assimp.MAUI -> Scene -> Orbit conversion -> Model3D/Node3D/Mesh/Material3D/Texture2D -> GPU upload -> renderer`

Import happens outside the render loop.

## Processing
Select post-processing deliberately. Typical requirements may include triangulation, joining identical vertices, normal generation and tangent generation. Do not enable every flag blindly.

## Conversion
Preserve node hierarchy, local transforms, mesh references, materials, required UVs, normals, tangents/bitangents, indices, supported vertex colors and texture references. Resolve external textures relative to the source asset and support embedded textures where the package permits.

## Lifetime
Convert native Assimp data into contiguous Orbit-owned data. Do not retain dangling references to native scene memory.

## Caching
Cache by stable asset identity plus relevant import settings. CPU asset caching and GPU resource caching are separate concerns.

## Formats
Validate representative formats actually supported by the installed Assimp.MAUI build. Prefer at least one glTF asset and one traditional format such as OBJ for the 0.1-rc1 validation set.

## Stage 07 Windows sample assets
The Windows runtime sample packages `toyota-gazoo-racing-wrt-gr-yaris-1-10.glb` and its same-basename `toyota-gazoo-racing-wrt-gr-yaris-1-10.png` sidecar from `assets/`. The checked-in GLB contains geometry only: it has no material, image, texture, or `TEXCOORD_0` attributes. The sample imports its mesh with Assimp.MAUI, decodes the real PNG to RGBA pixels, creates a `Texture2D` and material, and computes a one-time side projection only when source UVs are absent. It does not replace either asset with generated geometry or image data. This sample-specific UV fallback demonstrates the texture upload/draw path; it is not a substitute for author-authored UVs in production assets.
