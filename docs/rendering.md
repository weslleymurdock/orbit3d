# 3D Rendering Architecture

## Goal
The renderer must use GPU acceleration. `ICanvas` is not the primary 3D triangle rasterizer.

## Selected backend
Stage 04 pins `Silk.NET` version `2.23.0` across the concrete backend packages. This is the newest stable package line resolved by NuGet in this environment and matches the .NET 10/MAUI runtime baseline. The public `Orbit3D.Engine` contracts remain backend-neutral and do not expose Silk types.

## API selection
The concrete backend is isolated behind the existing abstractions. For the current stage, the implementation targets the smallest practical API set while preserving the abstraction boundary:
- Windows/desktop: OpenGL via `Silk.NET.OpenGL`
- Android/iOS/MacCatalyst: OpenGLES via `Silk.NET.OpenGLES`
- The runtime is not claimed as GPU-validated on mobile targets in this environment; only the Windows build/test path is confirmed here.

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
