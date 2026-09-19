using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace OpenSkyrim.NifViewer
{
	public class NifViewerGame : Game
	{
		private readonly GraphicsDeviceManager _graphics;
		private readonly string _skyrimFolder;

		private Desktop _desktop;
		private ListView _listView;
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

			MyraEnvironment.Game = this;

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

			_listView.SelectedIndexChanged += (s, a) =>
			{
				var item = _listView.SelectedItem as Label;
				_statusLabel.Text = item?.Tag?.ToString() ?? item?.Text ?? string.Empty;
			};

			_statusLabel = new Label
			{
				HorizontalAlignment = HorizontalAlignment.Stretch
			};

			var grid = new Grid
			{
				RowSpacing = 4,
				Padding = new Thickness(8)
			};

			grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
			grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
			grid.RowsProportions.Add(new Proportion(ProportionType.Part, 1.0f));
			grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 24));

			Grid.SetRow(_headerLabel, 0);
			Grid.SetRow(_countLabel, 1);
			Grid.SetRow(_listView, 2);
			Grid.SetRow(_statusLabel, 3);

			grid.Widgets.Add(_headerLabel);
			grid.Widgets.Add(_countLabel);
			grid.Widgets.Add(_listView);
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

		protected override void Draw(GameTime gameTime)
		{
			base.Draw(gameTime);

			GraphicsDevice.Clear(Color.Black);

			_desktop.Render();
		}
	}
}