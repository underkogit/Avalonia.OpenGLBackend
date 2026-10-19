using System;
using System.Numerics;

namespace Avalonia.OpenGLBackend.Core;

public sealed class Camera
{
    public Vector3 Target { get; set; } = Vector3.Zero;
    public float Radius { get; set; } = 5f;
    public float Yaw { get; set; } = 0f;
    public float Pitch { get; set; } = 0.3f;
    public float FovDegrees { get; set; } = 60f;
    public float NearPlane { get; set; } = 0.01f;
    public float FarPlane { get; set; } = 5000f;

    public float MinRadius { get; set; } = 0.05f;
    public float MaxRadius { get; set; } = 1000f;
    public float ZoomSpeed { get; set; } = 0.5f;

    public Vector3 GetEyePosition()
    {
        float x = Radius * MathF.Cos(Pitch) * MathF.Sin(Yaw);
        float y = Radius * MathF.Sin(Pitch);
        float z = Radius * MathF.Cos(Pitch) * MathF.Cos(Yaw);
        return Target + new Vector3(x, y, z);
    }

    public Matrix4x4 GetViewMatrix()
        => Matrix4x4.CreateLookAt(GetEyePosition(), Target, Vector3.UnitY);

    public Matrix4x4 GetProjectionMatrix(float aspect)
        => Matrix4x4.CreatePerspectiveFieldOfView(
            FovDegrees * MathF.PI / 180f,
            aspect,
            NearPlane,
            FarPlane);
}