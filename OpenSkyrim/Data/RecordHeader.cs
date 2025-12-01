using System.Runtime.InteropServices;

namespace OpenSkyrim.Data;

// NOTE: the label field of a group is not reliable (http://www.uesp.net/wiki/Tes4Mod:Mod_File_Format)
[StructLayout(LayoutKind.Explicit, Pack = 1)]
public unsafe struct GroupLabel
{
	[FieldOffset(0)]
	public uint value; // formId, blockNo or raw int representation of type

	[FieldOffset(0)]
	public fixed byte recordType[4]; // record type in ascii

	[FieldOffset(0)]
	public fixed short grid[2]; // grid y, x (note the reverse order)
};

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GroupTypeHeader
{
	public uint typeId;
	public uint groupSize; // includes the 24 bytes (20 for TES4) of header (i.e. this struct)
	public GroupLabel label; // format based on type
	public int type;
	public ushort stamp; // & 0xff for day, & 0xff00 for months since Dec 2002 (i.e. 1 = Jan 2003)
	public ushort unknown;
	public ushort version; // not in TES4
	public ushort unknown2; // not in TES4
};

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RecordTypeHeader
{
	public uint typeId;
	public uint dataSize; // does *not* include 24 bytes (20 for TES4) of header
	public uint flags;
	public uint id;
	public uint revision;
	public ushort version; // not in TES4
	public ushort unknown; // not in TES4
};

[StructLayout(LayoutKind.Explicit, Pack = 1)]
public struct RecordHeader
{
	[FieldOffset(0)]
	public GroupTypeHeader group;

	[FieldOffset(0)]
	public RecordTypeHeader record;
};

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SubRecordHeader
{
	public uint typeId;
	public ushort dataSize;
};

[StructLayout(LayoutKind.Explicit, Pack = 1)]
public struct ESMVersion
{
	[FieldOffset(0)]
	public float f;

	[FieldOffset(0)]
	public uint ui;
};

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct HeaderData
{
	public ESMVersion version; // File format version.
	public int records; // Number of records
	public uint nextObjectId;
};