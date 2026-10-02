using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Data;
using Nursia.SceneGraph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSkyrim.NifViewer;

public class MainForm : Grid
{
	private const int LocationsSource = 0;
	private const int ModelsSource = 1;

private readonly SkyrimFileSystem _fileSystem;
	private readonly SkyrimSceneBuilder _sceneBuilder;
	private readonly object _skyrimWorldLock = new object();

	private ComboView _sourceCombo;
	private DataGrid _grid;
	private DrModelViewWidget _viewer;
	private Label _headerLabel;
	private Label _countLabel;
	private Label _statusLabel;
	private readonly VerticalStackPanel _leftPanel;

	private readonly Label _cameraPositionLabel;
	private readonly Label _cameraYawLabel;
	private readonly Label _cameraPitchLabel;
	private readonly Label _cameraForwardLabel;

	private SkyrimWorld _skyrimWorld;
	private int _populateVersion;
	private volatile List<Entry> _pendingData;

	public MainForm(SkyrimFileSystem fileSystem)
	{
		_fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
		_sceneBuilder = new SkyrimSceneBuilder(_fileSystem);

		RowSpacing = 4;
		ColumnSpacing = 8;
		Padding = new Thickness(8);

		ColumnsProportions.Add(new Proportion(ProportionType.Part, 1.0f));
		ColumnsProportions.Add(new Proportion(ProportionType.Part, 2.0f));
		RowsProportions.Add(new Proportion(ProportionType.Auto));
		RowsProportions.Add(new Proportion(ProportionType.Auto));
		RowsProportions.Add(new Proportion(ProportionType.Part, 1.0f));
		RowsProportions.Add(new Proportion(ProportionType.Pixels, 24));

		_headerLabel = new Label
		{
			Text = $"Skyrim folder: {_fileSystem.RootPath}",
			HorizontalAlignment = HorizontalAlignment.Stretch
		};

		_countLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Stretch
		};

		_sourceCombo = new ComboView
		{
			HorizontalAlignment = HorizontalAlignment.Stretch
		};

		_sourceCombo.Widgets.Add(new Label { Text = "Locations" });
		_sourceCombo.Widgets.Add(new Label { Text = "Models" });
		_sourceCombo.SelectedIndex = LocationsSource;
		_sourceCombo.SelectedIndexChanged += (s, a) => QueuePopulateData();

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
			IndexColumnWidth = null
		};

		_grid.SelectedIndexChanged += (s, a) => OnEntrySelected();

		_viewer = new DrModelViewWidget
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
		};

		_statusLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Stretch
		};

		_cameraPositionLabel = new Label { Text = "Position" };
		_cameraYawLabel = new Label { Text = "Yaw" };
		_cameraPitchLabel = new Label { Text = "Pitch" };
		_cameraForwardLabel = new Label { Text = "Forward" };

		var cameraInfoPanel = new VerticalStackPanel
		{
			Spacing = 4,

			// A Panel arranges every child at its own bounds, so this padding is what insets the
			// information from the corner of the viewer.
			Padding = new Thickness(8)
		};

		cameraInfoPanel.Widgets.Add(new Label
		{
			Text = "Camera"
		});
		cameraInfoPanel.Widgets.Add(_cameraPositionLabel);
		cameraInfoPanel.Widgets.Add(_cameraYawLabel);
		cameraInfoPanel.Widgets.Add(_cameraPitchLabel);
		cameraInfoPanel.Widgets.Add(_cameraForwardLabel);

		// The panel draws its children on top of each other, so the camera information is an
		// overlay on the viewer rather than sitting beside it.
		var viewerPanel = new Panel();
		viewerPanel.Widgets.Add(_viewer);
		viewerPanel.Widgets.Add(cameraInfoPanel);

		var mainSplit = new HorizontalSplitPane();

		_leftPanel = new VerticalStackPanel
		{
			Spacing = 8
		};

		_leftPanel.Widgets.Add(_sourceCombo);

		StackPanel.SetProportionType(_grid, ProportionType.Fill);
		_leftPanel.Widgets.Add(_grid);

		mainSplit.Widgets.Add(_leftPanel);
		mainSplit.Widgets.Add(viewerPanel);
		mainSplit.SetSplitterPosition(0, 0.25f);


		Grid.SetColumn(_headerLabel, 0);
		Grid.SetColumnSpan(_headerLabel, 2);
		Grid.SetRow(_headerLabel, 0);
		Grid.SetColumn(_countLabel, 0);
		Grid.SetColumnSpan(_countLabel, 2);
		Grid.SetRow(_countLabel, 1);
		Grid.SetColumn(_statusLabel, 0);
		Grid.SetRow(mainSplit, 2);
		Grid.SetColumnSpan(mainSplit, 2);
		Grid.SetColumnSpan(_statusLabel, 2);
		Grid.SetRow(_statusLabel, 3);

		Widgets.Add(_headerLabel);
		Widgets.Add(_countLabel);
		Widgets.Add(mainSplit);
		Widgets.Add(_statusLabel);

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
			LoadModel(entry.ModelPath);
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

			var start = _sceneBuilder.PlayerStart != null ? ", player start" : ", no player start";
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
