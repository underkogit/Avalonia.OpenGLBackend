using System.Numerics;
using Avalonia.OpenGLBackend.Rendering.Meshes;

namespace Avalonia.OpenGLBackend.Core.Objects;

public sealed class LineObject : SceneObject
{
    private readonly Vector3 _start;
    private readonly Vector3 _end;

    protected override string VertexShaderSource => @"
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        uniform mat4 uMvp;
        void main()
        {
            gl_Position = uMvp * vec4(aPosition, 1.0);
        }";

    protected override string FragmentShaderSource => @"
        #version 330 core
        uniform vec3 uColor;
        out vec4 FragColor;
        void main()
        {
            FragColor = vec4(uColor, 1.0);
        }";

    public LineObject(Vector3 start, Vector3 end, Vector3 color)
    {
        _start = start;
        _end = end;
        Color = color;
    }

    protected override void OnGlInit()
    {
        base.OnGlInit();
        Mesh = new LineMesh(new[] { _start, _end });
    }
}
