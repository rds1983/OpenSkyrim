using System;
using System.Collections.Generic;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;
using Nursia;
using Nursia.SceneGraph;
using OpenSkyrim.NifViewer.Utility;

namespace OpenSkyrim.NifViewer;

public sealed class SkyrimSceneBuilder
{
	private const int MaxPlacedObjects = 4000;

	private static readonly Matrix RootRotation = Matrix.CreateRotationX(-MathHelper.PiOver2);

	private readonly SkyrimFileSystem _fileSystem;

	public int PlacedObjectCount { get; private set; }
	public int LoadedModelCount { get; private set; }

	public SkyrimSceneBuilder(SkyrimFileSystem fileSystem)
	{
		_fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
	}

	public SceneNode Build(ILinkCache linkCache, ICellGetter cell)
	{
		PlacedObjectCount = 0;
		LoadedModelCount = 0;

		var name = string.IsNullOrWhiteSpace(cell.EditorID) ? "Location" : cell.EditorID;

		var rootNode = new SceneNode()
		{
			Id = name,
			Rotation = new Vector3(-90, 0, 0)
		};

		var children = new List<DrModelBone>();
		foreach (var placed in EnumeratePlaced(cell))
		{
			if (PlacedObjectCount >= MaxPlacedObjects)
			{
				break;
			}

			var bone = CreateChild(linkCache, placed);
			if (bone != null)
			{
				rootNode.Children.Add(bone);
				++PlacedObjectCount;
			}
		}

		return rootNode;
	}

	private SceneNode CreateChild(ILinkCache linkCache, IPlacedGetter placed)
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

		if (!baseLink.TryResolve(linkCache, out var baseRecord) || baseRecord == null)
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

		DrModel model;
		try
		{
			model = _fileSystem.LoadModel(Nrs.GraphicsDevice, modelPath);
		}
		catch (Exception ex)
		{
			OSK.LogWarning($"Failed to load model '{modelPath}': {ex.Message}");
			return null;
		}

		if (model == null || model.Meshes.Length == 0)
		{
			return null;
		}


		var boneName = $"{baseRecord.EditorID}";

		var result = new NursiaModelNode
		{
			Id = boneName,
			Model = model
		};

		SetTransform(result, placedObject);

		return result;
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

	private static void SetTransform(SceneNode node, IPlacedObjectGetter placed)
	{
		var placement = placed.Placement;
		if (placed == null || placement == null)
		{
			return;
		}

		node.Translation = placement.Position.ToVector3();
		node.Rotation = placement.Rotation.ToVector3().ToDegrees();
		if (placed.Scale != null)
		{
			node.Scale = new Vector3(placed.Scale.Value);
		}
		else
		{
			node.Scale = Vector3.One;
		}
	}
}
