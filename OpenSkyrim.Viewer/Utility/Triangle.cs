using Microsoft.Xna.Framework;
using System;

namespace OpenSkyrim.Viewer.Utility;

/// <summary>
/// A triangle defined by three vertices in space.
/// </summary>
public readonly struct Triangle
{
	public Triangle(Vector3 v0, Vector3 v1, Vector3 v2)
	{
		V0 = v0;
		V1 = v1;
		V2 = v2;
	}

	/// <summary>
	/// Gets the first vertex.
	/// </summary>
	public Vector3 V0 { get; }

	/// <summary>
	/// Gets the second vertex.
	/// </summary>
	public Vector3 V1 { get; }

	/// <summary>
	/// Gets the third vertex.
	/// </summary>
	public Vector3 V2 { get; }

	/// <summary>
	/// Gets the axis-aligned bounding box that contains the triangle.
	/// </summary>
	public BoundingBox BoundingBox => new BoundingBox(Vector3.Min(V0, Vector3.Min(V1, V2)), Vector3.Max(V0, Vector3.Max(V1, V2)));

	/// <summary>
	/// Returns this triangle with its vertices transformed by the given matrix.
	/// </summary>
	public Triangle Transform(Matrix transform)
	{
		return new Triangle(
			Vector3.Transform(V0, transform),
			Vector3.Transform(V1, transform),
			Vector3.Transform(V2, transform));
	}

	/// <summary>
	/// Tests a ray against the triangle using the Möller-Trumbore algorithm. Both faces of the
	/// triangle are tested. On a hit, <paramref name="distance"/> is the ray parameter (which
	/// equals the distance along the ray because its direction is normalized).
	/// </summary>
	public bool Intersects(in Ray ray, out float distance)
	{
		distance = 0.0f;

		var edge1 = V1 - V0;
		var edge2 = V2 - V0;
		var pvec = Vector3.Cross(ray.Direction, edge2);
		var det = Vector3.Dot(edge1, pvec);

		// The absolute value of the determinant makes the test double-sided; a value near zero
		// means the ray is parallel to the triangle.
		if (Math.Abs(det) < 1e-8f)
		{
			return false;
		}

		var invDet = 1.0f / det;
		var tvec = ray.Position - V0;
		var u = Vector3.Dot(tvec, pvec) * invDet;
		if (u < 0.0f || u > 1.0f)
		{
			return false;
		}

		var qvec = Vector3.Cross(tvec, edge1);
		var v = Vector3.Dot(ray.Direction, qvec) * invDet;
		if (v < 0.0f || u + v > 1.0f)
		{
			return false;
		}

		var t = Vector3.Dot(edge2, qvec) * invDet;
		if (t < 0.0f)
		{
			return false;
		}

		distance = t;
		return true;
	}
}
