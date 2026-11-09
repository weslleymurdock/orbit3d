# 3D Rendering Architecture

## Goal
The renderer must use GPU acceleration. `ICanvas` is not the primary 3D triangle rasterizer.

## Selected backend
Stage 04 pins `Silk.NET` version `2.23.0` across the concrete backend packages. This is the newest stable package line resolved by NuGet in this environment and matches the .NET 10/MAUI runtime baseline. The public `Orbit3D.Engine` contracts remain backend-neutral and do not expose Silk types.

## API selection
The concrete backend is isolated behind the existing abstractions. For the current stage, the implementation targets the smallest practical API set while preserving the abstraction boundary:
- Windows/MacCatalyst/desktop: OpenGL (`#version 330 core`) via `Silk.NET.OpenGL` (Windows WGL OpenGL 3.3 core; Mac Catalyst CGL OpenGL 4.1 core via `CAOpenGLLayer`)
- Android/iOS: OpenGL ES 3.0 (`#version 300 es`) via `Silk.NET.OpenGL` / `Silk.NET.OpenGLES`
- All four MAUI targets (`Android`, `Windows`, `iOS`, and `MacCatalyst`) are implemented and runtime-validated on their respective target environments.

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

## Stage 07 Windows status
`UseSilkGraphics()` also registers the Windows MAUI handler. WinUI's desktop composition surface occludes a child HWND, so the handler creates a non-activating owned popup aligned with the MAUI view, passing hit-testing through to the XAML surface. A registered window class with `CS_OWNDC` backs the double-buffered WGL OpenGL 3.3 core context. Context creation, resize, render callbacks, and disposal run on the WinUI UI thread; the popup's client rectangle supplies framebuffer pixel dimensions and presentation uses `SwapBuffers`. Unload destroys the popup and invalidates context-bound GPU resources while the context is current. Windows requires a hardware/vendor driver exposing `WGL_ARB_create_context` and OpenGL 3.3. Unsupported drivers fail with an explicit platform exception rather than silently falling back to a diagnostic renderer. Android's GLES surface and renderer remain unchanged.

The Silk shader set includes `BasicLitTextured`, allowing the runtime sample to combine directional lighting with the decoded sidecar texture while keeping the shader-kind contract backend-neutral.

## Stage 08 iOS status
`UseSilkGraphics()` registers the iOS `SilkGraphicsSurfaceHandler` alongside Android and Windows, and wires iOS lifecycle callbacks (`OnResignActivation`, `DidEnterBackground`, `WillEnterForeground`, `OnActivated`). On iOS, `SilkIosSurfaceView` exports `[Export("layerClass")]` returning `CAEAGLLayer`, creates an `EAGLContext(EAGLRenderingAPI.OpenGLES3)`, and resolves OpenGL ES entry points from `/System/Library/Frameworks/OpenGLES.framework/OpenGLES` via `NativeLibrary.TryGetExport`. It creates a framebuffer object with a layer-backed color renderbuffer (`context.RenderBufferStorage((uint)All.Renderbuffer, eaglLayer)`) and a `DepthComponent24` depth renderbuffer, assigns the FBO handle to `SilkGraphicsContext.DefaultFramebuffer`, and presents frames by binding the color renderbuffer and calling `context.PresentRenderBuffer((uint)All.Renderbuffer)` on each `CADisplayLink` tick. Renderbuffer storage is allocated only once view bounds are non-zero and reallocated only when pixel dimensions change. Runtime rendering, camera interaction, orientation resize, and background/foreground context recreation are validated on the `iPhone Air — iOS 26.5` Simulator on the remote macOS host via VS Code Remote - SSH.

## Stage 09 Mac Catalyst status
`UseSilkGraphics()` registers the Mac Catalyst `SilkGraphicsSurfaceHandler` and wires scene/application lifecycle callbacks (`SceneDidDisconnect`, `WillTerminate`). Because Mac Catalyst (`macabi`) does not ship `OpenGLES.framework` or `EAGLContext` at runtime and does not expose AppKit's `NSOpenGLContext` or `CAOpenGLLayer` in `Microsoft.MacCatalyst.dll`, `SilkMacSurfaceView` creates a dynamically registered Objective-C subclass of `CAOpenGLLayer` (`OrbitSilkOpenGLLayer`) and resolves CGL and OpenGL entry points from `/System/Library/Frameworks/OpenGL.framework/OpenGL`. `copyCGLPixelFormatForDisplayMask:` configures a hardware-accelerated, double-buffered OpenGL 4.1 core-profile pixel format (`kCGLOGLPVersion_GL4_Core`, 24-bit color, 8-bit alpha, 24-bit depth). A main-run-loop `CADisplayLink` marks the layer as needing display each tick; inside `drawInCGLContext:pixelFormat:forLayerTime:displayTime:`, `SilkNativeContextLifetime` retains and adopts the compositor's `CGLContextObj`, wraps it in `SilkGraphicsContext` (`isOpenGles: false`, `presentAfterRenderCallback: true`), updates `DefaultFramebuffer` and pixel viewport dimensions from `GL_FRAMEBUFFER_BINDING` and `GL_VIEWPORT`, invokes `RenderFrame`, and calls `glFlush()` so Core Animation composites the rendered FBO. On surface suspension or disposal, `SilkNativeContextLifetime.Release()` makes the retained `CGLContextObj` current before raising `ContextLost`, allowing `GpuResourceCache.Clear()` to delete buffers, textures, shaders, and VAOs on the owning context before releasing it. Runtime execution on `macOS 15.6.1` (`Intel(R) UHD Graphics 630`, OpenGL `4.1 INTEL-23.7.2`, GLSL `4.10`) validates GLB + sidecar PNG rendering (`1` indexed draw call, `20,000` triangles), camera orbit/zoom, window resize (`1330x914` to `1247x748`), and repeated context suspension/recreation cycles.
