# 3D Performance Rules

## Target data path
`Assimp import (once) -> Orbit CPU asset (cached) -> GPU upload (once/change) -> GPU rendering (many frames)`

## Forbidden hot-path work
Do not perform per frame Assimp traversal, native property access for every vertex, vertex/index marshaling, texture decoding, GPU resource creation, GPU readback or repeated shader compilation.

## Resource strategy
Upload immutable meshes once. Decode/upload textures once. Cache resources by asset/device. If a device is recreated, recreate device-dependent resources from CPU-side data or a reloadable cache.

## Stage 04 implementation notes
The concrete backend keeps the lifetime model explicit:
- device owns resources;
- resources are created once and reused across frames;
- shader compilation and mesh uploads are not performed per frame;
- the renderer validates ownership and disposal before draw calls.
This avoids the common Stage 04 bug where a backend leaks or recreates GPU resources during normal render loops.

## Stage 05 status
The Silk path uploads buffers/textures and compiles programs at resource creation, then reuses their handles in `DrawIndexed`. Uniform lookup and render-state application currently happen per draw and can be cached after profiling. Device/context recreation and re-upload from retained CPU asset data are not integrated yet; do not treat context loss as recoverable until that lifecycle is implemented.

## Scene performance
Start with hierarchical transforms, frustum culling, a simple render queue and stable resource bindings. Add batching or instancing only when measurements justify it.

## Allocation policy
Avoid allocations in Update/Render. Prefer reusable collections and preallocated resource descriptions.

## Instrumentation
Track frame time, FPS, draw calls, triangles, visible objects and GPU resource counts.

## Validation
Benchmark representative assets and object counts, recording platform/device/configuration. Avoid conclusions from a trivial sample alone.
