using System;
using System.Numerics;
using Avalonia.OpenGLBackend.Rendering;
using Silk.NET.OpenGL;

namespace Avalonia.OpenGLBackend.Core;

public abstract class SceneObject : IDisposable
{
    private static GL? _gl;
    public static void SetGl(GL gl) => _gl = gl;

    public string Name { get; set; } = string.Empty;
    public Transform Transform { get; } = new();
    public Vector3 Color { get; set; } = new(0.3f, 0.6f, 1.0f);
    public bool Visible { get; set; } = true;
    public Vector3 AngularVelocity { get; set; } = Vector3.Zero;
    public Vector3 LinearVelocity { get; set; } = Vector3.Zero;

    public Mesh? Mesh { get; set; }
    public Func<Mesh>? MeshFactory { get; set; }
    public bool OwnsMesh { get; set; } = true;

    protected virtual string VertexShaderSource => DefaultVertexShader;
    protected virtual string FragmentShaderSource => DefaultFragmentShader;

    public ShaderProgram? Program { get; protected set; }
    protected bool IsInitialized { get; private set; }

    public const string DefaultVertexShader = @"
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec3 aNormal;
        uniform mat4 uMvp;
        out vec3 vNormal;
        void main()
        {
            vNormal = aNormal;
            gl_Position = uMvp * vec4(aPosition, 1.0);
        }";

    public const string DefaultFragmentShader = @"
        #version 330 core
        in vec3 vNormal;
        uniform vec3 uColor;
        out vec4 FragColor;
        void main()
        {
            vec3 lightDir = normalize(vec3(0.5, 0.8, 0.6));
            float diff = max(dot(normalize(vNormal), lightDir), 0.0);
            FragColor = vec4(uColor * (0.3 + 0.7 * diff), 1.0);
        }";

    public virtual bool IsAnimated =>
        AngularVelocity.LengthSquared() > 0f || LinearVelocity.LengthSquared() > 0f;

    public void EnsureInitialized()
    {
        if (IsInitialized) return;
        OnGlInit();
        Mesh ??= MeshFactory?.Invoke();
        IsInitialized = true;
    }

    public void Update(float dt)
    {
        if (AngularVelocity.LengthSquared() > 0f)
            Transform.Rotate(AngularVelocity * dt);
        if (LinearVelocity.LengthSquared() > 0f)
            Transform.Translate(LinearVelocity * dt);
        OnUpdate(dt);
    }

    public void Render(Matrix4x4 view, Matrix4x4 projection, ShaderProgram? fallbackProgram, int fallbackMvpLoc, int fallbackColorLoc)
    {
        if (!Visible) return;
        var program = Program ?? fallbackProgram;
        if (program is null) return;

        int mvpLoc = Program is not null ? GetUniformLocation("uMvp") : fallbackMvpLoc;
        int colorLoc = Program is not null ? GetUniformLocation("uColor") : fallbackColorLoc;

        OnRender(program, view, projection, mvpLoc, colorLoc);
    }

    protected virtual void OnGlInit()
    {
        _cachedMvpLoc = -1;
        _cachedColorLoc = -1;

        if (VertexShaderSource != DefaultVertexShader || FragmentShaderSource != DefaultFragmentShader)
        {
            Program = ShaderProgram.Create(VertexShaderSource, FragmentShaderSource);
        }
    }

    protected virtual void OnUpdate(float dt) { }

    protected unsafe virtual void OnRender(ShaderProgram program, Matrix4x4 view, Matrix4x4 projection, int mvpLoc, int colorLoc)
    {
        var gl = _gl;
        var mesh = Mesh;
        if (gl is null || mesh is null) return;

        program.Use();

        var mvp = Transform.GetModelMatrix() * view * projection;

        if (mvpLoc >= 0)
            gl.UniformMatrix4(mvpLoc, 1, false, (float*)&mvp);

        var color = mesh.Color ?? Color;
        if (colorLoc >= 0)
            gl.Uniform3(colorLoc, color.X, color.Y, color.Z);

        mesh.Draw();
    }

    private int _cachedMvpLoc = -1;
    private int _cachedColorLoc = -1;

    protected int GetUniformLocation(string name)
    {
        if (Program is null) return -1;

        if (name == "uMvp")
        {
            if (_cachedMvpLoc < 0) _cachedMvpLoc = Program.GetUniformLocation(name);
            return _cachedMvpLoc;
        }
        if (name == "uColor")
        {
            if (_cachedColorLoc < 0) _cachedColorLoc = Program.GetUniformLocation(name);
            return _cachedColorLoc;
        }
        return Program.GetUniformLocation(name);
    }

    public virtual void Dispose()
    {
        if (OwnsMesh) Mesh?.Dispose();
        Mesh = null;
        _cachedMvpLoc = -1;
        _cachedColorLoc = -1;
        Program?.Dispose();
        Program = null;
        IsInitialized = false;
    }
}
