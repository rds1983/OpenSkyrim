using System;
using System.Collections.Generic;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;
using Nursia.SceneGraph;
using OpenSkyrim.NifViewer.Utility;

namespace OpenSkyrim.NifViewer;

public sealed class SkyrimSceneBuilder
{
	private const int MaxPlacedObjects = 4000;

	// Skyrim spawns the player at the cell's XMarker reference. The marker also has a
	// model (MarkerX.nif), so the placed object pass renders it as a mesh already.
	private const string PlayerStartMarkerEditorId = "XMarker";

	// NIF data is Z-up; Nursia is Y-up, hence the -90 pitch about X. Placements are relative
	// to the cell, so the player start has to be rotated by the same amount the location
	// root node applies to its children.
	private static readonly Vector3 LocationRootRotation = new Vector3(-90, 0, 0);

	private readonly SkyrimFileSystem _fileSystem;

	public int PlacedObjectCount { get; private set; }
	public int LoadedModelCount { get; private set; }

	/// <summary>
	/// Gets the world transform of where the player appears in the last built location, or
	/// <c>null</c> when the location has no player start (worldspace cells never do).
	/// </summary>
	public SrtTransform? PlayerStart { get; private set; }

	public SkyrimSceneBuilder(SkyrimFileSystem fileSystem)
	{
		_fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
	}

	public SceneNode Build(ILinkCache linkCache, ICellGetter cell)
	{
		PlacedObjectCount = 0;
		LoadedModelCount = 0;
		PlayerStart = null;

		var name = string.IsNullOrWhiteSpace(cell.EditorID) ? "Location" : cell.EditorID;

		var rootNode = new SceneNode()
		{
			Id = name,
			Rotation = LocationRootRotation
		};

		PlayerStart = CreatePlayerStart(linkCache, cell);

		foreach (var placed in EnumeratePlaced(cell))
		{
			if (PlacedObjectCount >= MaxPlacedObjects)
			{
				break;
			}

			var child = CreateChild(linkCache, placed);
			if (child != null)
			{
				rootNode.Children.Add(child);
				++PlacedObjectCount;
			}
		}

		return rootNode;
	}

	private static SrtTransform? CreatePlayerStart(ILinkCache linkCache, ICellGetter cell)
	{
		foreach (var placed in EnumeratePlaced(cell))
		{
			if (placed is not IPlacedObjectGetter placedObject)
			{
				continue;
			}

			var baseLink = placedObject.Base;
			if (baseLink == null || baseLink.IsNull)
			{
				continue;
			}

			if (!baseLink.TryResolve(linkCache, out var baseRecord) || baseRecord == null)
			{
				continue;
			}

			if (!string.Equals(baseRecord.EditorID, PlayerStartMarkerEditorId, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var placement = placedObject.Placement;
			if (placement == null)
			{
				continue;
			}

			var rootRotation = ToQuaternion(LocationRootRotation);
			var position = Vector3.Transform(placement.Position.ToVector3(), rootRotation);

			var rotation = rootRotation * ToQuaternion(GetRotation(placement));

			return new SrtTransform(position, rotation, Vector3.One);
		}

		return null;
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
			model = _fileSystem.LoadModel(modelPath);
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

	private static void SetTransform(SceneNode node, IPlacedGetter placed)
	{
		var placement = placed.Placement;
		if (placed == null || placement == null)
		{
			return;
		}

		node.Translation = placement.Position.ToVector3();
		node.Rotation = GetRotation(placement);

		if (placed.Scale != null)
		{
			node.Scale = new Vector3(placed.Scale.Value);
		}
		else
		{
			node.Scale = Vector3.One;
		}
	}

	private static Quaternion ToQuaternion(Vector3 degrees)
	{
		var radians = degrees.ToRadians();

		// Same order SceneNode uses, see SceneNode.LocalTransform.
		return Quaternion.CreateFromYawPitchRoll(radians.Y, radians.X, radians.Z);
	}

	private static Vector3 GetRotation(IPlacementGetter placement)
	{
		var rot = placement.Rotation.ToVector3().ToDegrees();

		// For some reason, if I ignore models' internal rotations(I set to Identity in the model loader)
		// And rotate in negative direction over Z axis in locations
		// Then it is placed correctly
		return new Vector3(rot.X, rot.Y, -rot.Z);
	}
}
