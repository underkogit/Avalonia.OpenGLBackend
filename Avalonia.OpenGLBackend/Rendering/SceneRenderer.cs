using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.OpenGLBackend.Core;
using Avalonia.OpenGLBackend.Rendering.Meshes;
using Silk.NET.OpenGL;

namespace Avalonia.OpenGLBackend.Rendering;

public sealed class SceneRenderer : OpenGlControlBase
{
    private GL? _gl;
    private ShaderProgram? _fallbackProgram;
    private int _uMvpLocation;
    private int _uColorLocation;

    private readonly ConcurrentQueue<Action> _pendingActions = new();
    private bool _initialized;

    private enum DragMode
    {
        None,
        Orbit,
        Pan
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int WglSwapIntervalEXT(int interval);
    public event Action<int>? FpsUpdated;

    private DragMode _dragMode = DragMode.None;
    private Point _lastMousePos;
    private bool _dragging;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Stopwatch _fpsClock = Stopwatch.StartNew();
    private double _lastTime;
    private int _frameCount;

    public Scene Scene { get; } = new();
    public Camera Camera { get; } = new();

    public bool VSyncEnabled { get; set; } = true;
    public bool ContinuousRendering { get; set; }

    public void EnqueueGlAction(Action action)
    {
        _pendingActions.Enqueue(action);
        RequestNextFrameRendering();
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        _gl = GL.GetApi(gl.GetProcAddress);
        ApplySwapInterval(gl);

        ShaderProgram.SetGl(_gl);
        Mesh.SetGl(_gl);
        SceneObject.SetGl(_gl);

        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Lequal);

        try
        {
            _fallbackProgram = ShaderProgram.Create(SceneObject.DefaultVertexShader, SceneObject.DefaultFragmentShader);
            _uMvpLocation = _fallbackProgram.GetUniformLocation("uMvp");
            _uColorLocation = _fallbackProgram.GetUniformLocation("uColor");
        }
        catch (Exception ex)
        {
            Console.WriteLine($">>> Fallback shader error: {ex.Message}");
            return;
        }

        foreach (var obj in Scene.Objects)
        {
            try
            {
                obj.EnsureInitialized();
            }
            catch (Exception ex)
            {
                Console.WriteLine($">>> Init '{obj.Name}' failed: {ex.Message}");
            }
        }

        _lastTime = _clock.Elapsed.TotalSeconds;
        _initialized = true;
        ProcessPendingActions();
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        foreach (var obj in Scene.Objects)
        {
            try
            {
                obj.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($">>> Dispose '{obj.Name}' failed: {ex.Message}");
            }
        }

        _fallbackProgram?.Dispose();
        _fallbackProgram = null;
        _initialized = false;
        _gl?.Dispose();
        _gl = null;
    }

    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_gl is null) return;

        _frameCount++;
        if (_fpsClock.ElapsedMilliseconds >= 1000)
        {
            FpsUpdated?.Invoke(_frameCount);
            _frameCount = 0;
            _fpsClock.Restart();
        }

        var now = _clock.Elapsed.TotalSeconds;
        var dt = now - _lastTime;
        _lastTime = now;
        if (dt < 0) dt = 0;
        if (dt > 0.1) dt = 0.1;
        var dtf = (float)dt;

        ProcessPendingActions();

        foreach (var obj in Scene.Objects)
        {
            obj.EnsureInitialized();
            obj.Update(dtf);
        }

        if (ContinuousRendering || Scene.Objects.Any(obj => obj.IsAnimated))
            RequestNextFrameRendering();

        var topLevel = TopLevel.GetTopLevel(this);
        var scaling = topLevel?.RenderScaling ?? 1.0;
        var width = Math.Max(1, (int)(Bounds.Width * scaling));
        var height = Math.Max(1, (int)(Bounds.Height * scaling));

        _gl.Viewport(0, 0, (uint)width, (uint)height);
        var background = Scene.BackgroundColor;
        _gl.ClearColor(background.X, background.Y, background.Z, background.W);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var aspect = width / (float)height;
        var view = Camera.GetViewMatrix();
        var projection = Camera.GetProjectionMatrix(aspect);

        foreach (var obj in Scene.Objects)
        {
            if (!obj.Visible) continue;
            obj.Render(view, projection, _fallbackProgram, _uMvpLocation, _uColorLocation);
        }
    }

    public void HandlePointerPressed(PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;

        DragMode mode;
        if (props.IsMiddleButtonPressed) mode = DragMode.Pan;
        else if (props.IsRightButtonPressed) mode = DragMode.Orbit;
        else return;

        var position = e.GetPosition(this);
        _dragging = true;
        e.Pointer.Capture(this);

        EnqueueGlAction(() =>
        {
            _dragMode = mode;
            _lastMousePos = position;
        });
    }

    public void HandlePointerMoved(PointerEventArgs e)
    {
        if (!_dragging) return;

        var position = e.GetPosition(this);
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        var height = Math.Max(1, (int)(Bounds.Height * scaling));

        EnqueueGlAction(() => ApplyDrag(position, height));
    }

    private void ApplyDrag(Point position, int height)
    {
        var delta = position - _lastMousePos;
        _lastMousePos = position;

        var dx = Math.Clamp((float)delta.X, -50f, 50f);
        var dy = Math.Clamp((float)delta.Y, -50f, 50f);

        if (_dragMode == DragMode.Orbit)
        {
            Camera.Yaw -= dx * 0.01f;
            Camera.Pitch += dy * 0.01f;
            const float limit = 1.5533f;
            Camera.Pitch = Math.Clamp(Camera.Pitch, -limit, limit);
        }
        else if (_dragMode == DragMode.Pan)
        {
            var forward = Vector3.Normalize(Camera.Target - Camera.GetEyePosition());
            var right = Vector3.Cross(forward, Vector3.UnitY);
            if (right.LengthSquared() < 1e-6f) right = Vector3.UnitX;
            right = Vector3.Normalize(right);
            var up = Vector3.Normalize(Vector3.Cross(right, forward));

            var fovYRad = Camera.FovDegrees * MathF.PI / 180f;
            var worldPerPixel = 2f * Camera.Radius * MathF.Tan(fovYRad * 0.5f) / height;
            worldPerPixel = Math.Clamp(worldPerPixel, 0.0005f, 0.05f);

            Camera.Target += -right * dx * worldPerPixel + up * dy * worldPerPixel;
        }
    }

    public void HandlePointerReleased(PointerReleasedEventArgs e)
    {
        _dragging = false;
        e.Pointer.Capture(null);
        EnqueueGlAction(() => _dragMode = DragMode.None);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
        EnqueueGlAction(() => _dragMode = DragMode.None);
    }

    public void SetObjectPosition(string name, Vector3 position) => EnqueueGlAction(() =>
    {
        var obj = Scene.Objects.FirstOrDefault(o => o.Name == name);
        obj?.Transform.SetPosition(position.X, position.Y, position.Z);
    });

    public void SetObjectRotationDegrees(string name, Vector3 euler) => EnqueueGlAction(() =>
    {
        var obj = Scene.Objects.FirstOrDefault(o => o.Name == name);
        obj?.Transform.SetRotationDegrees(euler.X, euler.Y, euler.Z);
    });

    public void SetObjectAngularVelocity(string name, Vector3 velocity) => EnqueueGlAction(() =>
    {
        var obj = Scene.Objects.FirstOrDefault(o => o.Name == name);
        if (obj is not null) obj.AngularVelocity = velocity;
    });

    public void SetObjectLinearVelocity(string name, Vector3 velocity) => EnqueueGlAction(() =>
    {
        var obj = Scene.Objects.FirstOrDefault(o => o.Name == name);
        if (obj is not null) obj.LinearVelocity = velocity;
    });

    public void SetObjectWireframe(string name, bool wireframe) => EnqueueGlAction(() =>
    {
        var obj = Scene.Objects.FirstOrDefault(o => o.Name == name);
        if (obj is null) return;

        obj.EnsureInitialized();
        if (obj.Mesh is TriangleMesh triangleMesh) triangleMesh.Wireframe = wireframe;
    });

    public void HandlePointerWheelChanged(PointerWheelEventArgs e)
    {
        var delta = (float)e.Delta.Y;

        EnqueueGlAction(() =>
        {
            var factor = MathF.Pow(0.9f, delta * Camera.ZoomSpeed);
            Camera.Radius = Math.Clamp(Camera.Radius * factor, Camera.MinRadius, Camera.MaxRadius);
        });
    }

    private void ApplySwapInterval(GlInterface gl)
    {
        try
        {
            IntPtr swapIntervalPtr = gl.GetProcAddress("wglSwapIntervalEXT");
            if (swapIntervalPtr != IntPtr.Zero)
            {
                var wglSwapInterval = Marshal.GetDelegateForFunctionPointer<WglSwapIntervalEXT>(swapIntervalPtr);
                wglSwapInterval(VSyncEnabled ? 1 : 0);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($">>> VSync setup error: {ex.Message}");
        }
    }

    private void ProcessPendingActions()
    {
        if (!_initialized) return;
        while (_pendingActions.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Console.WriteLine($">>> GL action error: {ex.Message}");
            }
        }
    }
}