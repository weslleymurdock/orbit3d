# 3D Rendering Architecture

## Goal
The renderer must use GPU acceleration. `ICanvas` is not the primary 3D triangle rasterizer.

## Abstraction
The engine should provide backend-neutral concepts for graphics device/context, command/render context, vertex/index buffer, texture, sampler, shader/program, render target, depth buffer, mesh resource and renderer. Names may follow established repository conventions.

## Backend candidates
Evaluate OpenGL/OpenGL ES, Metal, Direct3D 11/12, Vulkan and established .NET graphics abstractions such as Silk.NET or Veldrid.

Evaluate Android, iOS, MacCatalyst and Windows support, native surface integration, shader/tooling, maintenance, resource lifetime and performance.

## Frame
1. Acquire/validate surface.
2. Update camera matrices.
3. Build visible render list.
4. Bind color/depth targets.
5. Clear required buffers.
6. Bind shader/material.
7. Bind mesh buffers.
8. Set world/view/projection state.
9. Draw indexed geometry.
10. Present/swap.

Avoid CPU readback.

## Minimum state
0.1-rc1 requires depth test/write, back-face culling, viewport, optional scissor, basic alpha blending and indexed drawing.

## Surface lifecycle
Handle creation, resize, render, suspension, recreation and disposal. Mobile suspension may invalidate native graphics contexts.
