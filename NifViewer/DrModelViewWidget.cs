using System;
using System.Collections.Generic;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Nursia;
using Nursia.Materials;
using Nursia.Rendering;
using Nursia.SceneGraph;
using Nursia.SceneGraph.Lights;
using Nursia.Utilities;

namespace OpenSkyrim.NifViewer;

public class DrModelViewWidget : Widget
{
	private const int GridSize = 200;
	private const int GridCellSize = 2;
	private const int AxisesSize = 160;

	private readonly ForwardRenderer _renderer = new ForwardRenderer();
	private readonly Scene _scene = new Scene();
	private readonly Scene _sceneAxises;
	private readonly NursiaModelNode _modelNode = new NursiaModelNode();
	private readonly Camera _camera = new Camera();
	private readonly CameraInputController _cameraController;
	private MeshNode _gridMesh;

	private MeshNode GridMesh
	{
		get
		{
			if (_gridMesh == null)
			{
				var vertices = new List<Vector3>();
				var indices = new List<ushort>();

				ushort idx = 0;
				for (var x = -GridSize; x <= GridSize; x += GridCellSize)
				{
					vertices.Add(new Vector3(x, 0, -GridSize));
					vertices.Add(new Vector3(x, 0, GridSize));

					indices.Add(idx);
					++idx;
					indices.Add(idx);
					++idx;
				}

				for (var z = -GridSize; z <= GridSize; z += GridCellSize)
				{
					vertices.Add(new Vector3(-GridSize, 0, z));
					vertices.Add(new Vector3(GridSize, 0, z));

					indices.Add(idx);
					++idx;
					indices.Add(idx);
					++idx;
				}

				var mesh = new DrMeshPart(Nrs.GraphicsDevice, vertices.ToArray(), indices.ToArray(), PrimitiveType.LineList);

				_gridMesh = new MeshNode
				{
					Mesh = mesh,
					Material = new UnlitMaterial
					{
						DiffuseColor = Color.Green,
						CastsShadows = false
					},
				};
			}

			return _gridMesh;
		}
	}

	public DrModelViewWidget()
	{
		_cameraController = new CameraInputController(_camera)
		{
			MoveSpeed = 2.5f,
			RotationSpeed = 0.15f,
			SprintMultiplier = 2.5f
		};

		var root = new SceneNode();
		root.Children.Add(new DirectLight { Rotation = new Vector3(45, 45, 0), CastsShadow = false });
		root.Children.Add(new DirectLight { Rotation = new Vector3(225, 45, 0), CastsShadow = false });
		root.Children.Add(GridMesh);
		root.Children.Add(_modelNode);

		_scene.Root = root;
		_scene.Camera = _camera;
		_camera.View = Matrix.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.Up);
		_camera.NearPlane = 0.1f;
		_camera.FarPlane = 1000f;

		_sceneAxises = new Scene
		{
			Root = Resources.ModelAxises
		};
	}

	public DrModel Model
	{
		get => _modelNode.Model;
		set
		{
			_modelNode.Model = value;
			_modelNode.Materials = BuildMaterials(value);
			ResetCamera();
		}
	}

	private IMaterial[][] BuildMaterials(DrModel model)
	{
		if (model == null)
		{
			return null;
		}

		var materials = new IMaterial[model.Meshes.Length][];
		for (var meshIndex = 0; meshIndex < model.Meshes.Length; ++meshIndex)
		{
			var mesh = model.Meshes[meshIndex];
			materials[meshIndex] = new IMaterial[mesh.MeshParts.Count];

			for (var partIndex = 0; partIndex < mesh.MeshParts.Count; ++partIndex)
			{
				materials[meshIndex][partIndex] = ToNursiaMaterial(mesh.MeshParts[partIndex].Material);
			}
		}

		return materials;
	}

	private static IMaterial ToNursiaMaterial(DrMaterial material)
	{
		if (material == null)
		{
			return new UnlitMaterial
			{
				DiffuseColor = Color.White
			};
		}

		return new BlinnPhongMaterial
		{
			DiffuseColor = material.DiffuseColor,
			SpecularColor = material.SpecularColor,
			SpecularPower = material.Shininess,
			EmissiveColor = material.EmissiveColor,
			DiffuseTexture = material.DiffuseTexture,
			SpecularTexture = material.SpecularTexture,
			NormalTexture = material.NormalTexture
		};
	}

	public void UpdateCameraInput(float elapsedSeconds)
	{
		if (_modelNode.Model != null)
		{
			_cameraController.Update(elapsedSeconds);
		}
	}

	public override void InternalRender(RenderContext context)
	{
		if (ActualBounds.Width <= 0 || ActualBounds.Height <= 0)
		{
			return;
		}

		context.End();

		var device = MyraEnvironment.GraphicsDevice;
		var previousViewport = device.Viewport;

		var bounds = context.ToGlobal(ActualBounds);
		device.Viewport = new Viewport(bounds.X, bounds.Y, bounds.Width, bounds.Height);
		if (_modelNode.Model != null)
		{
			_scene.Render(_renderer, _camera);
		}
		device.Viewport = previousViewport;

		// Draw axises gizmo in the top-right corner
		var axisesRoot = _sceneAxises.Root;
		var axisesCamera = (Camera)_camera.Clone();

		// Make the gizmo placed always in front of the camera
		axisesCamera.Translation = Vector3.Zero;
		var direction = axisesCamera.GlobalTransform.Forward;
		direction.Normalize();
		axisesRoot.Translation = direction * 2.5f;

		var axisesTarget = _sceneAxises.RenderToTarget(_renderer, axisesCamera, AxisesSize, AxisesSize);

		context.Begin();
		context.Draw(axisesTarget, new Rectangle(ActualBounds.Width - AxisesSize, 0, AxisesSize, AxisesSize), null, Color.White);
	}

	private void ResetCamera()
	{
		if (_modelNode.Model == null)
		{
			_camera.View = Matrix.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.Up);
			_cameraController.FocusPoint = null;
			return;
		}

		var boundingBox = _modelNode.BoundingBox;
		if (!boundingBox.HasValue)
		{
			return;
		}

		var min = boundingBox.Value.Min;
		var max = boundingBox.Value.Max;
		var center = (min + max) / 2f;
		var size = Math.Max(max.X - min.X, Math.Max(max.Y - min.Y, max.Z - min.Z));
		var distance = Math.Max(size * 1.75f, 5f);

		_camera.View = Matrix.CreateLookAt(new Vector3(center.X, center.Y, center.Z + distance), center, Vector3.Up);
		_cameraController.FocusPoint = center;
		_camera.NearPlane = Math.Max(0.01f, size / 1000f);
		_camera.FarPlane = Math.Max(1000f, size * 20f);
	}
}
