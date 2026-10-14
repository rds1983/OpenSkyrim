using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Data;
using Nursia.SceneGraph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSkyrim.NifViewer.UI;

partial class MainForm
{
	private const int LocationsSource = 0;
	private const int ModelsSource = 1;

	private readonly SkyrimFileSystem _fileSystem;
	private readonly SkyrimSceneBuilder _sceneBuilder;
	private readonly object _skyrimWorldLock = new object();

	private SkyrimWorld _skyrimWorld;
	private int _populateVersion;
	private volatile List<Entry> _pendingData;

	private readonly DataGrid _grid;
	private readonly DataGrid _objectsGrid;
	private readonly VerticalSplitPane _leftSplit;
	private readonly DrModelViewWidget _viewer;
	private bool _isLeftSplit;

	public MainForm(SkyrimFileSystem fileSystem)
	{
		BuildUI();

		_fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
		_sceneBuilder = new SkyrimSceneBuilder(_fileSystem);

		_sourceCombo.SelectedIndex = LocationsSource;
		_sourceCombo.SelectedIndexChanged += (s, a) =>
		{
			// The placed-objects list only applies to a loaded location, so the split is
			// collapsed when the models source is shown.
			if (_sourceCombo.SelectedIndex == ModelsSource)
			{
				SetLeftGridSplit(false);
			}

			QueuePopulateData();
		};

		// The DataGrid draws a filter row underneath its header, so per-column filtering and
		// header sorting come from the grid itself rather than a separate text box.
		_grid = new DataGrid
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
			FillColumnIndex = 0,
			Columns = new DataGridColumnBase[]
			{
				new DataGridTextColumn(nameof(Entry.Name), "Name", 100)
			},
			HasHeader = false,
			IndexColumnWidth = null,
			AcceptsKeyboardFocus = true
		};

		StackPanel.SetProportionType(_grid, ProportionType.Fill);

		_grid.SelectedIndexChanged += (s, a) => OnEntrySelected();
		_gridContainer.Widgets.Add(_grid);

		// The placed-objects grid is display-only: selecting a row does nothing.
		_objectsGrid = new DataGrid
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
			FillColumnIndex = 0,
			Columns = new DataGridColumnBase[]
			{
				new DataGridTextColumn(nameof(SkyrimPlacedObject.Name), "Name", 100)
			},
			HasHeader = false,
			IndexColumnWidth = null,
			AcceptsKeyboardFocus = true
		};

		_leftSplit = new VerticalSplitPane();
		StackPanel.SetProportionType(_leftSplit, ProportionType.Fill);

		_viewer = new DrModelViewWidget
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
		};

		_viewerContainer.Widgets.Add(_viewer);

		_mainSplit.SetSplitterPosition(0, 0.25f);

		QueuePopulateData();
	}

	public void Update(float elapsedSeconds)
	{
		ApplyPendingData();
		_viewer.UpdateCameraInput(elapsedSeconds);
		UpdateCameraInfo();
	}

	private void UpdateCameraInfo()
	{
		var controller = _viewer.CameraController;

		// The controller owns the orientation as yaw and pitch relative to its up axis, which is
		// more reliable here than reading a rotation back off the camera transform.
		var eye = controller.Eye;
		var forward = controller.Forward;

		_cameraPositionLabel.Text = $"Position  {eye.X:0.0}, {eye.Y:0.0}, {eye.Z:0.0}";
		_cameraYawLabel.Text = $"Yaw       {controller.Yaw:0.0} deg";
		_cameraPitchLabel.Text = $"Pitch     {controller.Pitch:0.0} deg";
		_cameraForwardLabel.Text = $"Forward   {forward.X:0.000}, {forward.Y:0.000}, {forward.Z:0.000}";
	}

	private void OnEntrySelected()
	{
		if (!(_grid.SelectedItem is Entry entry))
		{
			return;
		}

		if (entry.Location != null)
		{
			LoadLocation(entry.Location);
			return;
		}

		if (!string.IsNullOrEmpty(entry.ModelPath))
		{
			SetLeftGridSplit(false);
			LoadModel(entry.ModelPath);
		}
	}

	/// <summary>
	/// Splits the left panel so the entry grid shares it with the placed-objects grid, or puts
	/// the entry grid back to filling the whole panel.
	/// </summary>
	private void SetLeftGridSplit(bool split)
	{
		if (split == _isLeftSplit)
		{
			return;
		}

		_isLeftSplit = split;

		if (split)
		{
			_leftPanel.Widgets.Remove(_gridContainer);

			_leftSplit.Widgets.Add(_gridContainer);
			_leftSplit.Widgets.Add(_objectsGrid);
			_leftSplit.SetSplitterPosition(0, 0.5f);

			_leftPanel.Widgets.Add(_leftSplit);
		}
		else
		{
			_leftSplit.Widgets.Remove(_gridContainer);
			_leftSplit.Widgets.Remove(_objectsGrid);

			_leftPanel.Widgets.Add(_gridContainer);
			StackPanel.SetProportionType(_gridContainer, ProportionType.Fill);
		}
	}

	private void LoadModel(string path)
	{
		try
		{
			var model = _fileSystem.LoadModel(path);
			_viewer.Node = new NursiaModelNode
			{
				Model = model
			};
			_statusLabel.Text = $"Loaded {model.Meshes.Length} mesh(es) from {path}";
		}
		catch (Exception ex)
		{
			OSK.LogError($"Failed to load '{path}': {ex.Message}");
			_viewer.PlayerStart = null;
			_viewer.Node = null;
			_statusLabel.Text = ex.Message;
		}
	}

	private void LoadLocation(SkyrimLocation location)
	{
		try
		{
			_statusLabel.Text = $"Loading '{location.Name}'...";
			var world = GetSkyrimWorld();
			if (world == null)
			{
				_statusLabel.Text = "Skyrim.esm is not available.";
				return;
			}

			var scene = _sceneBuilder.Build(world.LinkCache, location.Cell);
			_viewer.PlayerStart = _sceneBuilder.PlayerStart;
			_viewer.Node = scene;

			_objectsGrid.Data = _sceneBuilder.PlacedObjects;
			SetLeftGridSplit(true);

			var start = _sceneBuilder.PlayerStart != null
				? $", start: {_sceneBuilder.PlayerStartSource}"
				: ", no player start";
			_statusLabel.Text = $"Loaded {_sceneBuilder.PlacedObjectCount} object(s) ({_sceneBuilder.LoadedModelCount} mesh group(s)){start} from {location.Name}";
		}
		catch (Exception ex)
		{
			OSK.LogError($"Failed to load '{location.Name}': {ex.Message}");
			_viewer.PlayerStart = null;
			_viewer.Node = null;
			_statusLabel.Text = ex.Message;
		}
	}

	private SkyrimWorld GetSkyrimWorld()
	{
		lock (_skyrimWorldLock)
		{
			if (_skyrimWorld != null)
			{
				return _skyrimWorld;
			}

			var pluginPath = _fileSystem.GetPluginPath("Skyrim.esm");
			if (!File.Exists(pluginPath))
			{
				OSK.LogWarning($"Unable to find plugin '{pluginPath}'");
				return null;
			}

			OSK.LogInfo($"Loading '{pluginPath}'...");
			_skyrimWorld = new SkyrimWorld(pluginPath);
			OSK.LogInfo($"Found {_skyrimWorld.Locations.Count} location(s).");
			return _skyrimWorld;
		}
	}

	private void QueuePopulateData()
	{
		var source = _sourceCombo.SelectedIndex;
		var version = Interlocked.Increment(ref _populateVersion);
		_pendingData = null;

		_statusLabel.Text = "Populating list...";

		Task.Run(() =>
		{
			try
			{
				var data = new List<Entry>();

				if (source == LocationsSource)
				{
					PopulateLocations(data);
				}
				else
				{
					PopulateModels(data);
				}

				if (version == Volatile.Read(ref _populateVersion))
				{
					_pendingData = data;
				}
			}
			catch (Exception ex)
			{
				OSK.LogError($"Failed to populate list: {ex.Message}");
			}
		});
	}

	private void PopulateModels(List<Entry> data)
	{
		foreach (var key in _fileSystem.Keys)
		{
			var ext = Path.GetExtension(key);
			if (!string.Equals(ext, ".nif", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			data.Add(new Entry(null, key));
		}
	}

	private void PopulateLocations(List<Entry> data)
	{
		var world = GetSkyrimWorld();
		if (world == null)
		{
			return;
		}

		foreach (var location in world.Locations)
		{
			data.Add(new Entry(location, null));
		}
	}

	private void ApplyPendingData()
	{
		var data = _pendingData;
		if (data == null)
		{
			return;
		}

		_pendingData = null;
		_grid.Data = data;

		var noun = _sourceCombo.SelectedIndex == LocationsSource ? "locations" : "models";
		_statusLabel.Text = $"There are {data.Count} {noun}.";
	}

	private sealed class Entry
	{
		public Entry(SkyrimLocation location, string modelPath)
		{
			Location = location;
			ModelPath = modelPath;
			Name = location?.Name ?? modelPath;
		}

		/// <summary>
		/// The value the grid's text column binds to, so the filter row matches on it.
		/// </summary>
		public string Name { get; }

		public SkyrimLocation Location { get; }

		public string ModelPath { get; }
	}
}
