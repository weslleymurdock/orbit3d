using System.Runtime.InteropServices;
using Orbit3D.Engine;
using Orbit3D.Engine.Graphics;
using Silk.NET.OpenGL;

namespace Orbit3D.Graphics.Silk;

/// <summary>
/// A minimal, backend-owned device that implements the Orbit3D abstractions and manages resource lifetime.
/// </summary>
public sealed class SilkRenderDevice : IRenderDevice
{
    private readonly List<IGraphicsResource> resources = [];
    private readonly DrawElementsFunction drawElements;

    /// <summary>Creates a device bound to a current native Silk graphics context.</summary>
    public SilkRenderDevice(SilkGraphicsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.EnsureCurrent();
        Context = context;
        var drawElementsAddress = context.GetProcAddress("glDrawElements");
        if (drawElementsAddress == 0)
            throw new InvalidOperationException("The graphics context does not expose glDrawElements.");
        drawElements = Marshal.GetDelegateForFunctionPointer<DrawElementsFunction>(drawElementsAddress);
    }

    internal SilkGraphicsContext Context { get; }

    internal void DrawElements(PrimitiveType topology, uint indexCount, DrawElementsType indexType, nint indexOffset)
    {
        EnsureActive();
        Context.EnsureCurrent();
        drawElements(topology, indexCount, indexType, indexOffset);
    }

    internal void AbandonResources()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;
        foreach (var resource in resources.ToArray())
            ((SilkResource)resource).Abandon();
    }

    /// <inheritdoc />
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public IVertexBuffer CreateVertexBuffer(VertexBufferDescription description, ReadOnlySpan<byte> data)
    {
        EnsureActive();
        Context.EnsureCurrent();
        ValidateDescription(description, data.Length, nameof(description));

        var gl = Context.Api;
        var vertexArray = gl.GenVertexArray();
        var buffer = gl.GenBuffer();
        try
        {
            gl.BindVertexArray(vertexArray);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
            gl.BufferData(BufferTargetARB.ArrayBuffer, data, BufferUsageARB.StaticDraw);

            ConfigureVertexLayout(gl, description.StrideInBytes);
            gl.BindVertexArray(0);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            return Register(new SilkVertexBuffer(this, description, buffer, vertexArray));
        }
        catch
        {
            gl.BindVertexArray(0);
            gl.DeleteBuffer(buffer);
            gl.DeleteVertexArray(vertexArray);
            throw;
        }
    }

    /// <inheritdoc />
    public IIndexBuffer CreateIndexBuffer(IndexBufferDescription description, ReadOnlySpan<byte> data)
    {
        EnsureActive();
        Context.EnsureCurrent();
        if (description.IndexCount <= 0 || !Enum.IsDefined(description.Format))
            throw new ArgumentException("Index buffer description is invalid.", nameof(description));

        var indexSize = description.Format == IndexFormat.UInt16 ? sizeof(ushort) : sizeof(uint);
        var expected = checked(description.IndexCount * indexSize);
        ValidateDataLength(expected, data.Length);

        var gl = Context.Api;
        var vertexArray = gl.GenVertexArray();
        var buffer = gl.GenBuffer();
        try
        {
            gl.BindVertexArray(vertexArray);
            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, buffer);
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, data, BufferUsageARB.StaticDraw);
            gl.BindVertexArray(0);
            gl.DeleteVertexArray(vertexArray);
            return Register(new SilkIndexBuffer(this, description, buffer));
        }
        catch
        {
            gl.BindVertexArray(0);
            gl.DeleteVertexArray(vertexArray);
            gl.DeleteBuffer(buffer);
            throw;
        }
    }

    /// <inheritdoc />
    public ITextureResource CreateTexture(TextureDescription description, ReadOnlySpan<byte> data)
    {
        EnsureActive();
        Context.EnsureCurrent();
        if (description.Width <= 0 || description.Height <= 0 || !Enum.IsDefined(description.Format))
            throw new ArgumentException("Texture description is invalid.", nameof(description));

        ValidateDataLength(description.DataSizeInBytes, data.Length);
        var gl = Context.Api;
        var texture = gl.GenTexture();
        try
        {
            gl.BindTexture(TextureTarget.Texture2D, texture);
            gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            var (internalFormat, pixelFormat) = description.Format switch
            {
                TextureFormat.R8Unorm => (InternalFormat.R8, PixelFormat.Red),
                TextureFormat.Rgba8Unorm => (InternalFormat.Rgba8, PixelFormat.Rgba),
                TextureFormat.Rgba8Srgb => (InternalFormat.Srgb8Alpha8, PixelFormat.Rgba),
                _ => throw new ArgumentOutOfRangeException(nameof(description))
            };
            gl.TexImage2D(TextureTarget.Texture2D, 0, (int)internalFormat,
                (uint)description.Width, (uint)description.Height, 0, pixelFormat, PixelType.UnsignedByte, data);
            gl.BindTexture(TextureTarget.Texture2D, 0);
            return Register(new SilkTextureResource(this, description, texture));
        }
        catch
        {
            gl.BindTexture(TextureTarget.Texture2D, 0);
            gl.DeleteTexture(texture);
            throw;
        }
    }

    /// <inheritdoc />
    public IShaderProgram CreateShaderProgram(ShaderProgramDescription description)
    {
        EnsureActive();
        Context.EnsureCurrent();
        ArgumentNullException.ThrowIfNull(description);
        return Register(new SilkShaderProgram(this, description, CompileProgram(description)));
    }

    /// <inheritdoc />
    public IRenderPipeline CreatePipeline(RenderPipelineDescription description)
    {
        EnsureActive();
        Context.EnsureCurrent();
        ArgumentNullException.ThrowIfNull(description);
        ValidateOwned(description.ShaderProgram);
        return Register(new SilkRenderPipeline(this, description));
    }

    /// <inheritdoc />
    public IRenderTarget CreateRenderTarget(RenderTargetDescription description)
    {
        EnsureActive();
        Context.EnsureCurrent();
        if (description.Width <= 0 || description.Height <= 0)
            throw new ArgumentException("Render target description is invalid.", nameof(description));
        _ = new TextureDescription(description.Width, description.Height, description.ColorFormat);
        var gl = Context.Api;
        var framebuffer = gl.GenFramebuffer();
        var color = gl.GenTexture();
        var depth = description.HasDepthBuffer ? gl.GenRenderbuffer() : 0;
        try
        {
            gl.BindTexture(TextureTarget.Texture2D, color);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            var (internalFormat, pixelFormat) = description.ColorFormat switch
            {
                TextureFormat.R8Unorm => (InternalFormat.R8, PixelFormat.Red),
                TextureFormat.Rgba8Unorm => (InternalFormat.Rgba8, PixelFormat.Rgba),
                TextureFormat.Rgba8Srgb => (InternalFormat.Srgb8Alpha8, PixelFormat.Rgba),
                _ => throw new ArgumentOutOfRangeException(nameof(description))
            };
            gl.TexImage2D(TextureTarget.Texture2D, 0, (int)internalFormat,
                (uint)description.Width, (uint)description.Height, 0, pixelFormat, PixelType.UnsignedByte,
                ReadOnlySpan<byte>.Empty);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
            gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, color, 0);
            if (depth != 0)
            {
                gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depth);
                gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24,
                    (uint)description.Width, (uint)description.Height);
                gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                    RenderbufferTarget.Renderbuffer, depth);
            }

            var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != GLEnum.FramebufferComplete)
                throw new InvalidOperationException($"OpenGL framebuffer creation failed with status {status}.");
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            gl.BindTexture(TextureTarget.Texture2D, 0);
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
            return Register(new SilkRenderTarget(this, description, framebuffer, color, depth));
        }
        catch
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            gl.DeleteFramebuffer(framebuffer);
            gl.DeleteTexture(color);
            if (depth != 0)
                gl.DeleteRenderbuffer(depth);
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;
        foreach (var resource in resources.ToArray())
        {
            if (Context.IsCurrent)
                resource.Dispose();
            else
                ((SilkResource)resource).Abandon();
        }
        Context.DisposeApi();
    }

    internal void ValidateOwned(IGraphicsResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (!ReferenceEquals(resource.Owner, this))
            throw new ArgumentException("Resource belongs to another device.", nameof(resource));
        if (resource.IsDisposed)
            throw new ObjectDisposedException(resource.GetType().Name);
    }

    internal void Unregister(IGraphicsResource resource) => resources.Remove(resource);

    private static void ValidateDescription(VertexBufferDescription description, int actualLength, string paramName)
    {
        if (description.VertexCount <= 0 || description.StrideInBytes <= 0)
            throw new ArgumentException("Vertex buffer description is invalid.", paramName);

        ValidateDataLength(description.DataSizeInBytes, actualLength);
    }

    private static void ValidateDataLength(int expected, int actual)
    {
        if (expected != actual)
            throw new ArgumentException("Data length does not match the resource description.");
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void DrawElementsFunction(PrimitiveType topology, uint indexCount, DrawElementsType indexType, nint indexOffset);

    private void EnsureActive()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }

    internal void EnsureContextCurrent()
    {
        EnsureActive();
        Context.EnsureCurrent();
    }

    internal static void ConfigureVertexLayout(GL gl, int stride, int firstVertex = 0)
    {
        if (stride is not (12 or 20 or 24 or 32 or 44))
            throw new ArgumentException("Vertex stride must match position, position/UV, position/normal, position/normal/UV, or position/normal/UV/tangent layout.", nameof(stride));

        var offset = checked(firstVertex * stride);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, (uint)stride, (nint)offset);
        gl.EnableVertexAttribArray(0);
        offset += sizeof(float) * 3;

        if (stride >= 24)
        {
            gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, (uint)stride, (nint)offset);
            gl.EnableVertexAttribArray(1);
            offset += sizeof(float) * 3;
        }
        if (stride == 20)
        {
            gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, (uint)stride, (nint)offset);
            gl.EnableVertexAttribArray(2);
        }
        else if (stride >= 32)
        {
            gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, (uint)stride, (nint)offset);
            gl.EnableVertexAttribArray(2);
            offset += sizeof(float) * 2;
            if (stride >= 44)
            {
                gl.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, false, (uint)stride, (nint)offset);
                gl.EnableVertexAttribArray(3);
            }
        }
    }

    private uint CompileProgram(ShaderProgramDescription description)
    {
        var gl = Context.Api;
        var vertexSource = GetVertexSource(Context.IsOpenGles);
        var fragmentSource = GetFragmentSource(description.Kind, Context.IsOpenGles);
        var vertexShader = CompileShader(ShaderType.VertexShader, vertexSource, "vertex");
        uint fragmentShader = 0;
        uint program = 0;
        try
        {
            fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource, "fragment");
            program = gl.CreateProgram();
            gl.AttachShader(program, vertexShader);
            gl.AttachShader(program, fragmentShader);
            gl.LinkProgram(program);
            gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var linked);
            if (linked == 0)
                throw new InvalidOperationException($"Shader program link failed for '{description.Name}': {gl.GetProgramInfoLog(program)}");
            return program;
        }
        catch
        {
            if (program != 0)
                gl.DeleteProgram(program);
            throw;
        }
        finally
        {
            gl.DeleteShader(vertexShader);
            if (fragmentShader != 0)
                gl.DeleteShader(fragmentShader);
        }
    }

    private uint CompileShader(ShaderType type, string source, string stage)
    {
        var gl = Context.Api;
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out var compiled);
        if (compiled != 0)
            return shader;

        var log = gl.GetShaderInfoLog(shader);
        gl.DeleteShader(shader);
        throw new InvalidOperationException($"{stage} shader compilation failed: {log}");
    }

    private static string GetVertexSource(bool gles) => gles ? """
        #version 300 es
        precision highp float;
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec3 aNormal;
        layout(location = 2) in vec2 aUv;
        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;
        out vec3 vNormal;
        out vec2 vUv;
        void main() {
            vNormal = mat3(uModel) * aNormal;
            vUv = aUv;
            gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
        }
        """ : """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec3 aNormal;
        layout(location = 2) in vec2 aUv;
        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;
        out vec3 vNormal;
        out vec2 vUv;
        void main() {
            vNormal = mat3(uModel) * aNormal;
            vUv = aUv;
            gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
        }
        """;

    private static string GetFragmentSource(ShaderProgramKind kind, bool gles)
    {
        var version = gles ? "#version 300 es\nprecision mediump float;\n" : "#version 330 core\n";
        var lighting = kind is ShaderProgramKind.BasicLit or ShaderProgramKind.BasicLitTextured
            ? "vec3 normal = length(vNormal) > 0.0001 ? normalize(vNormal) : vec3(0.0, 0.0, 1.0); float diffuse = max(dot(normal, normalize(-uLightDirection)), 0.0); color.rgb *= max(diffuse, 0.18) * uLightColor;\n"
            : string.Empty;
        var texture = kind is ShaderProgramKind.UnlitTextured or ShaderProgramKind.BasicLitTextured
            ? "color *= texture(uBaseTexture, vUv);\n"
            : string.Empty;
        return $$"""
            {{version}}
            in vec3 vNormal;
            in vec2 vUv;
            uniform vec4 uBaseColor;
            uniform vec3 uLightDirection;
            uniform vec3 uLightColor;
            uniform sampler2D uBaseTexture;
            uniform bool uUseTexture;
            out vec4 fragColor;
            void main() {
                vec4 color = uBaseColor;
                {{texture}}
                {{lighting}}
                fragColor = color;
            }
            """.Replace("#version 300 es\nprecision mediump float;\n\n", "#version 300 es\nprecision mediump float;\n");
    }

    private T Register<T>(T resource) where T : IGraphicsResource
    {
        resources.Add(resource);
        return resource;
    }

    internal abstract class SilkResource : IGraphicsResource
    {
        protected SilkResource(SilkRenderDevice owner)
        {
            Owner = owner;
        }

        public IRenderDevice Owner { get; }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            ((SilkRenderDevice)Owner).Context.EnsureCurrent();
            IsDisposed = true;
            try
            {
                OnDispose();
            }
            finally
            {
                ((SilkRenderDevice)Owner).Unregister(this);
            }
        }

        protected virtual void OnDispose()
        {
        }

        internal void Abandon()
        {
            IsDisposed = true;
            ((SilkRenderDevice)Owner).Unregister(this);
        }
    }

    internal sealed class SilkVertexBuffer(SilkRenderDevice owner, VertexBufferDescription description, uint handle, uint vertexArray) : SilkResource(owner), IVertexBuffer
    {
        public VertexBufferDescription Description { get; } = description;
        public uint Handle { get; } = handle;
        public uint VertexArray { get; } = vertexArray;
        protected override void OnDispose()
        {
            ((SilkRenderDevice)Owner).Context.Api.DeleteBuffer(Handle);
            ((SilkRenderDevice)Owner).Context.Api.DeleteVertexArray(VertexArray);
        }
    }

    internal sealed class SilkIndexBuffer(SilkRenderDevice owner, IndexBufferDescription description, uint handle) : SilkResource(owner), IIndexBuffer
    {
        public IndexBufferDescription Description { get; } = description;
        public uint Handle { get; } = handle;
        protected override void OnDispose() => ((SilkRenderDevice)Owner).Context.Api.DeleteBuffer(Handle);
    }

    internal sealed class SilkTextureResource(SilkRenderDevice owner, TextureDescription description, uint handle) : SilkResource(owner), ITextureResource
    {
        public TextureDescription Description { get; } = description;
        public uint Handle { get; } = handle;
        protected override void OnDispose() => ((SilkRenderDevice)Owner).Context.Api.DeleteTexture(Handle);
    }

    internal sealed class SilkShaderProgram(SilkRenderDevice owner, ShaderProgramDescription description, uint handle) : SilkResource(owner), IShaderProgram
    {
        public ShaderProgramDescription Description { get; } = description;
        public uint Handle { get; } = handle;
        protected override void OnDispose() => ((SilkRenderDevice)Owner).Context.Api.DeleteProgram(Handle);
    }

    internal sealed class SilkRenderPipeline(SilkRenderDevice owner, RenderPipelineDescription description) : SilkResource(owner), IRenderPipeline
    {
        public RenderPipelineDescription Description { get; } = description;
    }

    internal sealed class SilkRenderTarget(
        SilkRenderDevice owner,
        RenderTargetDescription description,
        uint framebuffer,
        uint color,
        uint depth) : SilkResource(owner), IRenderTarget
    {
        public RenderTargetDescription Description { get; } = description;
        public uint Framebuffer { get; } = framebuffer;
        public uint ColorTexture { get; } = color;
        public uint DepthBuffer { get; } = depth;
        protected override void OnDispose()
        {
            var gl = ((SilkRenderDevice)Owner).Context.Api;
            gl.DeleteFramebuffer(Framebuffer);
            gl.DeleteTexture(ColorTexture);
            if (DepthBuffer != 0)
                gl.DeleteRenderbuffer(DepthBuffer);
        }
    }
}
