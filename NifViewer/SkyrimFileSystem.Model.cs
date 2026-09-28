using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NifViewer.Utility;
using NiflySharp;
using NiflySharp.Blocks;
using Nursia;
using System;
using System.Collections.Generic;
using System.IO;

namespace OpenSkyrim.NifViewer;

public partial class SkyrimFileSystem
{
	/// <summary>
	/// Loads a NIF model from the file system. The returned model is cached, subsequent calls with the
	/// same key return the same instance.
	/// </summary>
	/// <param name="key">The path of the NIF file, e.g. "meshes/furniture/common/commonbeddouble01.nif".</param>
	/// <returns>The loaded model, or null if the key is not a valid NIF file.</returns>
	public DrModel LoadModel(string key)
	{
		if (TryGetCached(key, out DrModel cached))
		{
			return cached;
		}

		OSK.LogInfo($"Loading '{key}'...");

		using var stream = Open(key);
		var model = LoadNifModel(stream, Path.GetFileNameWithoutExtension(key));
		SetCached(key, model);

		OSK.LogInfo($"Loaded model '{key}' ({model.Bones.Length} bone(s), {model.Meshes.Length} mesh(es))");
		return model;
	}

	/// <summary>
	/// Converts a NIF stream into a <see cref="DrModel"/>. Vertices stay in shape-local space; every node's
	/// local transform becomes the default pose of the bone it produces, so the model skeleton places the
	/// meshes without touching the vertices.
	/// </summary>
	private DrModel LoadNifModel(Stream nifStream, string rootName)
	{
		var nifFile = LoadNif(nifStream);
		var root = new DrModelBone(string.IsNullOrWhiteSpace(rootName) ? "NifModel" : rootName);

		var children = new List<DrModelBone>();
		foreach (var rootNode in nifFile.GetRootNodes())
		{
			children.Add(CreateBone(nifFile, rootNode));
		}

		root.Children = children.ToArray();
		return new DrModel(root);
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

	private DrModelBone CreateBone(NifFile nifFile, INiObject node)
	{
		var mesh = node is INiShape shape ? CreateMesh(nifFile, shape) : null;
		var bone = new DrModelBone(GetNodeName(node), mesh)
		{
			DefaultPose = new SrtTransform(GetLocalTransform(node))
		};

		if (node is NiNode niNode)
		{
			var children = new List<DrModelBone>(niNode.Children.Count);
			for (var i = 0; i < niNode.Children.Count; i++)
			{
				var child = nifFile.GetBlock(niNode.Children.GetBlockRef(i));
				if (child != null)
				{
					children.Add(CreateBone(nifFile, child));
				}
			}

			bone.Children = children.ToArray();
		}

		return bone;
	}

	private DrMesh CreateMesh(NifFile nifFile, INiShape shape)
	{
		var geometryData = shape.GeometryData;
		var positions = geometryData?.Vertices ?? (shape as BSTriShape)?.VertexPositions;
		var normals = geometryData?.Normals ?? (shape as BSTriShape)?.Normals;
		var uvs = geometryData?.UVSets ?? (shape as BSTriShape)?.UVs;
		if (positions == null || positions.Count == 0)
		{
			return null;
		}

		var name = GetNodeName(shape);
		var textures = GetTexturePaths(nifFile, shape);

		var meshBuilder = new MeshBuilder();
		for (var i = 0; i < positions.Count; i++)
		{
			meshBuilder.AddVertex(new VertexPositionNormalTexture(
				ToVector3(positions[i]),
				normals != null && normals.Count > i ? ToVector3(normals[i]) : Vector3.Up,
				uvs != null && uvs.Count > i ? ToVector2(uvs[i]) : Vector2.Zero));
		}

		if (shape.Triangles != null)
		{
			foreach (var triangle in shape.Triangles)
			{
				meshBuilder.AddIndex(triangle.V1);
				meshBuilder.AddIndex(triangle.V2);
				meshBuilder.AddIndex(triangle.V3);
			}
		}

		var meshPart = meshBuilder.CreateMeshPart(Nrs.GraphicsDevice, true);
		meshPart.Material = CreateMaterial(name, textures);

		var mesh = new DrMesh
		{
			Name = name,
			Tag = textures
		};
		mesh.MeshParts.Add(meshPart);
		return mesh;
	}

	private DrMaterial CreateMaterial(string name, IReadOnlyList<string> textures)
	{
		var material = new DrMaterial
		{
			Name = name
		};

		for (var i = 0; i < textures.Count; ++i)
		{
			var texturePath = textures[i];
			if (string.IsNullOrWhiteSpace(texturePath))
			{
				continue;
			}

			var texture = LoadTexture(texturePath);
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

	private static Vector3 ToVector3(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
	private static Vector2 ToVector2(NiflySharp.Structs.TexCoord uv) => new Vector2(uv.U, uv.V);
}
