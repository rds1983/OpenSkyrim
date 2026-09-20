using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Nursia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenSkyrim.NifViewer
{
	public class NifViewerGame : Game
	{
		private readonly GraphicsDeviceManager _graphics;
		private readonly string _skyrimFolder;

		private Desktop _desktop;
		private ComboView _archiveComboBox;
		private ListView _listView;
		private DrModelViewWidget _viewer;
		private Label _headerLabel;
		private Label _countLabel;
		private Label _statusLabel;

		public NifViewerGame(string skyrimFolder)
		{
			_skyrimFolder = SkyrimData.ResolveSkyrimDataFolder(skyrimFolder);

			_graphics = new GraphicsDeviceManager(this)
			{
				PreferredBackBufferWidth = 1280,
				PreferredBackBufferHeight = 800
			};

			Window.AllowUserResizing = true;
			IsMouseVisible = true;
			Window.Title = "NifViewer - OpenSkyrim";
		}

		protected override void LoadContent()
		{
			base.LoadContent();

			GraphicsDevice.PresentationParameters.RenderTargetUsage = RenderTargetUsage.PreserveContents;

			MyraEnvironment.Game = this;
			Nrs.Game = this;

			_headerLabel = new Label
			{
				Text = $"Skyrim folder: {_skyrimFolder}",
				HorizontalAlignment = HorizontalAlignment.Stretch
			};

			_countLabel = new Label
			{
				HorizontalAlignment = HorizontalAlignment.Stretch
			};

			_archiveComboBox = new ComboView
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch
			};
			_archiveComboBox.SelectedIndexChanged += (s, a) =>
			{
				PopulateListView(GetSelectedArchivePath());
			};

			_listView = new ListView
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch
			};

			_viewer = new DrModelViewWidget
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
			};

			_listView.SelectedIndexChanged += (s, a) =>
			{
				var item = _listView.SelectedItem as Label;
				var path = item?.Tag?.ToString() ?? item?.Text ?? string.Empty;

				try
				{
					if (path.Contains("::", StringComparison.Ordinal))
					{
						var split = path.Split(new[] { "::" }, 2, StringSplitOptions.None);
						var archivePath = split[0];
						var archiveEntryPath = split[1];

						if (SkyrimData.TryGet(Path.GetFileName(archivePath), out var archiveInfo))
						{
							using var memoryStream = archiveInfo.Open(archiveEntryPath);
							var model = NifModelLoader.LoadDrModel(GraphicsDevice, memoryStream, Path.GetFileNameWithoutExtension(archiveEntryPath));
							_viewer.Model = model;
							_statusLabel.Text = $"Loaded {model.Meshes.Length} mesh(es) from {archiveEntryPath} ({archivePath})";
							return;
						}
					}

					if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
					{
						var model = NifModelLoader.LoadDrModel(GraphicsDevice, path);
						_viewer.Model = model;
						_statusLabel.Text = $"Loaded {model.Meshes.Length} mesh(es) from {path}";
						return;
					}

					_viewer.Model = null;
					_statusLabel.Text = path;
				}
				catch (Exception ex)
				{
					_viewer.Model = null;
					_statusLabel.Text = ex.Message;
				}
			};

			_statusLabel = new Label
			{
				HorizontalAlignment = HorizontalAlignment.Stretch
			};

			var grid = new Grid
			{
				RowSpacing = 4,
				ColumnSpacing = 8,
				Padding = new Thickness(8)
			};

			grid.ColumnsProportions.Add(new Proportion(ProportionType.Part, 1.0f));
			grid.ColumnsProportions.Add(new Proportion(ProportionType.Part, 2.0f));
			grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
			grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
			grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
			grid.RowsProportions.Add(new Proportion(ProportionType.Part, 1.0f));
			grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 24));

			Grid.SetColumn(_headerLabel, 0);
			Grid.SetColumnSpan(_headerLabel, 2);
			Grid.SetRow(_headerLabel, 0);
			Grid.SetColumn(_countLabel, 0);
			Grid.SetColumnSpan(_countLabel, 2);
			Grid.SetRow(_countLabel, 1);
			Grid.SetColumn(_archiveComboBox, 0);
			Grid.SetColumnSpan(_archiveComboBox, 2);
			Grid.SetRow(_archiveComboBox, 2);
			Grid.SetColumn(_listView, 0);
			Grid.SetRow(_listView, 3);
			Grid.SetColumn(_viewer, 1);
			Grid.SetRow(_viewer, 3);
			Grid.SetColumn(_statusLabel, 0);
			Grid.SetColumnSpan(_statusLabel, 2);
			Grid.SetRow(_statusLabel, 4);

			grid.Widgets.Add(_headerLabel);
			grid.Widgets.Add(_countLabel);
			grid.Widgets.Add(_archiveComboBox);
			grid.Widgets.Add(_listView);
			grid.Widgets.Add(_viewer);
			grid.Widgets.Add(_statusLabel);

			_desktop = new Desktop
			{
				Root = grid
			};

			SkyrimData.Initialize(_skyrimFolder);
			PopulateArchiveCombo();
			PopulateListView(GetSelectedArchivePath());
		}

		private string GetSelectedArchivePath()
		{
			return (_archiveComboBox.SelectedItem as Label)?.Tag as string;
		}

		private void PopulateArchiveCombo()
		{
			_archiveComboBox.Widgets.Clear();

			var archives = SkyrimData.Archives.Values.ToList();
			archives.Sort((a, b) => string.Compare(a.ArchivePath, b.ArchivePath, StringComparison.OrdinalIgnoreCase));

			foreach (var archive in archives)
			{
				_archiveComboBox.Widgets.Add(new Label
				{
					Text = Path.GetFileName(archive.ArchivePath),
					Tag = archive.ArchivePath
				});
			}

			if (_archiveComboBox.Widgets.Count > 0)
			{
				_archiveComboBox.SelectedIndex = 0;
			}
		}

		private void PopulateListView(string selectedArchive = null)
		{
			_listView.Widgets.Clear();

			var nifFiles = new List<string>();
			try
			{
				if (Directory.Exists(_skyrimFolder))
				{
					foreach (var file in Directory.EnumerateFiles(_skyrimFolder, "*.nif", SearchOption.AllDirectories))
					{
						nifFiles.Add(file);
					}
				}

				foreach (var archive in SkyrimData.Archives.Values)
				{
					foreach (var file in archive.Files)
					{
						if (string.Equals(Path.GetExtension(file), ".nif", StringComparison.OrdinalIgnoreCase))
						{
							nifFiles.Add($"{archive.ArchivePath}::{file}");
						}
					}
				}

				if (!string.IsNullOrWhiteSpace(selectedArchive))
				{
					nifFiles.RemoveAll(file => !file.Contains("::", StringComparison.Ordinal)
						|| !string.Equals(file.Substring(0, file.IndexOf("::", StringComparison.Ordinal)), selectedArchive, StringComparison.OrdinalIgnoreCase));
				}
			}
			catch (Exception ex)
			{
				System.Console.WriteLine(ex.ToString());
			}

			nifFiles.Sort(StringComparer.OrdinalIgnoreCase);

			foreach (var file in nifFiles)
			{
				var displayPath = file.Contains("::", StringComparison.Ordinal)
					? file.Substring(file.IndexOf("::", StringComparison.Ordinal) + 2)
					: Path.GetRelativePath(_skyrimFolder, file);

				_listView.Widgets.Add(new Label
				{
					Text = displayPath,
					Tag = file
				});
			}

			if (string.IsNullOrWhiteSpace(selectedArchive))
			{
				_countLabel.Text = $"Showing {nifFiles.Count} .nif file(s) in {_skyrimFolder}";
			}
			else
			{
				_countLabel.Text = $"Showing {nifFiles.Count} .nif file(s) in {Path.GetFileName(selectedArchive)}";
			}
		}

		protected override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			_viewer?.UpdateCameraInput();
		}

		protected override void Draw(GameTime gameTime)
		{
			base.Draw(gameTime);

			GraphicsDevice.Clear(Color.Black);

			_desktop.Render();
		}
	}
}