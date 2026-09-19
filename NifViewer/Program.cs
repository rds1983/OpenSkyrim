using System;
using System.IO;

namespace OpenSkyrim.NifViewer
{
	class Program
	{
		static int Main(string[] args)
		{
			try
			{
				var skyrimFolder = args.Length > 0 ? args[0] : SkyrimDataScanner.DefaultSkyrimFolder;
				var dataFolder = SkyrimDataScanner.ResolveSkyrimDataFolder(skyrimFolder);
				if (!Directory.Exists(dataFolder))
				{
					Console.WriteLine($"Skyrim data folder '{dataFolder}' does not exist.");
					Console.WriteLine($"Expected a Skyrim install under '{SkyrimDataScanner.DefaultSkyrimFolder}' or pass a path to the game root.");
					return 1;
				}

				Environment.SetEnvironmentVariable("FNA3D_FORCE_DRIVER", "D3D11");

				using (var game = new NifViewerGame(dataFolder))
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
}