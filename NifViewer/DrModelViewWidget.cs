using System;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Nursia.Materials;
using Nursia.Rendering;
using Nursia.SceneGraph;
using Nursia.SceneGraph.Lights;
using Nursia.Utilities;

namespace OpenSkyrim.NifViewer
{
	public class DrModelViewWidget : Widget
	{
		private readonly ForwardRenderer _renderer = new ForwardRenderer();
		private readonly Scene _scene = new Scene();
		private readonly NursiaModelNode _modelNode = new NursiaModelNode();
		private readonly Camera _camera = new Camera();
		private readonly CameraInputController _cameraController;

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
			root.Children.Add(_modelNode);

			_scene.Root = root;
			_scene.Camera = _camera;
			_camera.View = Matrix.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.Up);
			_camera.NearPlane = 0.1f;
			_camera.FarPlane = 1000f;
		}

		public DrModel Model
		{
			get => _modelNode.Model;
			set
			{
				_modelNode.Model = value;
				_modelNode.Materials = BuildPurpleMaterials(value);
				ResetCamera();
			}
		}

		private static IMaterial[][] BuildPurpleMaterials(DrModel model)
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
					materials[meshIndex][partIndex] = new UnlitMaterial
					{
						DiffuseColor = new Color(0.75f, 0.25f, 1f)
					};
				}
			}

			return materials;
		}

		public void UpdateCameraInput()
		{
			if (_modelNode.Model != null)
			{
				_cameraController.Update();
			}
		}

		public override void InternalRender(RenderContext context)
		{
			if (_modelNode.Model == null || ActualBounds.Width <= 0 || ActualBounds.Height <= 0)
			{
				return;
			}

			context.End();

			var device = MyraEnvironment.GraphicsDevice;
			var previousViewport = device.Viewport;

			var bounds = context.ToGlobal(ActualBounds);
			device.Viewport = new Viewport(bounds.X, bounds.Y, bounds.Width, bounds.Height);
			_scene.Render(_renderer, _camera);
			device.Viewport = previousViewport;

			context.Begin();
		}

		private void ResetCamera()
		{
			if (_modelNode.Model == null)
			{
				_camera.View = Matrix.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.Up);
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
			_camera.NearPlane = Math.Max(0.01f, size / 1000f);
			_camera.FarPlane = Math.Max(1000f, size * 20f);
		}
	}
}
