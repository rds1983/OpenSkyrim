using Mutagen.Bethesda.Skyrim;

namespace OpenSkyrim.Viewer.Data;

public sealed class Location
{
	public string Name { get; init; }
	public ICellGetter Cell { get; init; }
	public IWorldspaceGetter Worldspace { get; init; }

	public override string ToString() => Name;
}
