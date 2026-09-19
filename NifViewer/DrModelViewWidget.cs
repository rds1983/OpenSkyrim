using System;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Nursia.Rendering;
using Nursia.SceneGraph;
using Nursia.SceneGraph.Lights;

namespace OpenSkyrim.NifViewer
{
	public class DrModelViewWidget : Widget
	{
		private readonly ForwardRenderer _renderer = new ForwardRenderer();
		private readonly Scene _scene = new Scene();
		private readonly NursiaModelNode _modelNode = new NursiaModelNode();
		private readonly Camera _camera = new Camera();

		public DrModelViewWidget()
		{
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
				ResetCamera();
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
