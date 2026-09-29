using Microsoft.Xna.Framework;
using Noggog;
using System;

namespace OpenSkyrim.NifViewer.Utility;

internal static class Mathematics
{
	public static Vector3 ToVector3(this P3Float v) => new Vector3(v.X, v.Y, v.Z);
	public static Vector3 ToVector3(this System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

	public static Vector3 ToDegrees(this Vector3 v) => new Vector3(MathHelper.ToDegrees(v.X), MathHelper.ToDegrees(v.Y), MathHelper.ToDegrees(v.Z));

	public static Vector3 ToEulerAngles(this Quaternion r)
	{
		return new Vector3
		{
			X = (float)Math.Asin(2.0f * (r.X * r.W - r.Y * r.Z)),
			Y = (float)Math.Atan2(2.0f * (r.Y * r.W + r.X * r.Z), 1.0f - 2.0f * (r.X * r.X + r.Y * r.Y)),
			Z = (float)Math.Atan2(2.0f * (r.X * r.Y + r.Z * r.W), 1.0f - 2.0f * (r.X * r.X + r.Z * r.Z))
		};
	}
}
