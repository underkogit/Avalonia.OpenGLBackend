using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.OpenGLBackend.Core;
using Avalonia.OpenGLBackend.Core.Objects;
using Avalonia.OpenGLBackend.Formats;
using Avalonia.OpenGLBackend.Rendering;
using Avalonia.OpenGLBackend.Rendering.Meshes;

namespace OpenGL.Demo;

public partial class MainWindow : Window
{
    private const string ModelPath = @"F:\KompacProjects\CNC_v1\Сборка.stl";
    private const float ModelScale = 0.1f;

    private SceneRenderer? _renderer;
    private Vector3 _modelCenter;
    private float _modelRadius = 5f;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadSceneAsync();
    }

    private async Task LoadSceneAsync()
    {
        var renderer = this.FindControl<SceneRenderer>("RRenderer");
        var host = this.FindControl<Border>("RendererHost");
        var fpsText = this.FindControl<TextBlock>("FpsText");
        if (renderer is null || host is null || fpsText is null) return;

        _renderer = renderer;
        renderer.ContinuousRendering = true;

        host.PointerPressed += (_, e) => renderer.HandlePointerPressed(e);
        host.PointerMoved += (_, e) => renderer.HandlePointerMoved(e);
        host.PointerReleased += (_, e) => renderer.HandlePointerReleased(e);
        host.PointerWheelChanged += (_, e) => renderer.HandlePointerWheelChanged(e);

        renderer.FpsUpdated += fps =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => fpsText.Text = $"FPS: {fps}");
        };

        fpsText.Text = "Loading...";

        RawMeshData raw;
        Vector3 center;
        float radius;
        try
        {
            (raw, center, radius) = await Task.Run(() =>
            {
                var data = ModelLoader.Load(ModelPath);
                var bounds = ComputeBounds(data);
                return (data, bounds.Center, bounds.Radius);
            });
        }
        catch (Exception ex)
        {
            fpsText.Text = $"Load error: {ex.Message}";
            return;
        }

        _modelCenter = center * ModelScale;
        _modelRadius = MathF.Max(radius * ModelScale * 2f, 0.5f);

        renderer.EnqueueGlAction(() =>
        {
            var model = new GameObject
            {
                Name = Path.GetFileNameWithoutExtension(ModelPath),
                Color = new Vector3(0.8f, 0.8f, 0.8f),
                MeshFactory = () => TriangleMesh.FromRawData(raw)
            };
            model.Transform.SetScale(ModelScale);
            renderer.Scene.Add(model);
        });
    }

    private void OnJogClick(object? sender, RoutedEventArgs e)
    {
        var renderer = _renderer;
        if (renderer is null || sender is not Button { Tag: string axis }) return;

        var direction = axis switch
        {
            "X-" => -Vector3.UnitX,
            "X+" => Vector3.UnitX,
            "Y-" => -Vector3.UnitY,
            "Y+" => Vector3.UnitY,
            "Z-" => -Vector3.UnitZ,
            "Z+" => Vector3.UnitZ,
            _ => Vector3.Zero
        };
        if (direction == Vector3.Zero) return;

        renderer.EnqueueGlAction(() =>
            renderer.Camera.Target += direction * (renderer.Camera.Radius * 0.1f));
    }

    private void OnHomeClick(object? sender, RoutedEventArgs e)
    {
        var renderer = _renderer;
        if (renderer is null) return;

        var center = _modelCenter;
        var radius = _modelRadius;

        renderer.EnqueueGlAction(() =>
        {
            renderer.Camera.Target = center;
            renderer.Camera.Radius = radius;
            renderer.Camera.Yaw = 0f;
            renderer.Camera.Pitch = 0.3f;
        });
    }

    private void OnStopClick(object? sender, RoutedEventArgs e)
    {
        var renderer = _renderer;
        if (renderer is null) return;

        renderer.EnqueueGlAction(() =>
        {
            foreach (var obj in renderer.Scene.Objects)
            {
                obj.AngularVelocity = Vector3.Zero;
                obj.LinearVelocity = Vector3.Zero;
            }
        });
    }

    private void OnFitClick(object? sender, RoutedEventArgs e)
    {
        var renderer = _renderer;
        if (renderer is null) return;

        var center = _modelCenter;
        var radius = _modelRadius;

        renderer.EnqueueGlAction(() =>
        {
            renderer.Camera.Target = center;
            renderer.Camera.Radius = radius;
        });
    }

    private static (Vector3 Center, float Radius) ComputeBounds(RawMeshData data)
    {
        if (data.Positions.Count == 0) return (Vector3.Zero, 5f);

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        foreach (var (x, y, z) in data.Positions)
        {
            min = Vector3.Min(min, new Vector3(x, y, z));
            max = Vector3.Max(max, new Vector3(x, y, z));
        }

        return ((min + max) * 0.5f, Vector3.Distance(min, max) * 0.5f);
    }
}
