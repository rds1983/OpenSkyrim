using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D.UI;
using Nursia;

namespace OpenSkyrim.NifViewer;

public class NifViewerGame : Game
{
	private readonly GraphicsDeviceManager _graphics;
	private readonly string _skyrimFolder;

	private Desktop _desktop;
	private MainForm _mainForm;
	private SkyrimFileSystem _fileSystem;

	public NifViewerGame(string skyrimFolder)
	{
		_skyrimFolder = skyrimFolder;

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

		_fileSystem = new SkyrimFileSystem(_skyrimFolder);
		_mainForm = new MainForm(GraphicsDevice, _fileSystem);

		_desktop = new Desktop
		{
			Root = _mainForm
		};
	}

	protected override void Update(GameTime gameTime)
	{
		base.Update(gameTime);
		_mainForm.Update();
	}

	protected override void Draw(GameTime gameTime)
	{
		base.Draw(gameTime);

		GraphicsDevice.Clear(Color.Black);

		_desktop.Render();
	}
}