# Orbit3D Architecture

## Layers
1. Game/runtime model — scenes, objects, transforms, cameras and lights.
2. Asset pipeline — import and conversion to Orbit-owned data.
3. Graphics abstraction — backend-neutral GPU resources and render operations.
4. Platform backend — OpenGL/Metal/Direct3D/Vulkan or another selected implementation.

## Existing 2D path
`GameSceneView -> GraphicsView -> IDrawable -> ICanvas`

This remains the 2D path.

## Target 3D path
`GameSceneView3D -> GraphicsSurface -> IRenderDevice -> IRenderer3D -> GPU backend`

The scene/runtime must not know which graphics API is active.

## Dependency direction
Game code -> Orbit runtime/model -> renderer abstractions.
Asset import -> Assimp.MAUI -> Orbit-owned asset/model types.
Platform backend -> renderer abstractions -> native graphics API.

Never reverse these dependencies.

## Math
Use `System.Numerics` where practical. Document coordinate system, handedness, units, matrix convention, camera forward direction and depth range.

## 2D/3D coexistence
Do not replace the 2D renderer. Introduce 3D-specific runtime types where semantics differ. Future composition may combine 2D overlays and 3D rendering.

## Ownership
CPU asset data and GPU resources have separate lifetimes. Renderer/device owns device-dependent resources. Scene/model references must not accidentally own global resources.

## Editor boundary
0.1-rc1 is runtime-only. Do not introduce editor, inspector or editor serialization assumptions.
