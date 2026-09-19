using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mutagen.Bethesda.Archives;
using Mutagen.Bethesda.Plugins.Meta;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Noggog;
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
		private ListView _listView;
		private DrModelViewWidget _viewer;
		private Label _headerLabel;
		private Label _countLabel;
		private Label _statusLabel;

		public NifViewerGame(string skyrimFolder)
		{
			_skyrimFolder = SkyrimDataScanner.ResolveSkyrimDataFolder(skyrimFolder);

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
						var archive = Archive.CreateReader(GameConstants.SkyrimSE.Release, new FilePath(archivePath));
						var archiveFile = archive.Files.FirstOrDefault(f => string.Equals(f.Path, archiveEntryPath, StringComparison.OrdinalIgnoreCase));

						if (archiveFile != null)
						{
							using var memoryStream = new MemoryStream(archiveFile.GetBytes());
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
			grid.RowsProportions.Add(new Proportion(ProportionType.Part, 1.0f));
			grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 24));

			Grid.SetColumn(_headerLabel, 0);
			Grid.SetColumnSpan(_headerLabel, 2);
			Grid.SetRow(_headerLabel, 0);
			Grid.SetColumn(_countLabel, 0);
			Grid.SetColumnSpan(_countLabel, 2);
			Grid.SetRow(_countLabel, 1);
			Grid.SetColumn(_listView, 0);
			Grid.SetRow(_listView, 2);
			Grid.SetColumn(_viewer, 1);
			Grid.SetRow(_viewer, 2);
			Grid.SetColumn(_statusLabel, 0);
			Grid.SetColumnSpan(_statusLabel, 2);
			Grid.SetRow(_statusLabel, 3);

			grid.Widgets.Add(_headerLabel);
			grid.Widgets.Add(_countLabel);
			grid.Widgets.Add(_listView);
			grid.Widgets.Add(_viewer);
			grid.Widgets.Add(_statusLabel);

			_desktop = new Desktop
			{
				Root = grid
			};

			PopulateListView();
		}

		private void PopulateListView()
		{
			var nifFiles = new List<string>();
			try
			{
				nifFiles.AddRange(SkyrimDataScanner.EnumerateNifFiles(_skyrimFolder));
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

			_countLabel.Text = $"Showing first {nifFiles.Count} .nif file(s) in {_skyrimFolder}";
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