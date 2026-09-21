using Microsoft.Xna.Framework;
using Noggog;

namespace OpenSkyrim.NifViewer.Utility;

internal static class Math
{
	public static Vector3 ToVector3(this P3Float v) => new Vector3(v.X, v.Y, v.Z);

	public static Vector3 ToDegrees(this Vector3 v) => new Vector3(MathHelper.ToDegrees(v.X), MathHelper.ToDegrees(v.Y), MathHelper.ToDegrees(v.Z));
}
