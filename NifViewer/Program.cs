using System;

namespace OpenSkyrim.NifViewer;

class Program
{
	static int Main(string[] args)
	{
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