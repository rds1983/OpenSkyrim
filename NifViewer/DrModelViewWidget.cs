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

	private static readonly Vector3 WorldUp = new Vector3(0, 0, 1);

	// Yaw of zero looks along +X, so start far enough back on -X for the origin to be ahead of
	// the camera and the view to stay level rather than looking straight down the up axis.
	private static readonly Vector3 DefaultEye = -Vector3.Right * 5f;

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
			SprintMultiplier = 2.5f,
			Up = WorldUp
		};

		var root = new SceneNode();
		root.Children.Add(new DirectLight { Rotation = new Vector3(45, 45, 0), CastsShadow = false });
		root.Children.Add(new DirectLight { Rotation = new Vector3(225, 45, 0), CastsShadow = false });

		_scene.Root = root;
		_scene.Camera = _camera;
		_cameraController.Eye = DefaultEye;
		_camera.NearPlane = 0.1f;
		_camera.FarPlane = 10000f;

		_sceneAxises = new Scene
		{
			Root = Resources.ModelAxises
		};
	}

	/// <summary>
	/// Gets the controller that owns the camera position and orientation.
	/// </summary>
	public CameraInputController CameraController => _cameraController;

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

		// Make the gizmo placed always in front of the camera. Moving the camera clears its
		// view matrix, so it has to be rebuilt from the controller's orientation.
		axisesCamera.Translation = Vector3.Zero;
		axisesCamera.View = CameraInputController.CreateViewMatrix(
			Vector3.Zero, _cameraController.Yaw, _cameraController.Pitch, WorldUp);

		axisesRoot.Translation = _cameraController.Forward * 2.5f;

		var axisesTarget = _sceneAxises.RenderToTarget(_renderer, axisesCamera, AxisesSize, AxisesSize);

		context.Begin();
		context.Draw(axisesTarget, new Rectangle(ActualBounds.Width - AxisesSize, 0, AxisesSize, AxisesSize), null, Color.White);
	}

	private void ResetCamera()
	{
		if (_sceneNode == null)
		{
			_cameraController.Eye = DefaultEye;
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

		// The world is Z-up, so step back horizontally rather than along an axis.
		var eye = center - Vector3.Right * distance;
		_cameraController.Eye = eye;
		_cameraController.LookAlong(center - eye);
	}

	private bool ResetCameraToPlayerStart()
	{
		var playerStart = _playerStart;
		if (playerStart == null)
		{
			return false;
		}

		var transform = playerStart.Value;

		// Placements are Z-up and so is the world, so the direction the player faces is
		// the marker's local +Y axis.
		var forward = Vector3.Transform(Vector3.UnitY, transform.Rotation);
		if (forward.LengthSquared() <= 0f)
		{
			return false;
		}

		_cameraController.Eye = transform.Translation + WorldUp * PlayerEyeHeight;
		_cameraController.LookAlong(forward);

		return true;
	}
}
