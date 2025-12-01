using System;

namespace OpenSkyrim;

class Program
{
	// See system error codes: http://msdn.microsoft.com/en-us/library/windows/desktop/ms681382.aspx
	private const int ERROR_SUCCESS = 0;
	private const int ERROR_UNHANDLED_EXCEPTION = 574;  // 0x23E

	static void ShowUsage()
	{
		Console.WriteLine($"OpenSkyrim {Utility.Version}");
		Console.WriteLine("Usage: OpenSkyrim <skyrimFolder>");
	}

	static void Log(string message)
	{
		Console.WriteLine(message);
	}

	static int Process(string[] args)
	{
		if (args == null || args.Length == 0)
		{
			ShowUsage();
			return ERROR_SUCCESS;
		}

		return ERROR_SUCCESS;
	}

	static int Main(string[] args)
	{
		try
		{
			return Process(args);
		}
		catch (Exception ex)
		{
			Log(ex.ToString());
			return ERROR_UNHANDLED_EXCEPTION;
		}
	}
}
