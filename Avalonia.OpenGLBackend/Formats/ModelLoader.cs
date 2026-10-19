using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Silk.NET.Assimp;
using File = System.IO.File;

namespace Avalonia.OpenGLBackend.Formats;

public sealed class RawMeshData
{
    public List<(float X, float Y, float Z)> Positions { get; } = new();
    public List<(float X, float Y, float Z)> Normals { get; } = new();
    public List<(int A, int B, int C)> Triangles { get; } = new();

    public int VertexCount => Positions.Count;
    public int TriangleCount => Triangles.Count;
}

public static unsafe class ModelLoader
{
    private static readonly object Sync = new();
    private static readonly Lazy<Assimp> ApiRef = new(CreateApi);
    private static Assimp Api => ApiRef.Value;

    static ModelLoader()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (ApiRef.IsValueCreated) ApiRef.Value.Dispose();
        };
    }

    private static Assimp CreateApi()
    {
        try
        {
            return Assimp.GetApi();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to initialize the native Assimp library.", ex);
        }
    }

    private const PostProcessSteps Steps =
        PostProcessSteps.Triangulate |
        PostProcessSteps.FindDegenerates |
        PostProcessSteps.GenerateSmoothNormals |
        PostProcessSteps.PreTransformVertices;

    public static RawMeshData Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Model file not found: {filePath}");

        lock (Sync)
        {
            Scene* scene = Api.ImportFile(filePath, (uint)Steps);
            if (scene == null)
                throw new InvalidDataException($"Assimp failed to load '{filePath}': {Api.GetErrorStringS()}");

            try
            {
                var data = ConvertScene(scene);
                if (data.Triangles.Count == 0)
                    throw new InvalidDataException($"'{filePath}' contains no triangle geometry.");
                return data;
            }
            finally
            {
                Api.FreeScene(scene);
            }
        }
    }

    private static RawMeshData ConvertScene(Scene* scene)
    {
        var data = new RawMeshData();

        int totalVertices = 0;
        int totalFaces = 0;
        bool hasNormals = true;

        for (uint m = 0; m < scene->MNumMeshes; m++)
        {
            var mesh = scene->MMeshes[m];
            totalVertices += (int)mesh->MNumVertices;
            totalFaces += (int)mesh->MNumFaces;
            hasNormals &= mesh->MNormals != null;
        }

        data.Positions.EnsureCapacity(totalVertices);
        data.Triangles.EnsureCapacity(totalFaces);
        if (hasNormals)
            data.Normals.EnsureCapacity(totalVertices);

        for (uint m = 0; m < scene->MNumMeshes; m++)
        {
            var mesh = scene->MMeshes[m];
            int baseVertex = data.Positions.Count;

            for (uint i = 0; i < mesh->MNumVertices; i++)
            {
                Vector3 position = mesh->MVertices[i];
                data.Positions.Add((position.X, position.Y, position.Z));

                if (hasNormals)
                {
                    Vector3 normal = mesh->MNormals[i];
                    data.Normals.Add((normal.X, normal.Y, normal.Z));
                }
            }

            for (uint f = 0; f < mesh->MNumFaces; f++)
            {
                var face = mesh->MFaces[f];
                if (face.MNumIndices < 3) continue;

                int a = baseVertex + (int)face.MIndices[0];
                for (uint i = 1; i < face.MNumIndices - 1; i++)
                {
                    int b = baseVertex + (int)face.MIndices[i];
                    int c = baseVertex + (int)face.MIndices[i + 1];
                    data.Triangles.Add((a, b, c));
                }
            }
        }

        return data;
    }
}
