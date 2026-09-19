using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NiflySharp;

namespace OpenSkyrim.NifViewer
{
    public sealed class NifMeshDefinition
    {
        public string Name { get; set; } = string.Empty;
        public List<Vector3> Vertices { get; } = new();
        public List<Vector3> Normals { get; } = new();
        public List<Vector2> Uvs { get; } = new();
        public List<int> Indices { get; } = new();
    }

    public static class NifModelLoader
    {
        private static bool IsGamebryoHeaderText(string headerText)
        {
            return headerText.StartsWith("Gamebryo File Format, Version ", StringComparison.OrdinalIgnoreCase)
                || headerText.StartsWith("NetImmerse File Format, Version ", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGamebryoHeader(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanRead)
            {
                throw new InvalidOperationException("The input stream is not readable.");
            }

            var startPosition = stream.CanSeek ? stream.Position : 0;
            try
            {
                if (stream.CanSeek)
                {
                    stream.Position = startPosition;
                }

                using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
                var headerBytes = reader.ReadBytes(64);
                var headerText = Encoding.ASCII.GetString(headerBytes).TrimEnd('\0');
                return IsGamebryoHeaderText(headerText);
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = startPosition;
                }
            }
        }

        public static IReadOnlyList<NifMeshDefinition> LoadMeshDefinitions(string nifPath)
        {
            if (string.IsNullOrWhiteSpace(nifPath))
            {
                throw new ArgumentException("NIF path is required.", nameof(nifPath));
            }

            using var stream = File.OpenRead(nifPath);
            return LoadMeshDefinitions(stream);
        }

        public static IReadOnlyList<NifMeshDefinition> LoadMeshDefinitions(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanRead)
            {
                throw new InvalidOperationException("The input stream is not readable.");
            }

            var startPosition = stream.CanSeek ? stream.Position : 0;
            try
            {
                using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
                if (stream.CanSeek)
                {
                    stream.Position = startPosition;
                }

                var headerBytes = reader.ReadBytes(64);
                var headerText = Encoding.ASCII.GetString(headerBytes).TrimEnd('\0');
                if (IsGamebryoHeaderText(headerText))
                {
                    if (stream.CanSeek)
                    {
                        stream.Position = startPosition;
                    }

                    return LoadGamebryoMeshDefinitions(stream);
                }

                if (stream.CanSeek)
                {
                    stream.Position = startPosition;
                }

                var magic = reader.ReadBytes(4);
                if (magic.SequenceEqual(new byte[] { (byte)'N', (byte)'I', (byte)'F', 0 }))
                {
                    var meshCount = reader.ReadInt32();
                    var meshes = new List<NifMeshDefinition>(meshCount);

                    for (var i = 0; i < meshCount; i++)
                    {
                        var definition = new NifMeshDefinition
                        {
                            Name = reader.ReadString()
                        };

                        var vertexCount = reader.ReadInt32();
                        for (var v = 0; v < vertexCount; v++)
                        {
                            definition.Vertices.Add(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                        }

                        var normalCount = reader.ReadInt32();
                        for (var n = 0; n < normalCount; n++)
                        {
                            definition.Normals.Add(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                        }

                        var uvCount = reader.ReadInt32();
                        for (var u = 0; u < uvCount; u++)
                        {
                            definition.Uvs.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
                        }

                        var indexCount = reader.ReadInt32();
                        for (var t = 0; t < indexCount; t++)
                        {
                            definition.Indices.Add(reader.ReadInt32());
                        }

                        meshes.Add(definition);
                    }

                    return meshes;
                }

                throw new InvalidDataException("The file is not a valid NIF stream in the supported subset format.");
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = startPosition;
                }
            }
        }

        private static IReadOnlyList<NifMeshDefinition> LoadGamebryoMeshDefinitions(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanRead)
            {
                throw new InvalidOperationException("The input stream is not readable.");
            }

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            var nifFile = new NifFile();
            var loadResult = nifFile.Load(stream, new NifFileLoadOptions());
            if (loadResult != 0)
            {
                throw new InvalidDataException($"The Gamebryo NIF stream could not be loaded by Nifly (result {loadResult}).");
            }

            var definitions = new List<NifMeshDefinition>();
            foreach (var shape in nifFile.GetShapes())
            {
                if (shape == null)
                {
                    continue;
                }

                var type = shape.GetType();
                var positionProperty = type.GetProperty("VertexPositions");
                var triangleProperty = type.GetProperty("Triangles");
                var normalProperty = type.GetProperty("Normals");
                var uvProperty = type.GetProperty("UVs");

                var vertices = positionProperty?.GetValue(shape) as IEnumerable<System.Numerics.Vector3>;
                if (vertices == null)
                {
                    continue;
                }

                var definition = new NifMeshDefinition
                {
                    Name = type.GetProperty("Name")?.GetValue(shape) as string ?? type.Name
                };

                foreach (var vertex in vertices)
                {
                    definition.Vertices.Add(new Vector3(vertex.X, vertex.Y, vertex.Z));
                }

                var normals = normalProperty?.GetValue(shape) as IEnumerable<System.Numerics.Vector3>;
                if (normals != null)
                {
                    foreach (var normal in normals)
                    {
                        definition.Normals.Add(new Vector3(normal.X, normal.Y, normal.Z));
                    }
                }

                var uvs = uvProperty?.GetValue(shape) as IEnumerable;
                if (uvs != null)
                {
                    foreach (var uv in uvs)
                    {
                        var uValue = (float)uv.GetType().GetProperty("U")!.GetValue(uv)!;
                        var vValue = (float)uv.GetType().GetProperty("V")!.GetValue(uv)!;
                        definition.Uvs.Add(new Vector2(uValue, vValue));
                    }
                }

                var triangles = triangleProperty?.GetValue(shape) as IEnumerable;
                if (triangles != null)
                {
                    foreach (var triangle in triangles)
                    {
                        var v1 = Convert.ToInt32(triangle.GetType().GetProperty("V1")!.GetValue(triangle));
                        var v2 = Convert.ToInt32(triangle.GetType().GetProperty("V2")!.GetValue(triangle));
                        var v3 = Convert.ToInt32(triangle.GetType().GetProperty("V3")!.GetValue(triangle));

                        definition.Indices.Add(v1);
                        definition.Indices.Add(v2);
                        definition.Indices.Add(v3);
                    }
                }

                definitions.Add(definition);
            }

            return definitions;
        }

        public static DrModel LoadDrModel(GraphicsDevice graphicsDevice, string nifPath)
        {
            using var stream = File.OpenRead(nifPath);
            return LoadDrModel(graphicsDevice, stream, Path.GetFileNameWithoutExtension(nifPath));
        }

        public static DrModel LoadDrModel(GraphicsDevice graphicsDevice, Stream nifStream, string rootName)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            if (nifStream == null)
            {
                throw new ArgumentNullException(nameof(nifStream));
            }

            var definitions = LoadMeshDefinitions(nifStream);
            var root = new DrModelBone(string.IsNullOrWhiteSpace(rootName) ? "NifModel" : rootName);

            if (definitions.Count == 0)
            {
                if (IsGamebryoHeader(nifStream))
                {
                    throw new NotSupportedException("This is a real Gamebryo NIF. The mesh parser is not implemented yet, so the file cannot be loaded into DrModel.");
                }

                return new DrModel(root);
            }

            var children = new List<DrModelBone>(definitions.Count);

            foreach (var definition in definitions)
            {
                var mesh = new DrMesh { Name = definition.Name };
                var vertexData = new VertexPositionNormalTexture[definition.Vertices.Count];

                for (var i = 0; i < definition.Vertices.Count; i++)
                {
                    var position = definition.Vertices[i];
                    var normal = definition.Normals.Count > i ? definition.Normals[i] : Vector3.Up;
                    var uv = definition.Uvs.Count > i ? definition.Uvs[i] : Vector2.Zero;
                    vertexData[i] = new VertexPositionNormalTexture(position, normal, uv);
                }

                var indices = definition.Indices.Count > 0 ? definition.Indices.ToArray() : Array.Empty<int>();
                mesh.MeshParts.Add(new DrMeshPart(graphicsDevice, vertexData, indices));
                children.Add(new DrModelBone(definition.Name, mesh));
            }

            root.Children = children.ToArray();
            return new DrModel(root);
        }
    }
}
