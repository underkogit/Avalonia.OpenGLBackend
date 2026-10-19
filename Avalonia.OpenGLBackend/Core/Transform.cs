using System;
using System.Numerics;

namespace Avalonia.OpenGLBackend.Core;

public sealed class Transform
{
    private Vector3 _position = Vector3.Zero;
    private Vector3 _rotationEuler = Vector3.Zero;
    private Vector3 _scale = Vector3.One;

    public Vector3 Position
    {
        get => _position;
        set
        {
            _position = value;
            MarkDirty();
        }
    }

    public Vector3 RotationEuler
    {
        get => _rotationEuler;
        set
        {
            _rotationEuler = value;
            MarkDirty();
        }
    }

    public Vector3 Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            MarkDirty();
        }
    }

    private Matrix4x4? _cachedModel;
    private bool _dirty = true;

    private void MarkDirty() => _dirty = true;

    public void SetPosition(float x, float y, float z) => Position = new Vector3(x, y, z);

    public void SetRotationDegrees(float x, float y, float z) => RotationEuler = new Vector3(x, y, z);

    public void SetScale(float uniformScale) => Scale = new Vector3(uniformScale);

    public void Translate(Vector3 delta) => Position += delta;

    public void Rotate(Vector3 deltaDegrees) => RotationEuler += deltaDegrees;

    public Matrix4x4 GetModelMatrix()
    {
        if (_dirty || _cachedModel is null)
        {
            const float Deg2Rad = MathF.PI / 180f;

            var rot = Quaternion.CreateFromYawPitchRoll(
                RotationEuler.Y * Deg2Rad,
                RotationEuler.X * Deg2Rad,
                RotationEuler.Z * Deg2Rad);

            _cachedModel = Matrix4x4.CreateScale(Scale)
                           * Matrix4x4.CreateFromQuaternion(rot)
                           * Matrix4x4.CreateTranslation(Position);
            _dirty = false;
        }

        return _cachedModel.Value;
    }
}
