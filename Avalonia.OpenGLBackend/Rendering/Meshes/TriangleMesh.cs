using System.IO;
using Avalonia.OpenGLBackend.Formats;
using Silk.NET.OpenGL;

namespace Avalonia.OpenGLBackend.Rendering.Meshes;

public sealed class TriangleMesh : Mesh
{
    public unsafe TriangleMesh(float[] vertices, uint[] indices, bool wireframe = false)
    {
        Wireframe = wireframe;
        UseIndices = true;
        IndexCount = indices.Length;
        VertexCount = vertices.Length / 6;

        Vao = Context.GenVertexArray();
        Vbo = Context.GenBuffer();
        Ebo = Context.GenBuffer();

        Context.BindVertexArray(Vao);

        Context.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
        fixed (float* p = vertices)
            Context.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);

        Context.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ebo);
        fixed (uint* p = indices)
            Context.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(indices.Length * sizeof(uint)), p, BufferUsageARB.StaticDraw);

        const uint stride = 6u * sizeof(float);

        Context.EnableVertexAttribArray(0);
        Context.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);

        Context.EnableVertexAttribArray(1);
        Context.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));

        Context.BindVertexArray(0);
    }

    public bool Wireframe { get; set; }

    protected override PrimitiveType GetPrimitiveType() => PrimitiveType.Triangles;

    public override void Draw()
    {
        if (!Wireframe)
        {
            base.Draw();
            return;
        }

        Context.PolygonMode(GLEnum.FrontAndBack, PolygonMode.Line);
        base.Draw();
        Context.PolygonMode(GLEnum.FrontAndBack, PolygonMode.Fill);
    }

    public static TriangleMesh FromRawData(RawMeshData data, bool wireframe = false)
    {
        if (data.Positions.Count == 0)
            throw new InvalidDataException("Mesh has no vertices.");
        if (data.Triangles.Count == 0)
            throw new InvalidDataException("Mesh has no triangles.");

        int vertexCount = data.Positions.Count;
        foreach (var (a, b, c) in data.Triangles)
        {
            if ((uint)a >= (uint)vertexCount || (uint)b >= (uint)vertexCount || (uint)c >= (uint)vertexCount)
                throw new InvalidDataException("Mesh contains triangle indices outside the vertex range.");
        }

        var verts = new List<float>(data.Positions.Count * 6);
        var indices = new List<uint>(data.Triangles.Count * 3);

        bool hasNormals = data.Normals.Count == data.Positions.Count;

        if (hasNormals)
        {
            for (int i = 0; i < data.Positions.Count; i++)
            {
                var p = data.Positions[i];
                var n = data.Normals[i];
                verts.Add(p.X);
                verts.Add(p.Y);
                verts.Add(p.Z);
                verts.Add(n.X);
                verts.Add(n.Y);
                verts.Add(n.Z);
            }

            foreach (var (a, b, c) in data.Triangles)
            {
                indices.Add((uint)a);
                indices.Add((uint)b);
                indices.Add((uint)c);
            }
        }
        else
        {
            foreach (var (a, b, c) in data.Triangles)
            {
                var pa = data.Positions[a];
                var pb = data.Positions[b];
                var pc = data.Positions[c];
                var n = ComputeNormal(pa, pb, pc);
                uint baseIndex = (uint)(verts.Count / 6);
                AddVertex(verts, pa, n);
                AddVertex(verts, pb, n);
                AddVertex(verts, pc, n);
                indices.Add(baseIndex);
                indices.Add(baseIndex + 1);
                indices.Add(baseIndex + 2);
            }
        }

        return new TriangleMesh(verts.ToArray(), indices.ToArray(), wireframe);
    }

    private static void AddVertex(List<float> v, (float X, float Y, float Z) p, (float X, float Y, float Z) n)
    {
        v.Add(p.X);
        v.Add(p.Y);
        v.Add(p.Z);
        v.Add(n.X);
        v.Add(n.Y);
        v.Add(n.Z);
    }

    private static (float X, float Y, float Z) ComputeNormal(
        (float X, float Y, float Z) a, (float X, float Y, float Z) b, (float X, float Y, float Z) c)
    {
        float ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z;
        float vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;

        float nx = uy * vz - uz * vy;
        float ny = uz * vx - ux * vz;
        float nz = ux * vy - uy * vx;
        float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
        if (len < 1e-8f) return (0f, 1f, 0f);
        return (nx / len, ny / len, nz / len);
    }
}
