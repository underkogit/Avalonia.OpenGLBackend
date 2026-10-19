using System;
using Silk.NET.OpenGL;

namespace Avalonia.OpenGLBackend.Rendering;

public abstract class Mesh : IDisposable
{
    private static GL? _gl;
    public static void SetGl(GL gl) => _gl = gl;

    protected readonly GL Context = _gl ?? throw new InvalidOperationException("Mesh.SetGl was not called.");

    protected uint Vao;
    protected uint Vbo;
    protected uint Ebo;

    protected int VertexCount;
    protected int IndexCount;
    protected bool UseIndices;

    public virtual unsafe void Draw()
    {
        Context.BindVertexArray(Vao);

        if (UseIndices)
        {
            Context.DrawElements(GetPrimitiveType(), (uint)IndexCount, DrawElementsType.UnsignedInt, (void*)0);
        }
        else
        {
            Context.DrawArrays(GetPrimitiveType(), 0, (uint)VertexCount);
        }
    }

    protected abstract PrimitiveType GetPrimitiveType();

    public virtual void Dispose()
    {
        if (Ebo != 0)
        {
            Context.DeleteBuffer(Ebo);
            Ebo = 0;
        }

        if (Vbo != 0)
        {
            Context.DeleteBuffer(Vbo);
            Vbo = 0;
        }

        if (Vao != 0)
        {
            Context.DeleteVertexArray(Vao);
            Vao = 0;
        }

        VertexCount = 0;
        IndexCount = 0;
    }
}
