using System.Linq;
using System.Numerics;
using Avalonia.OpenGLBackend.Rendering.Meshes;

namespace Avalonia.OpenGLBackend.Core.Objects;

public sealed class GridObject : SceneObject
{
    private readonly Vector3 _startPosition;
    private readonly Vector3 _directionX;
    private readonly Vector3 _directionY;
    private readonly int _linesX;
    private readonly int _linesY;
    private readonly float _spacingX;
    private readonly float _spacingY;

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

    public GridObject(
        Vector3 startPosition,
        Vector3 directionX,
        Vector3 directionY,
        int linesX,
        int linesY,
        float spacingX,
        float spacingY,
        Vector3 color)
    {
        if (directionX.LengthSquared() < 1e-12f)
            throw new ArgumentException("Direction vector must not be zero.", nameof(directionX));
        if (directionY.LengthSquared() < 1e-12f)
            throw new ArgumentException("Direction vector must not be zero.", nameof(directionY));
        if (linesX <= 0)
            throw new ArgumentOutOfRangeException(nameof(linesX), "Line count must be positive.");
        if (linesY <= 0)
            throw new ArgumentOutOfRangeException(nameof(linesY), "Line count must be positive.");
        if (spacingX <= 0f)
            throw new ArgumentOutOfRangeException(nameof(spacingX), "Spacing must be positive.");
        if (spacingY <= 0f)
            throw new ArgumentOutOfRangeException(nameof(spacingY), "Spacing must be positive.");

        _startPosition = startPosition;
        _directionX = Vector3.Normalize(directionX);
        _directionY = Vector3.Normalize(directionY);
        _linesX = linesX;
        _linesY = linesY;
        _spacingX = spacingX;
        _spacingY = spacingY;
        Color = color;
    }

    protected override void OnGlInit()
    {
        base.OnGlInit();
        Mesh = new LineMesh(BuildVertices());
    }

    private Vector3[] BuildVertices()
    {
        float lengthX = (_linesX - 1) * _spacingX;
        float lengthY = (_linesY - 1) * _spacingY;

        return Enumerable.Range(0, _linesX)
            .SelectMany(x =>
            {
                Vector3 p1 = _startPosition + _directionX * (x * _spacingX);
                return new[] { p1, p1 + _directionY * lengthY };
            })
            .Concat(Enumerable.Range(0, _linesY)
                .SelectMany(y =>
                {
                    Vector3 p1 = _startPosition + _directionY * (y * _spacingY);
                    return new[] { p1, p1 + _directionX * lengthX };
                }))
            .ToArray();
    }
}
