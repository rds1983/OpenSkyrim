using System;
using System.Collections.Generic;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

namespace OpenSkyrim.NifViewer;

public sealed class SkyrimLocation
{
	public string Name { get; init; }
	public ICellGetter Cell { get; init; }
	public IWorldspaceGetter Worldspace { get; init; }

	public override string ToString() => Name;
}

public sealed class SkyrimWorld : IDisposable
{
	private readonly ISkyrimModDisposableGetter _mod;
	private readonly List<SkyrimLocation> _locations = new List<SkyrimLocation>();

	public ILinkCache LinkCache { get; }
	public IReadOnlyList<SkyrimLocation> Locations => _locations;

	public SkyrimWorld(string pluginPath)
	{
		_mod = SkyrimMod.CreateFromBinaryOverlay(new ModPath(pluginPath), SkyrimRelease.SkyrimSE);
		LinkCache = _mod.ToImmutableLinkCache();
		BuildLocations();
	}

	public void Dispose() => _mod?.Dispose();

	private void BuildLocations()
	{
		var cells = _mod.Cells;
		if (cells != null)
		{
			foreach (var block in cells.Records)
			{
				foreach (var subBlock in block.SubBlocks)
				{
					foreach (var cell in subBlock.Cells)
					{
						_locations.Add(new SkyrimLocation
						{
							Name = GetCellName(cell, null),
							Cell = cell
						});
					}
				}
			}
		}

		var worldspaces = _mod.Worldspaces;
		if (worldspaces != null)
		{
			foreach (var worldspace in worldspaces.RecordCache.Items)
			{
				foreach (var block in worldspace.SubCells)
				{
					foreach (var subBlock in block.Items)
					{
						foreach (var cell in subBlock.Items)
						{
							_locations.Add(new SkyrimLocation
							{
								Name = GetCellName(cell, worldspace),
								Cell = cell,
								Worldspace = worldspace
							});
						}
					}
				}
			}
		}
	}

	private static string GetCellName(ICellGetter cell, IWorldspaceGetter worldspace)
	{
		if (worldspace != null)
		{
			var name = worldspace.Name?.String;
			if (string.IsNullOrWhiteSpace(name))
			{
				name = worldspace.EditorID;
			}

			var point = cell.Grid?.Point ?? default;
			return $"{name} ({point.X}, {point.Y})";
		}

		var cellName = cell.Name?.String;
		if (string.IsNullOrWhiteSpace(cellName))
		{
			cellName = cell.EditorID;
		}

		return cellName;
	}
}
