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
