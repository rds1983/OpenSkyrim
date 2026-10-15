using Microsoft.Xna.Framework;
using Nursia.SceneGraph;
using OpenSkyrim.Viewer.Utility;
using System.Collections.Generic;

namespace OpenSkyrim.Viewer.Data;

/// <summary>
/// A single placed object of a location, as shown in the location's object list.
/// </summary>
public sealed class LocationObject
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
