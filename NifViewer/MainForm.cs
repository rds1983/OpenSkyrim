using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSkyrim.NifViewer;

public class MainForm : Grid
{
	private readonly GraphicsDevice _graphicsDevice;
	private readonly SkyrimFileSystem _fileSystem;

	private ListView _listView;
	private TextBox _filterTextBox;
	private DrModelViewWidget _viewer;
	private Label _headerLabel;
	private Label _countLabel;
	private Label _statusLabel;

	private int _populateVersion;
	private volatile ListView _pendingListView;

	public MainForm(GraphicsDevice graphicsDevice, SkyrimFileSystem fileSystem)
	{
		_graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
		_fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

		RowSpacing = 4;
		ColumnSpacing = 8;
		Padding = new Thickness(8);

		ColumnsProportions.Add(new Proportion(ProportionType.Part, 1.0f));
		ColumnsProportions.Add(new Proportion(ProportionType.Part, 2.0f));
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

		_filterTextBox = new TextBox
		{
			HorizontalAlignment = HorizontalAlignment.Stretch
		};

		_filterTextBox.TextChanged += (s, a) => QueuePopulateListView();

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
		Grid.SetColumn(_filterTextBox, 0);
		Grid.SetRow(_filterTextBox, 2);
		Grid.SetColumn(_viewer, 1);
		Grid.SetRow(_viewer, 3);
		Grid.SetColumn(_statusLabel, 0);
		Grid.SetColumnSpan(_statusLabel, 2);
		Grid.SetRow(_statusLabel, 4);

		Widgets.Add(_headerLabel);
		Widgets.Add(_countLabel);
		Widgets.Add(_filterTextBox);
		Widgets.Add(_viewer);
		Widgets.Add(_statusLabel);

		QueuePopulateListView();
	}

	public void Update(float elapsedSeconds)
	{
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

		var path = item.Tag.ToString();

		try
		{
			using (var stream = _fileSystem.Open(path))
			{
				var model = NifModelLoader.LoadDrModel(_graphicsDevice, stream, string.Empty, _fileSystem);
				_viewer.Model = model;
				_statusLabel.Text = $"Loaded {model.Meshes.Length} mesh(es) from {path}";
			}
		}
		catch (Exception ex)
		{
			_viewer.Model = null;
			_statusLabel.Text = ex.Message;
		}
	}

	private void QueuePopulateListView()
	{
		var filter = _filterTextBox.Text;
		var version = Interlocked.Increment(ref _populateVersion);
		_pendingListView = null;

		Task.Run(() =>
		{
			try
			{
				var listView = new ListView
				{
					HorizontalAlignment = HorizontalAlignment.Stretch,
					VerticalAlignment = VerticalAlignment.Stretch
				};

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
		Grid.SetRow(listView, 3);
		Widgets.Add(listView);
		_listView = listView;

		_statusLabel.Text = $"There are {_listView.Widgets.Count} models.";
	}
}