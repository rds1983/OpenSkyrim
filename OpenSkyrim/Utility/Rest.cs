using System.Reflection;

namespace OpenSkyrim.Utility;

internal static class Rest
{
	public static string Version
	{
		get
		{
			var assembly = typeof(Rest).Assembly;
			var name = new AssemblyName(assembly.FullName);

			return name.Version.ToString();
		}
	}

	public static string FormatFloat(this float f) => f.ToString("0.##");
}
