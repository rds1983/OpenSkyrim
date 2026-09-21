using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Nursia.SceneGraph;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSkyrim.NifViewer;

public class MainForm : Grid
{
	private const int LocationsSource = 0;
	private const int ModelsSource = 1;

	private readonly GraphicsDevice _graphicsDevice;
	private readonly SkyrimFileSystem _fileSystem;
	private readonly SkyrimSceneBuilder _sceneBuilder;
	private readonly object _skyrimWorldLock = new object();

	private ListView _listView;
	private ComboView _sourceCombo;
	private TextBox _filterTextBox;
	private DrModelViewWidget _viewer;
	private Label _headerLabel;
	private Label _countLabel;
	private Label _statusLabel;

	private SkyrimWorld _skyrimWorld;
	private int _populateVersion;
	private volatile ListView _pendingListView;
	private bool _filterPopulatePending;
	private float _filterPopulateDelay;

	public MainForm(GraphicsDevice graphicsDevice, SkyrimFileSystem fileSystem)
	{
		_graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
		_fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
		_sceneBuilder = new SkyrimSceneBuilder(_fileSystem);

		RowSpacing = 4;
		ColumnSpacing = 8;
		Padding = new Thickness(8);

		ColumnsProportions.Add(new Proportion(ProportionType.Part, 1.0f));
		ColumnsProportions.Add(new Proportion(ProportionType.Part, 2.0f));
		RowsProportions.Add(new Proportion(ProportionType.Auto));
		RowsProportions.Add(new Proportion(ProportionType.Auto));
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
		_sourceCombo.SelectedIndexChanged += (s, a) =>
		{
			_filterPopulatePending = false;
			QueuePopulateListView();
		};

		_filterTextBox = new TextBox
		{
			HorizontalAlignment = HorizontalAlignment.Stretch
		};

		_filterTextBox.TextChanged += (s, a) => QueuePopulateListViewDebounced();

		_viewer = new DrModelViewWidget
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
		};

		_statusLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Stretch
		};

		Grid.SetColumn(_headerLabel, 0);
		Grid.SetColumnSpan(_headerLabel, 2);
		Grid.SetRow(_headerLabel, 0);
		Grid.SetColumn(_countLabel, 0);
		Grid.SetColumnSpan(_countLabel, 2);
		Grid.SetRow(_countLabel, 1);
		Grid.SetColumn(_sourceCombo, 0);
		Grid.SetRow(_sourceCombo, 2);
		Grid.SetColumn(_filterTextBox, 0);
		Grid.SetRow(_filterTextBox, 3);
		Grid.SetColumn(_viewer, 1);
		Grid.SetRow(_viewer, 4);
		Grid.SetColumn(_statusLabel, 0);
		Grid.SetColumnSpan(_statusLabel, 2);
		Grid.SetRow(_statusLabel, 5);

		Widgets.Add(_headerLabel);
		Widgets.Add(_countLabel);
		Widgets.Add(_sourceCombo);
		Widgets.Add(_filterTextBox);
		Widgets.Add(_viewer);
		Widgets.Add(_statusLabel);

		QueuePopulateListView();
	}

	public void Update(float elapsedSeconds)
	{
		ApplyFilterDebounce(elapsedSeconds);
		ApplyPendingListView();
		_viewer.UpdateCameraInput(elapsedSeconds);
	}

	private void OnListItemSelected()
	{
		var item = _listView.SelectedItem as Label;
		if (item == null)
		{
			return;
		}

		if (item.Tag is SkyrimLocation location)
		{
			LoadLocation(location);
			return;
		}

		var path = item.Tag?.ToString();
		if (string.IsNullOrEmpty(path))
		{
			return;
		}

		LoadModel(path);
	}

	private void LoadModel(string path)
	{
		try
		{
			var model = _fileSystem.LoadModel(_graphicsDevice, path);
			_viewer.Node = new NursiaModelNode
			{
				Model = model
			};
			_statusLabel.Text = $"Loaded {model.Meshes.Length} mesh(es) from {path}";
		}
		catch (Exception ex)
		{
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
			_viewer.Node = scene;
			_statusLabel.Text = $"Loaded {_sceneBuilder.PlacedObjectCount} object(s) ({_sceneBuilder.LoadedModelCount} mesh group(s)) from {location.Name}";
		}
		catch (Exception ex)
		{
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

	private void QueuePopulateListViewDebounced(float delaySeconds = 1f)
	{
		_filterPopulatePending = true;
		_filterPopulateDelay = delaySeconds;
		_statusLabel.Text = "Populating list...";
	}

	private void ApplyFilterDebounce(float elapsedSeconds)
	{
		if (!_filterPopulatePending)
		{
			return;
		}

		_filterPopulateDelay -= elapsedSeconds;
		if (_filterPopulateDelay > 0f)
		{
			return;
		}

		_filterPopulatePending = false;
		QueuePopulateListView();
	}

	private void QueuePopulateListView()
	{
		var filter = _filterTextBox.Text;
		var source = _sourceCombo.SelectedIndex;
		var version = Interlocked.Increment(ref _populateVersion);
		_pendingListView = null;

		_statusLabel.Text = "Populating list...";

		Task.Run(() =>
		{
			try
			{
				var listView = new ListView
				{
					HorizontalAlignment = HorizontalAlignment.Stretch,
					VerticalAlignment = VerticalAlignment.Stretch
				};

				if (source == LocationsSource)
				{
					PopulateLocations(listView, filter);
				}
				else
				{
					PopulateModels(listView, filter);
				}

				if (version == Volatile.Read(ref _populateVersion))
				{
					_pendingListView = listView;
				}
			}
			catch (Exception ex)
			{
				OSK.LogError($"Failed to populate list: {ex.Message}");
			}
		});
	}

	private void PopulateModels(ListView listView, string filter)
	{
		foreach (var key in _fileSystem.Keys)
		{
			var ext = Path.GetExtension(key);
			if (!string.Equals(ext, ".nif", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (!string.IsNullOrEmpty(filter) && key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
			{
				continue;
			}

			listView.Widgets.Add(new Label
			{
				Text = key,
				Tag = key
			});
		}
	}

	private void PopulateLocations(ListView listView, string filter)
	{
		var world = GetSkyrimWorld();
		if (world == null)
		{
			return;
		}

		foreach (var location in world.Locations)
		{
			if (!string.IsNullOrEmpty(filter) && location.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
			{
				continue;
			}

			listView.Widgets.Add(new Label
			{
				Text = location.Name,
				Tag = location
			});
		}
	}

	private void ApplyPendingListView()
	{
		var listView = _pendingListView;
		if (listView == null)
		{
			return;
		}

		_pendingListView = null;

		if (_listView != null)
		{
			Widgets.Remove(_listView);
		}

		listView.SelectedIndexChanged += (s, a) => OnListItemSelected();

		Grid.SetColumn(listView, 0);
		Grid.SetRow(listView, 4);
		Widgets.Add(listView);
		_listView = listView;

		var noun = _sourceCombo.SelectedIndex == LocationsSource ? "locations" : "models";
		_statusLabel.Text = $"There are {_listView.Widgets.Count} {noun}.";
	}
}
