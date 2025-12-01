using OpenSkyrim.Data;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

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

		OS.LogInfo($"Skyrim Folder: {args[0]}");


		var esmPath = Path.Combine(args[0], "Data/Skyrim.esm");

		using (var stream = File.OpenRead(esmPath))
		using (var reader = new BinaryReader(stream))
		{
			// Signature - first 4 bytes
			var s = Encoding.UTF8.GetString(reader.ReadBytes(4));
			if (s != "TES4")
			{
				throw new Exception("Wrong signature. Expected 'TES4'");
			}

			// determine header size
			var subRecName = reader.ReadUInt32();

			int recHeaderSize;
			if (subRecName == 0x52444548) // "HEDR"
				recHeaderSize = Marshal.SizeOf<RecordHeader>() - 4; // TES4 header size is 4 bytes smaller than TES5 header
			else
				recHeaderSize = Marshal.SizeOf<RecordHeader>();

			// restart from the beginning (i.e. "TES4" record header)
			stream.Seek(0, SeekOrigin.Begin);

			RecordHeader header;
			reader.ReadStruct(out header);

			SubRecordHeader subHeader;
			while(reader.ReadStruct(out subHeader))
			{
				if (subHeader.typeId == Utility.FOURCC("HEDR"))
				{
					HeaderData headerData;
					reader.EnsureReadStruct(out headerData);
				} else if (subHeader.typeId == Utility.FOURCC("CNAM"))
				{

				}
			}
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
