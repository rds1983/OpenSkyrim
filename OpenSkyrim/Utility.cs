using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

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
	}
}
