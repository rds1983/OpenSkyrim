using System;

namespace OpenSkyrim.NifViewer;

class Program
{
	static int Main(string[] args)
	{
		if (args.Length == 0)
		{
			Console.WriteLine("Usage: NifViewer <skyrim-folder>");
			Console.WriteLine("  <skyrim-folder>  Path to the Skyrim installation folder, e.g. \"D:\\SteamLibrary\\steamapps\\common\\Skyrim Special Edition\"");
			return 1;
		}

		try
		{
			Environment.SetEnvironmentVariable("FNA3D_FORCE_DRIVER", "D3D11");

			using (var game = new NifViewerGame(args[0]))
			{
				game.Run();
			}

			return 0;
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.ToString());
			return 1;
		}
	}
}