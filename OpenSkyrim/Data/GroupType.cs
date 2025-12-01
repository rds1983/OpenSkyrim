namespace OpenSkyrim.Data;

// Based on http://www.uesp.net/wiki/Tes5Mod:Mod_File_Format#Groups
public enum GroupType
{
	Grp_RecordType = 0,
	Grp_WorldChild = 1,
	Grp_InteriorCell = 2,
	Grp_InteriorSubCell = 3,
	Grp_ExteriorCell = 4,
	Grp_ExteriorSubCell = 5,
	Grp_CellChild = 6,
	Grp_TopicChild = 7,
	Grp_CellPersistentChild = 8,
	Grp_CellTemporaryChild = 9,
	Grp_CellVisibleDistChild = 10
};
