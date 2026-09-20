using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NiflySharp;
using NiflySharp.Blocks;
using NiflySharp.Structs;

namespace OpenSkyrim.NifViewer
{
	public sealed class NifMeshDefinition
	{
		public string Name { get; set; } = string.Empty;
		public List<Vector3> Vertices { get; } = new();
		public List<Vector3> Normals { get; } = new();
		public List<Vector2> Uvs { get; } = new();
		public List<int> Indices { get; } = new();
		public List<string> Textures { get; } = new();
	}

	public static class NifModelLoader
	{
		public static IReadOnlyList<NifMeshDefinition> LoadMeshDefinitions(string nifPath)
		{
			using var stream = File.OpenRead(nifPath);
			return LoadMeshDefinitions(stream);
		}

		public static IReadOnlyList<NifMeshDefinition> LoadMeshDefinitions(Stream stream)
		{
			if (stream.CanSeek)
			{
				stream.Position = 0;
			}

			var nifFile = new NifFile();
			try
			{
				if (nifFile.Load(stream, new NifFileLoadOptions()) != 0)
				{
					throw new InvalidDataException("The stream is not a valid Gamebryo/NetImmerse NIF file.");
				}
			}
			catch (InvalidDataException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new InvalidDataException("The stream is not a valid Gamebryo/NetImmerse NIF file.", ex);
			}

			return nifFile.GetShapes().Select(shape => ToMeshDefinition(nifFile, shape)).Where(d => d != null).ToList();
		}

		private static NifMeshDefinition ToMeshDefinition(NifFile nifFile, INiShape shape)
		{
			var gd = shape.GeometryData;
			var vertices = gd?.Vertices ?? (shape as BSTriShape)?.VertexPositions;
			var normals = gd?.Normals ?? (shape as BSTriShape)?.Normals;
			var uvs = gd?.UVSets ?? (shape as BSTriShape)?.UVs;
			if (vertices == null || vertices.Count == 0)
			{
				return null;
			}

			var definition = new NifMeshDefinition
			{
				Name = string.IsNullOrWhiteSpace(shape.Name.String) ? shape.GetType().Name : shape.Name.String
			};
			definition.Vertices.AddRange(vertices.Select(v => new Vector3(v.X, v.Y, v.Z)));
			if (normals != null)
			{
				definition.Normals.AddRange(normals.Select(n => new Vector3(n.X, n.Y, n.Z)));
			}

			if (uvs != null)
			{
				definition.Uvs.AddRange(uvs.Select(u => new Vector2(u.U, u.V)));
			}

			if (shape.Triangles != null)
			{
				definition.Indices.AddRange(shape.Triangles.SelectMany(t => new[] { (int)t.V1, (int)t.V2, (int)t.V3 }));
			}

			definition.Textures.AddRange(GetTexturePaths(nifFile, shape));

			return definition;
		}

		private static IEnumerable<string> GetTexturePaths(NifFile nifFile, INiShape shape)
		{
			var shader = nifFile.GetShader(shape);
			var textureSetRef = shader?.TextureSetRef;
			if (shader?.HasTextureSet != true || textureSetRef == null || textureSetRef.IsEmpty())
			{
				return Enumerable.Empty<string>();
			}

			if (nifFile.GetBlock(textureSetRef) is not BSShaderTextureSet textureSet)
			{
				return Enumerable.Empty<string>();
			}

			return textureSet.Textures
				.Select(texture => texture.Content)
				.Where(path => !string.IsNullOrWhiteSpace(path));
		}

		public static DrModel LoadDrModel(GraphicsDevice graphicsDevice, string nifPath)
		{
			using var stream = File.OpenRead(nifPath);
			return LoadDrModel(graphicsDevice, stream, Path.GetFileNameWithoutExtension(nifPath));
		}

		public static DrModel LoadDrModel(GraphicsDevice graphicsDevice, Stream nifStream, string rootName)
		{
			var root = new DrModelBone(string.IsNullOrWhiteSpace(rootName) ? "NifModel" : rootName);
			root.Children = LoadMeshDefinitions(nifStream).Select(definition =>
			{
				var vertices = new VertexPositionNormalTexture[definition.Vertices.Count];
				for (var i = 0; i < definition.Vertices.Count; i++)
				{
					vertices[i] = new VertexPositionNormalTexture(
						definition.Vertices[i],
						definition.Normals.Count > i ? definition.Normals[i] : Vector3.Up,
						definition.Uvs.Count > i ? definition.Uvs[i] : Vector2.Zero);
				}

				var mesh = new DrMesh { Name = definition.Name };
				mesh.MeshParts.Add(new DrMeshPart(graphicsDevice, vertices, definition.Indices.ToArray()));
				mesh.Tag = definition.Textures;
				return new DrModelBone(definition.Name, mesh);
			}).ToArray();

			return new DrModel(root);
		}
	}
}