using System;
using System.Collections.Generic;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Nursia.Rendering;
using Nursia.SceneGraph;
using Nursia.SceneGraph.Lights;
using Nursia.Utilities;
using OpenSkyrim.Viewer.Data;
using OpenSkyrim.Viewer.Utility;

namespace OpenSkyrim.Viewer.UI;

public class SceneViewWidget : Widget
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
	private SceneNode _selectedNode;
	private SelectionBoxNode _selectionNode;
	private SrtTransform? _playerStart;
	private readonly Camera _camera = new Camera();
	private readonly CameraInputController _cameraController;

	public SceneViewWidget()
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

	/// <summary>
	/// Occurs when the viewer is left-clicked, with the node under the pointer, or <c>null</c>
	/// when the click did not hit anything.
	/// </summary>
	public event Action<SceneNode> ObjectPicked;

	/// <summary>
	/// Occurs when <see cref="SelectedNode"/> changes, with the new selected node (or
	/// <c>null</c> when the selection is cleared).
	/// </summary>
	public event Action<SceneNode> SelectionChanged;

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
			SelectedNode = null;

			if (_sceneNode != null)
			{
				_scene.Root.Children.Add(_sceneNode);
			}

			ResetCamera();
		}
	}

	/// <summary>
	/// Gets or sets the node whose bounding box is highlighted with a wireframe box, or
	/// <c>null</c> to clear the highlight.
	/// </summary>
	public SceneNode SelectedNode
	{
		get => _selectedNode;
		set
		{
			if (_selectedNode == value)
			{
				return;
			}

			_selectedNode = value;
			SelectionChanged?.Invoke(value);
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

	public override void OnTouchDown()
	{
		base.OnTouchDown();

		// Myra reports any mouse button as a touch, but the right button drives camera rotation,
		// so only the left button selects.
		if (Mouse.GetState().LeftButton != ButtonState.Pressed)
		{
			return;
		}

		ObjectPicked?.Invoke(Pick(Desktop.MousePosition));
	}

	/// <summary>
	/// Returns the scene node under the given screen position, or <c>null</c> when nothing is
	/// hit. Every triangle of the picked models is tested directly; the object owning the
	/// closest hit triangle is selected.
	/// </summary>
	/// <param name="screenPosition">The position in global (screen) coordinates.</param>
	public SceneNode Pick(Point screenPosition)
	{
		var ray = CreateRay(screenPosition);
		if (ray == null)
		{
			return null;
		}

		SceneNode result = null;
		var resultDistance = float.MaxValue;

		foreach (var node in EnumerateNodes(_sceneNode))
		{
			// Only placed objects carry pickable triangle geometry (tagged when the scene was
			// built).
			if (node.Tag is not LocationObject placed)
			{
				continue;
			}

			// Broad phase: skip the object when the ray misses its bounding box. This
			// also avoids building the object's triangles when it isn't a candidate.
			if (node is NursiaModelNode modelNode && modelNode.BoundingBox.HasValue)
			{
				var worldBox = TransformBox(modelNode.BoundingBox.Value, node.GlobalTransform);
				var nodeDistance = ray.Value.Intersects(worldBox);
				if (!nodeDistance.HasValue || nodeDistance.Value < 0.0f)
				{
					continue;
				}
			}

			var triangles = placed.Triangles;
			if (triangles == null)
			{
				continue;
			}

			// The triangles are in the model node's local space; the node's world transform
			// places them in the world.
			var worldTransform = node.GlobalTransform;
			foreach (var triangle in triangles)
			{
				var worldTriangle = triangle.Transform(worldTransform);
				if (worldTriangle.Intersects(ray.Value, out var distance) && distance < resultDistance)
				{
					resultDistance = distance;
					result = node;
				}
			}
		}

		return result;
	}

	private Ray? CreateRay(Point screenPosition)
	{
		if (_sceneNode == null || ActualBounds.Width <= 0 || ActualBounds.Height <= 0)
		{
			return null;
		}

		// Build the viewport using the widget's global position, then unproject the screen
		// point to world space at the near and far planes to obtain the pick ray.
		var bounds = ActualBounds;
		var origin = ToGlobal(bounds.Location);
		bounds.X = origin.X;
		bounds.Y = origin.Y;

		var viewport = new Viewport(bounds.X, bounds.Y, bounds.Width, bounds.Height);
		var near = viewport.Unproject(new Vector3(screenPosition.X, screenPosition.Y, 0.0f),
			_camera.Projection, _camera.View, Matrix.Identity);
		var far = viewport.Unproject(new Vector3(screenPosition.X, screenPosition.Y, 1.0f),
			_camera.Projection, _camera.View, Matrix.Identity);

		var direction = far - near;
		if (direction.LengthSquared() <= 0.0f)
		{
			return null;
		}

		direction.Normalize();
		return new Ray(near, direction);
	}

	private static IEnumerable<SceneNode> EnumerateNodes(SceneNode node)
	{
		yield return node;

		foreach (var child in node.Children)
		{
			foreach (var descendant in EnumerateNodes(child))
			{
				yield return descendant;
			}
		}
	}

	private static BoundingBox TransformBox(BoundingBox box, Matrix transform)
	{
		var corners = box.GetCorners();
		for (var i = 0; i < corners.Length; ++i)
		{
			corners[i] = Vector3.Transform(corners[i], transform);
		}

		return BoundingBox.CreateFromPoints(corners);
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

		UpdateSelectionBox();

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

	/// <summary>
	/// Keeps the selection box in sync with the selected node, adding or removing it from the
	/// scene as needed so it is drawn around the node's current bounding box.
	/// </summary>
	private void UpdateSelectionBox()
	{
		if (_selectedNode == null)
		{
			if (_selectionNode != null)
			{
				_scene.Root.Children.Remove(_selectionNode);
				_selectionNode = null;
			}

			return;
		}

		if (_selectionNode == null)
		{
			_selectionNode = new SelectionBoxNode(MyraEnvironment.GraphicsDevice);
		}

		_selectionNode.SetTarget(_selectedNode);

		if (!_scene.Root.Children.Contains(_selectionNode))
		{
			_scene.Root.Children.Add(_selectionNode);
		}
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
