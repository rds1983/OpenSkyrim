using DigitalRiseModel.Vertices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NifViewer.Utility;

internal static class XNA
{
	/// <summary>
	/// Gets the number of primitives for the given vertex/index buffer and primitive type.
	/// </summary>
	/// <param name="primitiveType">The primitive type.</param>
	/// <param name="count">The number of vertices or indices.</param>
	/// <returns>The number of primitives in the given vertex and index buffer.</returns>
	public static int GetPrimitiveCount(this PrimitiveType primitiveType, int count)
	{
		switch (primitiveType)
		{
			case PrimitiveType.LineList:
				return count / 2;
			case PrimitiveType.LineStrip:
				return count - 1;
			case PrimitiveType.TriangleList:
				return count / 3;
			case PrimitiveType.TriangleStrip:
				return count - 2;
		}

		throw new NotSupportedException("Unknown primitive type.");
	}

	public static int GetSize(this VertexElementFormat elementFormat)
	{
		switch (elementFormat)
		{
			case VertexElementFormat.Single:
				return 4;
			case VertexElementFormat.Vector2:
				return 8;
			case VertexElementFormat.Vector3:
				return 12;
			case VertexElementFormat.Vector4:
				return 16;
			case VertexElementFormat.Color:
				return 4;
			case VertexElementFormat.Byte4:
				return 4;
			case VertexElementFormat.Short2:
				return 4;
			case VertexElementFormat.Short4:
				return 8;
			case VertexElementFormat.NormalizedShort2:
				return 4;
			case VertexElementFormat.NormalizedShort4:
				return 8;
			case VertexElementFormat.HalfVector2:
				return 4;
			case VertexElementFormat.HalfVector4:
				return 8;
		}

		throw new Exception($"Unknown vertex element format {elementFormat}");
	}

	public static int GetElementsCount(this VertexElementFormat elementFormat)
	{
		switch (elementFormat)
		{
			case VertexElementFormat.Single:
				return 1;
			case VertexElementFormat.Vector2:
				return 2;
			case VertexElementFormat.Vector3:
				return 3;
			case VertexElementFormat.Vector4:
				return 4;
			case VertexElementFormat.Color:
				return 4;
			case VertexElementFormat.Byte4:
				return 4;
			case VertexElementFormat.Short2:
				return 2;
			case VertexElementFormat.Short4:
				return 4;
		}

		throw new Exception($"Unknown vertex element format {elementFormat}");
	}

	public static VertexElement? FindElement(this VertexDeclaration vd, VertexElementUsage usage)
	{
		var ve = vd.GetVertexElements();
		for (var i = 0; i < ve.Length; ++i)
		{
			if (ve[i].VertexElementUsage == usage)
			{
				return ve[i];
			}
		}

		return null;
	}

	public static VertexElement EnsureElement(this VertexDeclaration vd, VertexElementUsage usage)
	{
		var result = vd.FindElement(usage);

		if (result == null)
		{
			throw new Exception($"Could not find vertex element with usage {usage}");
		}

		return result.Value;
	}

	public static VertexBuffer CreateVertexBuffer<T>(this T[] vertices, GraphicsDevice device) where T : struct, IVertexType
	{
		var result = new VertexBuffer(device, new T().VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
		result.SetData(vertices);

		return result;
	}

	public static VertexBuffer CreateVertexBuffer(this Vector3[] vertices, GraphicsDevice device)
	{
		var result = new VertexBuffer(device, VertexPosition.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
		result.SetData(vertices);

		return result;
	}

	public static IndexBuffer CreateIndexBuffer(this ushort[] indices, GraphicsDevice device)
	{
		var result = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
		result.SetData(indices);

		return result;
	}

	public static IndexBuffer CreateIndexBuffer(this int[] indices, GraphicsDevice device)
	{
		var result = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
		result.SetData(indices);

		return result;
	}


	public static void Unwind<T>(this T[] data) where T : struct
	{
		for (var i = 0; i < data.Length / 3; i++)
		{
			var temp = data[i * 3];
			data[i * 3] = data[i * 3 + 2];
			data[i * 3 + 2] = temp;
		}
	}

	public static IEnumerable<Vector3> GetPositions(this VertexPositionNormalTexture[] vertices) => (from v in vertices select v.Position);
	public static IEnumerable<Vector3> GetPositions(this VertexPositionTexture[] vertices) => (from v in vertices select v.Position);
	public static IEnumerable<Vector3> GetPositions(this VertexPositionNormal[] vertices) => (from v in vertices select v.Position);
	public static IEnumerable<Vector3> GetPositions(this VertexPosition[] vertices) => (from v in vertices select v.Position);

	public static BoundingBox BuildBoundingBox(this VertexPositionNormalTexture[] vertices) => BoundingBox.CreateFromPoints(vertices.GetPositions());
	public static BoundingBox BuildBoundingBox(this VertexPositionTexture[] vertices) => BoundingBox.CreateFromPoints(vertices.GetPositions());
	public static BoundingBox BuildBoundingBox(this VertexPositionNormal[] vertices) => BoundingBox.CreateFromPoints(vertices.GetPositions());
	public static BoundingBox BuildBoundingBox(this VertexPosition[] vertices) => BoundingBox.CreateFromPoints(vertices.GetPositions());
	public static BoundingBox BuildBoundingBox(this Vector3[] vertices) => BoundingBox.CreateFromPoints(vertices);
}
