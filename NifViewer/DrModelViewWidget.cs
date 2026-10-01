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
using Nursia.Utilities;

namespace OpenSkyrim.NifViewer;

public class DrModelViewWidget : Widget
{
	private const int AxisesSize = 160;

	// Skyrim uses 1 unit = 1.428 cm, so an actor of height 1.00 is 128 units (1.83 m)
	// tall and its eyes sit at roughly 118 units.
	private const float PlayerEyeHeight = 118f;

	private readonly ForwardRenderer _renderer = new ForwardRenderer();
	private readonly Scene _scene = new Scene();
	private readonly Scene _sceneAxises;
	private SceneNode _sceneNode;
	private SrtTransform? _playerStart;
	private readonly Camera _camera = new Camera();
	private readonly CameraInputController _cameraController;

	public DrModelViewWidget()
	{
		_cameraController = new CameraInputController(_camera)
		{
			MoveSpeed = 200.0f,
			RotationSpeed = 0.15f,
			SprintMultiplier = 2.5f
		};

		var root = new SceneNode();
		root.Children.Add(new DirectLight { Rotation = new Vector3(45, 45, 0), CastsShadow = false });
		root.Children.Add(new DirectLight { Rotation = new Vector3(225, 45, 0), CastsShadow = false });

		_scene.Root = root;
		_scene.Camera = _camera;
		_camera.View = Matrix.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.Up);
		_camera.NearPlane = 0.1f;
		_camera.FarPlane = 10000f;

		_sceneAxises = new Scene
		{
			Root = Resources.ModelAxises
		};
	}

	public SceneNode Node
	{
		get => _sceneNode;
		set
		{
			if (_sceneNode != null)
			{
				_scene.Root.Children.Remove(_sceneNode);
			}

			_sceneNode = value;

			if (_sceneNode != null)
			{
				_scene.Root.Children.Add(_sceneNode);
			}

			ResetCamera();
		}
	}

	/// <summary>
	/// Gets or sets the world transform of where the player appears in the loaded location.
	/// When set, the camera is placed there at eye height instead of framing the scene.
	/// </summary>
	public SrtTransform? PlayerStart
	{
		get => _playerStart;
		set
		{
			_playerStart = value;
			ResetCamera();
		}
	}

	public void UpdateCameraInput(float elapsedSeconds)
	{
		if (_sceneNode != null)
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
		if (_sceneNode != null)
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
		if (_sceneNode == null)
		{
			_camera.View = Matrix.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.Up);
			return;
		}

		if (ResetCameraToPlayerStart())
		{
			return;
		}

		var boundingBox = _sceneNode.FullBoundingBox;
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
	}

	private bool ResetCameraToPlayerStart()
	{
		var playerStart = _playerStart;
		if (playerStart == null)
		{
			return false;
		}

		var transform = playerStart.Value;

		// Placements are Z-up, so the direction the player faces is the local +Y axis.
		var forward = Vector3.Transform(Vector3.UnitY, transform.Rotation);
		if (forward.LengthSquared() <= 0f)
		{
			return false;
		}

		forward.Normalize();

		// SceneNode.Rotation is pitch (X) and yaw (Y); in FNA a yaw of +Y turns the
		// camera towards -X, and +X pitches it up.
		_camera.Translation = transform.Translation + Vector3.Up * PlayerEyeHeight;
		_camera.Rotation = new Vector3(
			MathHelper.ToDegrees(MathF.Asin(MathHelper.Clamp(forward.Y, -1f, 1f))),
			MathHelper.ToDegrees(MathF.Atan2(-forward.X, -forward.Z)),
			0f);

		return true;
	}
}
