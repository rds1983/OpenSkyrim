using System;
using System.Reflection;

namespace OpenSkyrim.NifViewer;

public static class OSK
{
	/// <summary>
	/// Gets or sets the action used for info-level logging.
	/// </summary>
	public static Action<string> LogInfo = Console.WriteLine;

	/// <summary>
	/// Gets or sets the action used for warning-level logging.
	/// </summary>
	public static Action<string> LogWarning = Console.WriteLine;

	/// <summary>
	/// Gets or sets the action used for error-level logging.
	/// </summary>
	public static Action<string> LogError = Console.WriteLine;

	public static string Version
	{
		get
		{
			var assembly = typeof(OSK).GetType().Assembly;
			var name = new AssemblyName(assembly.FullName);

			return name.Version.ToString();
		}
	}
}
