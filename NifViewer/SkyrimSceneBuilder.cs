using System;
using System.Collections.Generic;
using DigitalRiseModel;
using DynamicData.Kernel;
using Microsoft.Xna.Framework;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;
using Nursia.SceneGraph;
using OpenSkyrim.NifViewer.Utility;

namespace OpenSkyrim.NifViewer;

/// <summary>
/// A single placed object of a location, as shown in the location's object list.
/// </summary>
public sealed class SkyrimPlacedObject
{
	/// <summary>
	/// Gets the display name of the object (base record editor id, model path or form key).
	/// </summary>
	public string Name { get; init; }

	/// <summary>
	/// Gets or sets the scene node this object was placed as, or <c>null</c> when it was not
	/// added to the scene (no model, missing file or beyond the scene's object cap).
	/// </summary>
	public SceneNode Node { get; set; }

	private Triangle[] _triangles;
	private bool _trianglesBuilt;

	/// <summary>
	/// Gets every triangle of this object's model (in the model node's local space, bone
	/// transforms applied), built lazily from the model's source geometry. Returns <c>null</c>
	/// when the object has no pickable geometry.
	/// </summary>
	public Triangle[] Triangles
	{
		get
		{
			if (!_trianglesBuilt)
			{
				_trianglesBuilt = true;
				_triangles = BuildTriangles();
			}

			return _triangles;
		}
	}

	private Triangle[] BuildTriangles()
	{
		if (Node is not NursiaModelNode modelNode || modelNode.Model == null)
		{
			return null;
		}

		var modelInstance = modelNode.ModelInstance;
		var result = new List<Triangle>();

		foreach (var mesh in modelNode.Model.Meshes)
		{
			var bone = mesh.ParentBone;
			if (bone == null)
			{
				continue;
			}

			var boneTransform = modelInstance.GetBoneGlobalTransform(bone.Index);

			foreach (var part in mesh.MeshParts)
			{
				var positions = part.SourcePositions;
				var indices = part.SourceIndices;
				if (positions == null || indices == null || indices.Length < 3)
				{
					continue;
				}

				var count = indices.Length / 3;
				for (var i = 0; i < count; ++i)
				{
					result.Add(new Triangle(
						Vector3.Transform(positions[indices[i * 3]], boneTransform),
						Vector3.Transform(positions[indices[i * 3 + 1]], boneTransform),
						Vector3.Transform(positions[indices[i * 3 + 2]], boneTransform)));
				}
			}
		}

		return result.Count > 0 ? result.ToArray() : null;
	}
}

public sealed class SkyrimSceneBuilder
{
	private const int MaxPlacedObjects = 4000;

	private readonly SkyrimFileSystem _fileSystem;

	public int PlacedObjectCount { get; private set; }
	public int LoadedModelCount { get; private set; }

	/// <summary>
	/// Gets the display entries for every placed object of the last built location.
	/// </summary>
	public List<SkyrimPlacedObject> PlacedObjects { get; private set; } = new List<SkyrimPlacedObject>();

	/// <summary>
	/// Gets the world transform of where the player appears in the last built location, or
	/// <c>null</c> when the location has no player start (worldspace cells never do).
	/// </summary>
	public SrtTransform? PlayerStart { get; private set; }

	/// <summary>
	/// Gets a short description of which hint <see cref="PlayerStart"/> was taken from,
	/// for diagnostics.
	/// </summary>
	public string PlayerStartSource { get; private set; }

	public SkyrimSceneBuilder(SkyrimFileSystem fileSystem)
	{
		_fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
	}

	public SceneNode Build(ILinkCache linkCache, ICellGetter cell)
	{
		PlacedObjectCount = 0;
		LoadedModelCount = 0;
		PlacedObjects = new List<SkyrimPlacedObject>();
		PlayerStart = null;
		PlayerStartSource = null;

		var name = string.IsNullOrWhiteSpace(cell.EditorID) ? "Location" : cell.EditorID;

		var rootNode = new SceneNode()
		{
			Id = name
		};

		// The full set of placements is needed twice: the start is resolved from all of them,
		// while the scene only takes the first MaxPlacedObjects.
		var placedObjects = EnumeratePlaced(cell).AsList();

		PlayerStart = PlayerStartResolver.Resolve(linkCache, placedObjects, out var startSource);
		PlayerStartSource = startSource;

		PlacedObjects = BuildPlacedObjectList(linkCache, placedObjects);

		for (var i = 0; i < placedObjects.Count; ++i)
		{
			if (PlacedObjectCount >= MaxPlacedObjects)
			{
				break;
			}

			var child = CreateChild(linkCache, placedObjects[i]);
			if (child != null)
			{
				rootNode.Children.Add(child);

				// PlacedObjects is built from the same list in the same order, so the node can
				// be linked back to its list entry for selection in the UI. The node is also
				// tagged with its entry so the viewer can pick it and use its triangle boxes.
				PlacedObjects[i].Node = child;
				child.Tag = PlacedObjects[i];

				++PlacedObjectCount;
			}
		}

		return rootNode;
	}

	private static List<SkyrimPlacedObject> BuildPlacedObjectList(ILinkCache linkCache, IReadOnlyList<IPlacedGetter> placedObjects)
	{
		var result = new List<SkyrimPlacedObject>(placedObjects.Count);
		foreach (var placed in placedObjects)
		{
			result.Add(new SkyrimPlacedObject
			{
				Name = GetPlacedName(linkCache, placed)
			});
		}

		return result;
	}

	private static string GetPlacedName(ILinkCache linkCache, IPlacedGetter placed)
	{
		string baseEditorId = null;
		string modelPath = null;

		if (placed is IPlacedObjectGetter placedObject)
		{
			var baseLink = placedObject.Base;
			if (baseLink != null && !baseLink.IsNull && baseLink.TryResolve(linkCache, out var baseRecord) && baseRecord != null)
			{
				baseEditorId = baseRecord.EditorID;
				if (baseRecord is IModeledGetter modeled)
				{
					modelPath = modeled.Model?.File?.GivenPath;
				}
			}
		}

		if (!string.IsNullOrWhiteSpace(baseEditorId))
		{
			return baseEditorId;
		}

		if (!string.IsNullOrWhiteSpace(modelPath))
		{
			return modelPath;
		}

		if (!string.IsNullOrWhiteSpace(placed.EditorID))
		{
			return placed.EditorID;
		}

		return placed.FormKey.ToString();
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


	private static Vector3 GetRotation(IPlacementGetter placement)
	{
		var rot = placement.Rotation.ToVector3().ToDegrees();

		// For some reason, if I ignore models' internal rotations(I set to Identity in the model loader)
		// And rotate in negative direction over Z axis in locations
		// Then it is placed correctly
		return new Vector3(rot.X, rot.Y, -rot.Z);
	}
}

