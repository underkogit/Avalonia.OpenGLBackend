using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.OpenGLBackend.Core.Objects;
using Avalonia.OpenGLBackend.Formats;
using Avalonia.OpenGLBackend.Rendering;
using Avalonia.OpenGLBackend.Rendering.Meshes;

namespace OpenGL.Demo;

public partial class MainWindow : Window
{
    private const string ModelPath = @"F:\KompacProjects\CNC_v1\Сборка.stl";
    private const float ModelScale = 0.1f;

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

            var model2 = new GameObject
            {
                Name = Path.GetFileNameWithoutExtension(ModelPath),
                Color = new Vector3(0.8f, 0.8f, 0.8f),
                Mesh = TriangleMesh.FromRawData(raw)
            };
            model2.Transform.SetScale(ModelScale);
            renderer.Scene.Add(model2);

            renderer.Camera.Target = center * ModelScale;
            renderer.Camera.Radius = MathF.Max(radius * ModelScale * 2f, 0.5f);
            renderer.Camera.Yaw = 0f;
            renderer.Camera.Pitch = 0.3f;
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
