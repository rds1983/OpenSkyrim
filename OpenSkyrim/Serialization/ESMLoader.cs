using OpenSkyrim.Data;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenSkyrim.Serialization;

public static class ESMLoader
{
	private class ESMLoadContext
	{
		private BinaryReader Reader { get; }
		private int RecordRead { get; set; }
		private ESMInfo Result { get; } = new ESMInfo();

		public ESMLoadContext(BinaryReader reader)
		{
			Reader = reader ?? throw new ArgumentNullException(nameof(reader));
		}

		private bool GetSubrecordHeader(out SubRecordHeader subHeader)
		{
			var subrecordSize = Marshal.SizeOf<SubRecordHeader>();
			if (Result.Header.record.dataSize - RecordRead < subrecordSize)
			{
				subHeader = new SubRecordHeader();
				return false;
			}

			var result = Reader.ReadStruct(out subHeader);
			if (result)
			{
				RecordRead += Marshal.SizeOf<SubRecordHeader>() + subHeader.dataSize;
			}

			return result;
			
		}

		public ESMInfo Load()
		{
			// Signature - first 4 bytes
			var s = Encoding.UTF8.GetString(Reader.ReadBytes(4));
			if (s != "TES4")
			{
				throw new Exception("Wrong signature. Expected 'TES4'");
			}

			// determine header size
			var subRecName = Reader.ReadUInt32();

			int recHeaderSize;
			if (subRecName == 0x52444548) // "HEDR"
				recHeaderSize = Marshal.SizeOf<RecordHeader>() - 4; // TES4 header size is 4 bytes smaller than TES5 header
			else
				recHeaderSize = Marshal.SizeOf<RecordHeader>();

			// restart from the beginning (i.e. "TES4" record header)
			Reader.BaseStream.Seek(0, SeekOrigin.Begin);

			Reader.ReadStruct(out Result.Header);

			SubRecordHeader subHeader;
			while (GetSubrecordHeader(out subHeader))
			{
				var typeId = subHeader.typeId;
				if (typeId == Utility.FOURCC("HEDR"))
				{
					Reader.EnsureReadStruct(out Result.HeaderData);
				}
				else if (typeId == Utility.FOURCC("CNAM"))
				{
					Result.Author = Reader.ReadZString(subHeader.dataSize);
				}
				else if (typeId == Utility.FOURCC("SNAM"))
				{
					Result.Description = Reader.ReadZString(subHeader.dataSize);
				}
				else if (typeId == Utility.FOURCC("INTV") ||
					typeId == Utility.FOURCC("INCC") ||
					typeId == Utility.FOURCC("OFST") || // Oblivion only?
					typeId == Utility.FOURCC("DELE") || // Oblivion only?
					typeId == Utility.FOURCC("TNAM") || // Fallout 4 (CK only)
					typeId == Utility.FOURCC("MMSB")) // Fallout 76
				{
					// Skip
					Reader.BaseStream.Seek(subHeader.dataSize, SeekOrigin.Current);
				}
				else
				{
					throw new Exception($"Unknown subheader {typeId}");
				}
			}

			return Result;
		}
	}

	public static ESMInfo Load(string path)
	{
		OS.LogInfo($"Loading ESM: {path}");

		using (var stream = File.OpenRead(path))
		using (var reader = new BinaryReader(stream))
		{
			var context = new ESMLoadContext(reader);

			return context.Load();
		}
	}
}
