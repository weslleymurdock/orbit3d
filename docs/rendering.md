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

## Stage 05 status
The Silk backend performs OpenGL operations after a caller supplies an already-current context, including managed-span uploads, shader compile/link, framebuffer creation, clear, depth/culling/blend state, uniforms and indexed drawing. `SilkGraphicsSurface` provides an Android `GLSurfaceView` handler for GLES 3, with EGL depth configuration, context callbacks, resize and render callbacks; returning from the Android render callback presents through `GLSurfaceView`. Context identity is verified with native `eglGetCurrentContext()` so Java wrapper identity does not cause false context-loss failures. `UseSilkGraphics()` invokes Engine registration, which loads `c++_shared`, `assimp` and `assimpmaui` on Android `OnCreate` before registering the `IModelImporter` service. The sample imports the packaged tetrahedron `poly.obj` off the render loop and uploads its mesh buffers on the GL thread. Physical-device verification on M1908C3JGG confirms a visible, shaded tetrahedron, one indexed draw of 12 indices and `GL_NO_ERROR`. The matrix upload uses the transpose implied by `System.Numerics` row-vector storage when interpreted by GLSL; indexed drawing passes the element-buffer byte offset as a native pointer value rather than the address of the managed offset variable. Context recreation from retained CPU assets and native handlers for iOS, Mac Catalyst and Windows remain unvalidated. A successful backend build alone is not evidence of GPU runtime support.

The backend uses managed spans for data transfers and does not enable C# unsafe code. The `nint` value used for `DrawElements` is a byte offset into a bound index buffer, not a managed memory pointer.
