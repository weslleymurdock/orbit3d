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
Game code -> Orbit.Engine (2D) / Orbit3D.Engine (3D).
Orbit3D.Engine -> Orbit.Engine (Orbit3D.Engine owns the 3D functionality and Orbit.Engine must not become coupled to 3D).
Asset import -> Assimp.MAUI -> Orbit-owned asset/model types.
Platform backend -> renderer abstractions -> native graphics API.

Never reverse these dependencies.

## Math
Use `System.Numerics` where practical.
- Coordinate system: Right-handed (Y is up)
- Handedness: Right-handed
- Units: Meters (standard convention)
- Matrix convention: Row-major (as per `System.Numerics`), multiplication order is Vector * Matrix
- Camera forward direction: -Z (negative Z)
- Front-face convention: Counter-clockwise (CCW)
- Depth range: Platform dependent (handled by renderer backend)

## 2D/3D coexistence
Do not replace the 2D renderer. Introduce 3D-specific runtime types where semantics differ. Future composition may combine 2D overlays and 3D rendering.

## Ownership
CPU asset data and GPU resources have separate lifetimes. Renderer/device owns device-dependent resources. Scene/model references must not accidentally own global resources.

## Editor boundary
0.1-rc1 is runtime-only. Do not introduce editor, inspector or editor serialization assumptions.
