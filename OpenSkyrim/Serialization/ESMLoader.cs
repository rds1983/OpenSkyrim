using OpenSkyrim.Data;
using OpenSkyrim.Utility;
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
			if (Result.ESMHeader.record.dataSize - RecordRead < subrecordSize)
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

		private void LoadHeader()
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

			Reader.ReadStruct(out Result.ESMHeader);

			SubRecordHeader subHeader;
			while (GetSubrecordHeader(out subHeader))
			{
				var typeId = subHeader.typeId;
				if (typeId == StreamUtils.FOURCC("HEDR"))
				{
					Reader.EnsureReadStruct(out Result.ESMHeaderData);
				}
				else if (typeId == StreamUtils.FOURCC("CNAM"))
				{
					Result.Author = Reader.ReadZString(subHeader.dataSize);
				}
				else if (typeId == StreamUtils.FOURCC("SNAM"))
				{
					Result.Description = Reader.ReadZString(subHeader.dataSize);
				}
				else if (typeId == StreamUtils.FOURCC("INTV") ||
					typeId == StreamUtils.FOURCC("INCC") ||
					typeId == StreamUtils.FOURCC("OFST") || // Oblivion only?
					typeId == StreamUtils.FOURCC("DELE") || // Oblivion only?
					typeId == StreamUtils.FOURCC("TNAM") || // Fallout 4 (CK only)
					typeId == StreamUtils.FOURCC("MMSB")) // Fallout 76
				{
					// Skip
					Reader.BaseStream.Seek(subHeader.dataSize, SeekOrigin.Current);
				}
				else
				{
					throw new Exception($"Unknown subheader {typeId}");
				}
			}
		}

		public ESMInfo Load()
		{
			LoadHeader();

			while(!Reader.EndOfStream())
			{
				RecordHeader header;
				if (!Reader.ReadStruct(out header))
				{
					break;
				}

				if (header.record.typeId == StreamUtils.FOURCC("GRUP"))
				{
					// Group record
				} else
				{
					// Ordinary
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
