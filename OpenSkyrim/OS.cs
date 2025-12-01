using System;

namespace OpenSkyrim
{
	public static class OS
	{
		public static Action<string> LogInfo = Console.WriteLine;
		public static Action<string> LogWarning = Console.WriteLine;
		public static Action<string> LogError = Console.WriteLine;
	}
}
