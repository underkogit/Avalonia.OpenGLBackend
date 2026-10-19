using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Avalonia.OpenGLBackend.Core;

public sealed class Scene
{
    private readonly List<SceneObject> _objects = new();
    private readonly ReadOnlyCollection<SceneObject> _readOnlyObjects;

    public Scene() => _readOnlyObjects = _objects.AsReadOnly();

    public IReadOnlyList<SceneObject> Objects => _readOnlyObjects;
    public Vector4 BackgroundColor { get; set; } = Vector4.Zero;

    public void Add(SceneObject obj) => _objects.Add(obj);
    public void AddRange(IEnumerable<SceneObject> objects) => _objects.AddRange(objects);
    public bool Remove(SceneObject obj) => _objects.Remove(obj);
    public void Clear() => _objects.Clear();
}
