using System.Numerics;
using Silk.NET.OpenGL;

namespace Avalonia.OpenGLBackend.Rendering.Meshes;

public sealed class LineMesh : Mesh
{
    public unsafe LineMesh(IReadOnlyList<Vector3> vertices)
    {
        if (vertices.Count % 2 != 0)
            throw new ArgumentException("Количество вершин для линий должно быть чётным.", nameof(vertices));

        UseIndices = false;
        VertexCount = vertices.Count;

        var data = new float[vertices.Count * 3];
        for (int i = 0; i < vertices.Count; i++)
        {
            data[i * 3 + 0] = vertices[i].X;
            data[i * 3 + 1] = vertices[i].Y;
            data[i * 3 + 2] = vertices[i].Z;
        }

        Vao = Context.GenVertexArray();
        Vbo = Context.GenBuffer();

        Context.BindVertexArray(Vao);
        Context.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);

        fixed (float* p = data)
            Context.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(data.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);

        Context.EnableVertexAttribArray(0);
        Context.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);

        Context.BindVertexArray(0);
    }

    protected override PrimitiveType GetPrimitiveType() => PrimitiveType.Lines;
}
