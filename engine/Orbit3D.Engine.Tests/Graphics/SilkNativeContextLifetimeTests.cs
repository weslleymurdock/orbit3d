using Orbit3D.Graphics.Silk;

namespace Orbit3D.Engine.Tests.Graphics;

/// <summary>
/// Verifies the native-context ownership rules shared by compositor-owned surfaces such as Mac Catalyst's <c>CAOpenGLLayer</c>.
/// The tracker is exercised with an in-memory native-handle table; actual OpenGL behavior is validated on a real Mac Catalyst app.
/// </summary>
public class SilkNativeContextLifetimeTests
{
    private sealed class NativeContextTable
    {
        public nint Current { get; private set; }
        public Dictionary<nint, int> References { get; } = [];
        public List<string> Log { get; } = [];

        public void SetCurrent(nint handle)
        {
            Current = handle;
            Log.Add($"current:{handle}");
        }

        public void Retain(nint handle)
        {
            References[handle] = References.GetValueOrDefault(handle) + 1;
            Log.Add($"retain:{handle}");
        }

        public void Release(nint handle)
        {
            Assert.True(References.GetValueOrDefault(handle) > 0, $"Context {handle} was released more often than it was retained.");
            References[handle]--;
            Log.Add($"release:{handle}");
        }
    }

    private sealed class SurfaceEvents
    {
        public event EventHandler<SilkGraphicsContextEventArgs>? ContextCreated;
        public event EventHandler<SilkGraphicsContextEventArgs>? ContextLost;

        public void RaiseContextCreated(SilkGraphicsContext context) =>
            ContextCreated?.Invoke(this, new SilkGraphicsContextEventArgs(context));

        public void RaiseContextLost(SilkGraphicsContext context)
        {
            try
            {
                ContextLost?.Invoke(this, new SilkGraphicsContextEventArgs(context));
            }
            finally
            {
                context.DisposeApi();
            }
        }
    }

    private static (SilkNativeContextLifetime Lifetime, SurfaceEvents Surface, NativeContextTable Table) Create()
    {
        var surface = new SurfaceEvents();
        var table = new NativeContextTable();
        var lifetime = new SilkNativeContextLifetime(
            surface.RaiseContextCreated,
            surface.RaiseContextLost,
            _ => 0,
            () => table.Current,
            table.SetCurrent,
            table.Retain,
            table.Release,
            isOpenGles: false);
        return (lifetime, surface, table);
    }

    [Fact]
    public void Adopt_CreatesSilkContextOnceAndRetainsNativeContext()
    {
        var (lifetime, surface, table) = Create();
        var created = new List<SilkGraphicsContext>();
        surface.ContextCreated += (_, args) => created.Add(args.Context);

        Assert.True(lifetime.Adopt(10));
        Assert.False(lifetime.Adopt(10));

        Assert.Single(created);
        Assert.Same(created[0], lifetime.Context);
        Assert.Equal(10, lifetime.NativeContext);
        Assert.Equal(1, table.References[10]);
        Assert.True(created[0].IsCurrent);
        Assert.False(created[0].IsOpenGles);
    }

    [Fact]
    public void Release_RaisesContextLostWhileNativeContextIsCurrentAndReleasesItOnce()
    {
        var (lifetime, surface, table) = Create();
        lifetime.Adopt(10);
        var context = lifetime.Context!;
        var lostWhileCurrent = false;
        var lostContexts = new List<SilkGraphicsContext>();
        surface.ContextLost += (_, args) =>
        {
            lostContexts.Add(args.Context);
            lostWhileCurrent = args.Context.IsCurrent;
        };

        lifetime.Release();
        lifetime.Release();

        Assert.Single(lostContexts);
        Assert.Same(context, lostContexts[0]);
        Assert.True(lostWhileCurrent);
        Assert.True(context.IsApiDisposed);
        Assert.False(context.IsCurrent);
        Assert.Null(lifetime.Context);
        Assert.Equal(0, lifetime.NativeContext);
        Assert.Equal(0, table.Current);
        Assert.Equal(0, table.References[10]);
    }

    [Fact]
    public void Adopt_AfterRelease_CreatesFreshContextAndNeverReusesTheStaleOne()
    {
        var (lifetime, surface, table) = Create();
        var created = new List<SilkGraphicsContext>();
        surface.ContextCreated += (_, args) => created.Add(args.Context);

        lifetime.Adopt(10);
        lifetime.Release();
        lifetime.Adopt(11);
        lifetime.Release();
        lifetime.Adopt(12);

        Assert.Equal(3, created.Count);
        Assert.Equal(3, created.Distinct().Count());
        Assert.True(created[0].IsApiDisposed);
        Assert.True(created[1].IsApiDisposed);
        Assert.False(created[2].IsApiDisposed);
        Assert.Same(created[2], lifetime.Context);
        Assert.Equal(0, table.References[10]);
        Assert.Equal(0, table.References[11]);
        Assert.Equal(1, table.References[12]);
    }

    [Fact]
    public void Adopt_ReplacementContext_RetiresPreviousContextBeforeCreatingNewOne()
    {
        var (lifetime, surface, table) = Create();
        var events = new List<string>();
        surface.ContextCreated += (_, args) => events.Add($"created@{table.Current}");
        surface.ContextLost += (_, args) => events.Add($"lost@{table.Current}:{args.Context.IsCurrent}");

        lifetime.Adopt(10);
        var first = lifetime.Context!;
        lifetime.Adopt(20);

        Assert.Equal(["created@10", "lost@10:True", "created@20"], events);
        Assert.True(first.IsApiDisposed);
        Assert.NotSame(first, lifetime.Context);
        Assert.Equal(20, lifetime.NativeContext);
        Assert.Equal(20, table.Current);
        Assert.Equal(0, table.References[10]);
        Assert.Equal(1, table.References[20]);
    }

    [Fact]
    public void Release_ReleasesNativeContextEvenWhenContextLostHandlerThrows()
    {
        var (lifetime, surface, table) = Create();
        lifetime.Adopt(10);
        var context = lifetime.Context!;
        surface.ContextLost += (_, _) => throw new InvalidOperationException("handler failure");

        Assert.Throws<InvalidOperationException>(lifetime.Release);

        Assert.True(context.IsApiDisposed);
        Assert.Equal(0, table.References[10]);
        Assert.Equal(0, table.Current);
        lifetime.Release();
        Assert.Equal(0, table.References[10]);
    }

    [Fact]
    public void Adopt_RejectsNullNativeHandle()
    {
        var (lifetime, _, _) = Create();

        Assert.Throws<ArgumentException>(() => lifetime.Adopt(0));
    }
}
