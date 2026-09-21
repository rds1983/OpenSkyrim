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

	public sealed class NifTreeNode
	{
		public string Name { get; init; } = string.Empty;
		public Matrix Pose { get; init; } = Matrix.Identity;
		public NifMeshDefinition MeshData { get; init; }
		public IReadOnlyList<NifTreeNode> Children { get; init; } = Array.Empty<NifTreeNode>();
	}

	public static class NifModelLoader
	{
		public static IReadOnlyList<NifMeshDefinition> LoadMeshDefinitions(Stream stream)
		{
			var nifFile = LoadNif(stream);

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
				Name = GetNodeName(shape)
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
					definition.Indices.Add(triangle.V1);
					definition.Indices.Add(triangle.V2);
					definition.Indices.Add(triangle.V3);
				}
			}

			var textures = GetTexturePaths(nifFile, shape);
			foreach (var texture in textures)
			{
				definition.Textures.Add(texture);
			}

			return definition;
		}

		private static NifFile LoadNif(Stream stream)
		{
			if (stream.CanSeek)
			{
				stream.Position = 0;
			}

			var nifFile = new NifFile();
			if (nifFile.Load(stream, new NifFileLoadOptions()) != 0)
			{
				throw new InvalidDataException("The stream is not a valid Gamebryo/NetImmerse NIF file.");
			}

			return nifFile;
		}

		/// <summary>
		/// Parses the NIF transform hierarchy (root NiNodes and their descendants). Vertices stay in
		/// shape-local space; each node carries its own local transform in <see cref="NifTreeNode.Pose"/>.
		/// </summary>
		public static IReadOnlyList<NifTreeNode> ParseTree(Stream stream)
		{
			var nifFile = LoadNif(stream);
			var roots = new List<NifTreeNode>();
			foreach (var rootNode in nifFile.GetRootNodes())
			{
				roots.Add(ParseNode(nifFile, rootNode));
			}

			return roots;
		}

		private static NifTreeNode ParseNode(NifFile nifFile, INiObject node)
		{
			var mesh = node as INiShape == null ? null : ToMeshDefinition(nifFile, (INiShape)node);

			var children = new List<NifTreeNode>();
			if (node is NiNode niNode)
			{
				for (var i = 0; i < niNode.Children.Count; i++)
				{
					var child = nifFile.GetBlock(niNode.Children.GetBlockRef(i));
					if (child != null)
					{
						children.Add(ParseNode(nifFile, child));
					}
				}
			}

			return new NifTreeNode
			{
				Name = GetNodeName(node),
				Pose = GetLocalTransform(node),
				MeshData = mesh,
				Children = children
			};
		}

		private static string GetNodeName(INiObject node)
		{
			var name = (node as NiObjectNET)?.Name?.String;
			return string.IsNullOrWhiteSpace(name) ? node.GetType().Name : name;
		}

		private static Matrix GetLocalTransform(INiObject obj)
		{
			if (obj is not NiAVObject node)
			{
				return Matrix.Identity;
			}

			var translation = node.Translation;
			return Matrix.CreateScale(node.Scale)
				* FromMatrix33(node.Rotation)
				* Matrix.CreateTranslation(translation.X, translation.Y, translation.Z);
		}

		private static Matrix FromMatrix33(NiflySharp.Structs.Matrix33 rotation)
		{
			// NIF rotation matrices are stored column-major (nif.xml field order:
			// m11, m21, m31, m12, m22, m32, m13, m23, m33); the names follow the
			// logical (row, column), so the XNA (row-vector) matrix is the transpose.
			return new Matrix(
				rotation.M11, rotation.M21, rotation.M31, 0,
				rotation.M12, rotation.M22, rotation.M32, 0,
				rotation.M13, rotation.M23, rotation.M33, 0,
				0, 0, 0, 1);
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
			var nodes = ParseTree(nifStream);
			var children = new List<DrModelBone>(nodes.Count);

			foreach (var node in nodes)
			{
				children.Add(BuildNode(graphicsDevice, fileSystem, node));
			}

			root.Children = children.ToArray();
			return new DrModel(root);
		}

		/// <summary>
		/// Builds a <see cref="DrModelBone"/> (and its descendants) for a parsed NIF node, applying the
		/// node's local transform as the default pose so the hierarchy places the meshes.
		/// </summary>
		public static DrModelBone BuildNode(GraphicsDevice graphicsDevice, SkyrimFileSystem fileSystem, NifTreeNode node)
		{
			var mesh = node.MeshData == null ? null : CreateMesh(graphicsDevice, fileSystem, node.MeshData);
			var bone = new DrModelBone(node.Name, mesh)
			{
				DefaultPose = new SrtTransform(node.Pose)
			};

			var children = new List<DrModelBone>(node.Children.Count);
			foreach (var child in node.Children)
			{
				children.Add(BuildNode(graphicsDevice, fileSystem, child));
			}

			bone.Children = children.ToArray();
			return bone;
		}

		/// <summary>
		/// Creates a copy of a bone hierarchy (e.g. a cached model's root) so it can be re-attached under a
		/// different parent. The cloned meshes reuse the source VertexBuffer/IndexBuffer objects, so no GPU
		/// geometry is re-uploaded. The cache itself is never mutated.
		/// </summary>
		public static DrModelBone CloneHierarchy(DrModelBone source)
		{
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			DrMesh clonedMesh = null;
			if (source.Mesh != null)
			{
				clonedMesh = new DrMesh
				{
					Name = source.Mesh.Name,
					Tag = source.Mesh.Tag
				};
				foreach (var part in source.Mesh.MeshParts)
				{
					clonedMesh.MeshParts.Add(part.Clone());
				}
			}

			var bone = new DrModelBone(source.Name, clonedMesh)
			{
				DefaultPose = source.DefaultPose,
				Tag = source.Tag
			};

			if (source.Children != null)
			{
				var children = new DrModelBone[source.Children.Length];
				for (var i = 0; i < children.Length; ++i)
				{
					children[i] = CloneHierarchy(source.Children[i]);
				}

				bone.Children = children;
			}

			return bone;
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