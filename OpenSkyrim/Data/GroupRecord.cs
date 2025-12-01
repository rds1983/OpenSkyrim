namespace OpenSkyrim.Data;

public class GroupRecord: BaseRecord
{
	public GroupType GroupType => (GroupType)Header.group.type;
}
