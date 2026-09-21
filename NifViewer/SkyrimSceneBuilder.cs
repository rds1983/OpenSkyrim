using System;
using System.Collections.Generic;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

namespace OpenSkyrim.NifViewer;

public sealed class SkyrimSceneBuilder
{
	private const int MaxPlacedObjects = 4000;

	private static readonly Matrix RootRotation = Matrix.CreateRotationX(-MathHelper.PiOver2);

	private readonly GraphicsDevice _graphicsDevice;
	private readonly SkyrimFileSystem _fileSystem;

	public int PlacedObjectCount { get; private set; }
	public int LoadedModelCount { get; private set; }

	public SkyrimSceneBuilder(GraphicsDevice graphicsDevice, SkyrimFileSystem fileSystem)
	{
		_graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
		_fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
	}

	public DrModel Build(ILinkCache linkCache, ICellGetter cell)
	{
		PlacedObjectCount = 0;
		LoadedModelCount = 0;

		var name = string.IsNullOrWhiteSpace(cell.EditorID) ? "Location" : cell.EditorID;
		var root = new DrModelBone(name)
		{
			DefaultPose = new SrtTransform(RootRotation)
		};

		var children = new List<DrModelBone>();
		foreach (var placed in EnumeratePlaced(cell))
		{
			if (PlacedObjectCount >= MaxPlacedObjects)
			{
				break;
			}

			var bone = CreateBone(linkCache, placed);
			if (bone != null)
			{
				children.Add(bone);
				++PlacedObjectCount;
			}
		}

		root.Children = children.ToArray();
		return new DrModel(root);
	}

	private DrModelBone CreateBone(ILinkCache linkCache, IPlacedGetter placed)
	{
		if (placed is not IPlacedObjectGetter placedObject)
		{
			return null;
		}

		var baseLink = placedObject.Base;
		if (baseLink == null || baseLink.IsNull)
		{
			return null;
		}

		if (!baseLink.TryResolve<IPlaceableObjectGetter>(linkCache, out var baseRecord) || baseRecord == null)
		{
			return null;
		}

		if (baseRecord is not IModeledGetter modeled || modeled.Model == null)
		{
			return null;
		}

		var modelPath = modeled.Model.File?.GivenPath;
		if (string.IsNullOrWhiteSpace(modelPath))
		{
			return null;
		}

		if (!_fileSystem.FileExists(modelPath))
		{
			return null;
		}

		IReadOnlyList<NifMeshDefinition> definitions;
		try
		{
			definitions = _fileSystem.LoadMeshDefinitions(modelPath);
		}
		catch (Exception ex)
		{
			OSK.LogWarning($"Failed to load model '{modelPath}': {ex.Message}");
			return null;
		}

		if (definitions == null || definitions.Count == 0)
		{
			return null;
		}

		var transform = GetTransform(placedObject);
		var boneName = $"{baseRecord.EditorID}";
		var group = new DrModelBone(boneName)
		{
			DefaultPose = new SrtTransform(transform)
		};

		var children = new List<DrModelBone>(definitions.Count);
		for (var i = 0; i < definitions.Count; ++i)
		{
			var definition = definitions[i];
			var mesh = NifModelLoader.CreateMesh(_graphicsDevice, _fileSystem, definition);
			children.Add(new DrModelBone($"{boneName}_{i}", mesh));
		}

		group.Children = children.ToArray();
		++LoadedModelCount;
		return group;
	}

	private static IEnumerable<IPlacedGetter> EnumeratePlaced(ICellGetter cell)
	{
		var persistent = cell.Persistent;
		if (persistent != null)
		{
			foreach (var placed in persistent)
			{
				yield return placed;
			}
		}

		var temporary = cell.Temporary;
		if (temporary != null)
		{
			foreach (var placed in temporary)
			{
				yield return placed;
			}
		}
	}

	private static Matrix GetTransform(IPlacedObjectGetter placed)
	{
		var placement = placed.Placement;
		var position = placement?.Position ?? default;
		var rotation = placement?.Rotation ?? default;
		var scale = placed.Scale ?? 1f;

		return Matrix.CreateScale(scale)
			* Matrix.CreateRotationZ(MathHelper.ToRadians(rotation.Z))
			* Matrix.CreateRotationY(MathHelper.ToRadians(rotation.Y))
			* Matrix.CreateRotationX(MathHelper.ToRadians(rotation.X))
			* Matrix.CreateTranslation(position.X, position.Y, position.Z);
	}
}
