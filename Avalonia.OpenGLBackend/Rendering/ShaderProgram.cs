using System;
using Silk.NET.OpenGL;

namespace Avalonia.OpenGLBackend.Rendering;

public sealed class ShaderProgram : IDisposable
{
    private static GL? _gl;
    private readonly GL _context;

    public uint Handle { get; private set; }

    public static void SetGl(GL gl) => _gl = gl;

    private ShaderProgram(GL context, uint handle)
    {
        _context = context;
        Handle = handle;
    }

    public static ShaderProgram Create(string vertexSource, string fragmentSource)
    {
        var gl = _gl ?? throw new InvalidOperationException("ShaderProgram.SetGl was not called.");

        uint vs = 0;
        uint fs = 0;
        uint program = 0;

        try
        {
            vs = CompileShader(gl, ShaderType.VertexShader, vertexSource);
            fs = CompileShader(gl, ShaderType.FragmentShader, fragmentSource);

            program = gl.CreateProgram();
            gl.AttachShader(program, vs);
            gl.AttachShader(program, fs);
            gl.LinkProgram(program);

            gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int status);
            if (status == 0)
                throw new Exception($"Program link error: {gl.GetProgramInfoLog(program)}");

            return new ShaderProgram(gl, program);
        }
        catch
        {
            if (program != 0) gl.DeleteProgram(program);
            throw;
        }
        finally
        {
            if (vs != 0) gl.DeleteShader(vs);
            if (fs != 0) gl.DeleteShader(fs);
        }
    }

    private static uint CompileShader(GL gl, ShaderType type, string source)
    {
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);

        gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0)
        {
            var info = gl.GetShaderInfoLog(shader);
            gl.DeleteShader(shader);
            throw new Exception($"{type} compile error: {info}");
        }

        return shader;
    }

    public void Use() => _context.UseProgram(Handle);

    public int GetUniformLocation(string name) => _context.GetUniformLocation(Handle, name);

    public void Dispose()
    {
        if (Handle != 0)
        {
            _context.DeleteProgram(Handle);
            Handle = 0;
        }
    }
}
