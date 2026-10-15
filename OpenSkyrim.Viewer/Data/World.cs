using System;
using System.Collections.Generic;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

namespace OpenSkyrim.Viewer.Data;

public sealed class World : IDisposable
{
	private readonly ISkyrimModDisposableGetter _mod;
	private readonly List<Location> _locations = new List<Location>();

	public ILinkCache LinkCache { get; }
	public IReadOnlyList<Location> Locations => _locations;

	public World(string pluginPath)
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
						_locations.Add(new Location
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
							_locations.Add(new Location
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
