using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenSkyrim
{
	internal static class Utility
	{
		public static string Version
		{
			get
			{
				var assembly = typeof(Utility).Assembly;
				var name = new AssemblyName(assembly.FullName);

				return name.Version.ToString();
			}
		}

		public static unsafe bool ReadStruct<T>(this BinaryReader reader, out T result) where T : struct
		{
			var size = Marshal.SizeOf<T>();

			if (reader.BaseStream.Length - reader.BaseStream.Position < size)
			{
				result = new T();
				return false;
			}

			var bytes = reader.ReadBytes(size);
			var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
			result = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
			handle.Free();

			return true;
		}

		public static void EnsureReadStruct<T>(this BinaryReader reader, out T result) where T : struct
		{
			if (!reader.ReadStruct(out result))
			{
				throw new Exception($"Unable to read struct of type {typeof(T)}");
			}
		}
		
		public static uint FOURCC(string s)
		{
			return (uint)(s[0] | (s[1] << 8) | (s[2] << 16) | (s[3] << 24));
		}

		public static byte[] EnsureReadBytes(this BinaryReader reader, int size)
		{
			if (reader.BaseStream.Length - reader.BaseStream.Position < size)
			{
				throw new Exception($"Can't read {size} bytes. Position = {reader.BaseStream.Position}. Length = {reader.BaseStream.Length}");
			}

			return reader.ReadBytes(size);
		}

		public static string ReadZString(this BinaryReader reader, int size)
		{
			var bytes = reader.EnsureReadBytes(size);

			var count = bytes.Length;
			if (bytes[bytes.Length - 1] == '\0')
			{
				--count;
			}

			return Encoding.UTF8.GetString(bytes, 0, count);
		}

		public static string FormatFloat(this float f) => f.ToString("0.##");
	}
}
