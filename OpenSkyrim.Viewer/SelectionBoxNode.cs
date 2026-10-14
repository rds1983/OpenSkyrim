using System;
using DigitalRiseModel;
using DigitalRiseModel.Primitives;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nursia.Materials;
using Nursia.Rendering;
using Nursia.SceneGraph;

namespace OpenSkyrim.Viewer;

/// <summary>
/// A scene node that draws a wireframe box around a target node's full bounding box.
/// This follows the approach of the Nursia editor's selection box (see
/// <c>Nursia.Editor.Utility._3DUtils</c>): a shared unit box line mesh is scaled and
/// translated to the bounding box and then placed by the target's global transform.
/// </summary>
public sealed class SelectionBoxNode : SceneNode
{
	private static readonly BoundingBox UnitBox = new BoundingBox(Vector3.Zero, Vector3.One);
	private static readonly BoundingBox DefaultBox = new BoundingBox(new Vector3(-0.5f), new Vector3(0.5f));

	private static DrMeshPart _mesh;

	/// <summary>
	/// Gets the flags for this node.
	/// </summary>
	public override SceneFlags Flags => SceneFlags.HasRenderJobs;

	public DrMeshPart Mesh { get; }

	public IMaterial Material { get; }

	public Matrix Transform { get; set; }

	public SelectionBoxNode(GraphicsDevice graphicsDevice)
	{
		if (graphicsDevice == null)
		{
			throw new ArgumentNullException(nameof(graphicsDevice));
		}

		Mesh = GetMesh(graphicsDevice);
		Material = new UnlitMaterial
		{
			DiffuseColor = Color.Orange,
			CastsShadows = false
		};
	}

	/// <summary>
	/// Positions the box around the full bounding box of the given node.
	/// </summary>
	public void SetTarget(SceneNode node)
	{
		if (node == null)
		{
			return;
		}

		var box = node.FullBoundingBox ?? DefaultBox;

		var scale = box.Max - box.Min;
		Transform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(box.Min) * node.GlobalTransform;
	}

	public override void AddRenderJobs(Camera camera, IRenderJobsBatch batch)
	{
		base.AddRenderJobs(camera, batch);

		batch.AddMesh(Mesh, Material, Transform);
	}

	private static DrMeshPart GetMesh(GraphicsDevice graphicsDevice)
	{
		if (_mesh == null)
		{
			_mesh = MeshPrimitives.CreateBoxLinesMeshPart(graphicsDevice, UnitBox);
		}

		return _mesh;
	}
}
