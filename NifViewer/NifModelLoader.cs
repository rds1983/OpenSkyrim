using System;
using System.Collections.Generic;
using System.IO;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NiflySharp;
using NiflySharp.Blocks;
using NifViewer.Utility;

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

			var definitions = new List<NifMeshDefinition>();
			foreach (var shape in nifFile.GetShapes())
			{
				var definition = ToMeshDefinition(nifFile, shape);
				if (definition != null)
				{
					definitions.Add(definition);
				}
			}

			return definitions;
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

			foreach (var vertex in vertices)
			{
				definition.Vertices.Add(new Vector3(vertex.X, vertex.Y, vertex.Z));
			}

			if (normals != null)
			{
				foreach (var normal in normals)
				{
					definition.Normals.Add(new Vector3(normal.X, normal.Y, normal.Z));
				}
			}

			if (uvs != null)
			{
				foreach (var uv in uvs)
				{
					definition.Uvs.Add(new Vector2(uv.U, uv.V));
				}
			}

			if (shape.Triangles != null)
			{
				foreach (var triangle in shape.Triangles)
				{
					definition.Indices.Add((int)triangle.V1);
					definition.Indices.Add((int)triangle.V2);
					definition.Indices.Add((int)triangle.V3);
				}
			}

			var textures = GetTexturePaths(nifFile, shape);
			foreach (var texture in textures)
			{
				definition.Textures.Add(texture);
			}

			return definition;
		}

		private static List<string> GetTexturePaths(NifFile nifFile, INiShape shape)
		{
			var result = new List<string>();

			var shader = nifFile.GetShader(shape);
			var textureSetRef = shader?.TextureSetRef;
			if (shader?.HasTextureSet != true || textureSetRef == null || textureSetRef.IsEmpty())
			{
				return result;
			}

			if (nifFile.GetBlock(textureSetRef) is not BSShaderTextureSet textureSet)
			{
				return result;
			}

			// Keep the texture set ordering so index maps to the material slot
			// (0 = diffuse, 1 = normal, 2 = glow/emissive, ...).
			foreach (var texture in textureSet.Textures)
			{
				result.Add(texture.Content);
			}

			return result;
		}

		public static DrModel LoadDrModel(GraphicsDevice graphicsDevice, Stream nifStream, string rootName, SkyrimFileSystem fileSystem)
		{
			var root = new DrModelBone(string.IsNullOrWhiteSpace(rootName) ? "NifModel" : rootName);
			var definitions = LoadMeshDefinitions(nifStream);
			var children = new List<DrModelBone>(definitions.Count);

			foreach (var definition in definitions)
			{
				children.Add(new DrModelBone(definition.Name, CreateMesh(graphicsDevice, fileSystem, definition)));
			}

			root.Children = children.ToArray();
			return new DrModel(root);
		}

		public static DrMesh CreateMesh(GraphicsDevice graphicsDevice, SkyrimFileSystem fileSystem, NifMeshDefinition definition)
		{
			var meshBuilder = new MeshBuilder();

			for (var i = 0; i < definition.Vertices.Count; i++)
			{
				meshBuilder.AddVertex(new VertexPositionNormalTexture(
					definition.Vertices[i],
					definition.Normals.Count > i ? definition.Normals[i] : Vector3.Up,
					definition.Uvs.Count > i ? definition.Uvs[i] : Vector2.Zero));
			}

			meshBuilder.AddIndicesRange(definition.Indices);

			var mesh = new DrMesh
			{
				Name = definition.Name
			};
			var meshPart = meshBuilder.CreateMeshPart(graphicsDevice, true);
			meshPart.Material = CreateMaterial(graphicsDevice, fileSystem, definition);
			mesh.MeshParts.Add(meshPart);
			mesh.Tag = definition.Textures;
			return mesh;
		}

		private static DrMaterial CreateMaterial(GraphicsDevice graphicsDevice, SkyrimFileSystem fileSystem, NifMeshDefinition definition)
		{
			var material = new DrMaterial
			{
				Name = definition.Name
			};

			for (var i = 0; i < definition.Textures.Count; ++i)
			{
				var texturePath = definition.Textures[i];
				if (string.IsNullOrWhiteSpace(texturePath))
				{
					continue;
				}

				var texture = fileSystem.LoadTexture(graphicsDevice, texturePath);
				if (texture == null)
				{
					continue;
				}

				switch (i)
				{
					case 0:
						material.DiffuseTexture = texture;
						break;
					case 1:
						material.NormalTexture = texture;
						break;
					case 2:
						material.EmissiveTexture = texture;
						break;
				}
			}

			return material;
		}
	}
}